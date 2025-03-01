using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Cosmos;
using Purview.EventSourcing.CosmosDb.Snapshot;

namespace Purview.EventSourcing.CosmosDb;

sealed partial class CosmosDbClient
{
	Database _database = default!;

	readonly AsyncLazy<Container> _container;

	readonly CosmosDbEventStoreOptions _cosmosDbOptions;

	readonly string _containerName;
	readonly string _databaseCreatedKey;
	readonly string _containerCreatedKey;
	readonly string _partitionKey;

	static readonly ConcurrentDictionary<string, CosmosClient> _cosmosDbClients = new();
	static readonly ConcurrentDictionary<string, AsyncLazy<Database>> _createdDatabases = new();
	static readonly ConcurrentDictionary<string, AsyncLazy<Container>> _createdContainers = new();

	public CosmosDbClient([NotNull] CosmosDbEventStoreOptions cosmosDbOptions,
		string? partitionKeyOverride = null,
		string? containerNameOverride = null,
		CosmosClient? cosmosClient = null)
	{
		_cosmosDbOptions = cosmosDbOptions;

		_containerName = containerNameOverride ?? cosmosDbOptions.Container;
		_partitionKey = partitionKeyOverride ?? cosmosDbOptions.PartitionKeyPath;

		_databaseCreatedKey = $"{cosmosDbOptions.ConnectionString}-{cosmosDbOptions.Database}";
		_containerCreatedKey = $"{cosmosDbOptions.ConnectionString}-{_containerName}";

		_container = new AsyncLazy<Container>(() => InitializeAsync(cosmosClient));
	}

	async Task<ContainerResponse?> GetOrCreateContainerAsync(CancellationToken cancellationToken = default)
	{
		var client = GetOrCreateClient();
		var database = await InitializeDatabase(client, cancellationToken);
		var container = database.GetContainer(_containerName);

		var response = await container.ReadContainerStreamAsync(cancellationToken: cancellationToken);
		var containerExists = response.StatusCode == System.Net.HttpStatusCode.OK;

		var indexOptions = _cosmosDbOptions.IndexOptions;
		if (containerExists)
		{
			// Attempt to modify the container...
			var containerRequiresUpdate = false;
			var containerResponse = await container.ReadContainerAsync(cancellationToken: cancellationToken);

			#region Update existing resource

			if (indexOptions.Automatic != containerResponse.Resource.IndexingPolicy.Automatic)
			{
				containerResponse.Resource.IndexingPolicy.Automatic = indexOptions.Automatic;
				containerRequiresUpdate = true;
			}

			if (indexOptions.IndexingModel != containerResponse.Resource.IndexingPolicy.IndexingMode)
			{
				containerResponse.Resource.IndexingPolicy.IndexingMode = indexOptions.IndexingModel;
				containerRequiresUpdate = true;
			}

			foreach (var includePath in indexOptions.IncludedPaths)
			{
				if (!containerResponse.Resource.IndexingPolicy.IncludedPaths.Any(m => m.Path == includePath))
				{
					containerResponse.Resource.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = includePath });
					containerRequiresUpdate = true;
				}
			}

			foreach (var excludePath in indexOptions.ExcludedPaths)
			{
				if (!containerResponse.Resource.IndexingPolicy.ExcludedPaths.Any(m => m.Path == excludePath))
				{
					containerResponse.Resource.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = excludePath });
					containerRequiresUpdate = true;
				}
			}

			foreach (var spatialIndex in indexOptions.SpatialIndices)
			{
				if (!containerResponse.Resource.IndexingPolicy.SpatialIndexes.Any(m => m.Path == spatialIndex.Path))
				{
					containerResponse.Resource.IndexingPolicy.SpatialIndexes.Add(spatialIndex);
					containerRequiresUpdate = true;
				}
			}

			foreach (var compositeIndexSetFromConfig in indexOptions.CompositeIndices)
			{
				var existingSet = containerResponse.Resource.IndexingPolicy.CompositeIndexes.Any(existingCompositeIndexSet =>
					existingCompositeIndexSet.All(existingCompositeIndex =>
						compositeIndexSetFromConfig.Any(compositeIndexFromConfig => compositeIndexFromConfig.Path == existingCompositeIndex.Path && compositeIndexFromConfig.Order == existingCompositeIndex.Order)));

				if (!existingSet)
				{
					containerResponse.Resource.IndexingPolicy.CompositeIndexes.Add(new System.Collections.ObjectModel.Collection<CompositePath>([.. compositeIndexSetFromConfig]));
					containerRequiresUpdate = true;
				}
			}

			#endregion Update existing resource

			if (containerRequiresUpdate)
			{
				try
				{
					await container.ReplaceContainerStreamAsync(containerResponse.Resource, cancellationToken: cancellationToken);
					return await container.ReadContainerAsync(cancellationToken: cancellationToken);
				}
				catch
				{
					// Hopefully this is just because it was already created, perhaps when running in a multi-instance environment...
					// Should we do something with this?
				}
			}

			return containerResponse;
		}
		else
		{
			// Attempt to create the container...
			var containerBuilder = database.DefineContainer(_containerName, _partitionKey);

			#region Attempt to create the container

			var indexingPolicyBuilder = containerBuilder
				.WithIndexingPolicy()
					.WithAutomaticIndexing(indexOptions.Automatic)
					.WithIndexingMode(indexOptions.IndexingModel);

			var includedPathBuilder = indexingPolicyBuilder.WithIncludedPaths();
			foreach (var includedPath in indexOptions.IncludedPaths)
				includedPathBuilder = includedPathBuilder.Path(includedPath);

			indexingPolicyBuilder = includedPathBuilder.Attach();

			var excludedPathBuilder = indexingPolicyBuilder.WithExcludedPaths();
			foreach (var excludedPath in indexOptions.ExcludedPaths)
				excludedPathBuilder = excludedPathBuilder.Path(excludedPath);

			indexingPolicyBuilder = excludedPathBuilder.Attach();

			foreach (var compositeIndexSet in indexOptions.CompositeIndices)
			{
				var compositeIndexBuilder = indexingPolicyBuilder.WithCompositeIndex();
				foreach (var compositeIndex in compositeIndexSet)
					compositeIndexBuilder.Path(compositeIndex.Path, compositeIndex.Order);

				indexingPolicyBuilder = compositeIndexBuilder.Attach();
			}

			containerBuilder = indexingPolicyBuilder.Attach();

			#endregion Attempt to create the container

			var containerResponse = await containerBuilder.CreateIfNotExistsAsync(_cosmosDbOptions.ContainerThroughput, cancellationToken);
			if (containerResponse.StatusCode == System.Net.HttpStatusCode.OK || containerResponse.StatusCode == System.Net.HttpStatusCode.Accepted)
				return containerResponse;

			// Hoping that someone came in between and created it while we were waiting.
			return await container.ReadContainerAsync(cancellationToken: cancellationToken);
		}
	}

	async Task<Container> InitializeAsync(CosmosClient? cosmosClient, CancellationToken cancellationToken = default)
	{
		var client = cosmosClient ?? GetOrCreateClient();
		await InitializeDatabase(client, cancellationToken);

		return await InitializeContainerAsync(cancellationToken);
	}

	async Task<Container> InitializeContainerAsync(CancellationToken cancellationToken)
	{
		return await _createdContainers.GetOrAdd(_containerCreatedKey, _ => new AsyncLazy<Container>(async () =>
		{
			var response = await GetOrCreateContainerAsync(cancellationToken) ?? throw new NullReferenceException($"Unable to get the container response for '{_containerName}'");
			if (!(response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Accepted))
				throw new InvalidOperationException($"Unable to get or create the container '{_containerName}', with status code: {response.StatusCode}");

			return response.Container;
		}));
	}

	async Task<Database> InitializeDatabase(CosmosClient client, CancellationToken cancellationToken)
	{
		return _database = await _createdDatabases.GetOrAdd(_databaseCreatedKey, _ => new AsyncLazy<Database>(async () =>
		{
			var response = await client.CreateDatabaseIfNotExistsAsync(_cosmosDbOptions.Database, throughput: _cosmosDbOptions.DatabaseThroughput, cancellationToken: cancellationToken);
			if (!(response.StatusCode == System.Net.HttpStatusCode.OK || response.StatusCode == System.Net.HttpStatusCode.Accepted || response.StatusCode == System.Net.HttpStatusCode.Created))
				throw new InvalidOperationException($"Unable to get or create the database '{_cosmosDbOptions.Database}', with status code: {response.StatusCode}");

			return response.Database;
		}));
	}

	CosmosClient GetOrCreateClient()
		=> GetOrCreateClient(_cosmosDbOptions);

	static CosmosClient GetOrCreateClient(CosmosDbEventStoreOptions configuration)
	{
		return _cosmosDbClients.GetOrAdd($"{configuration.ConnectionString}".ToUpperInvariant(), _ =>
		{
			CosmosClientOptions clientOptions = new()
			{
				ConnectionMode = configuration.ConnectionMode,
				RequestTimeout = TimeSpan.FromSeconds(configuration.RequestTimeoutInSeconds ?? CosmosDbOptions.DefaultRequestTimeout),
				Serializer = new CosmosJsonNetSerializer(JsonHelpers.JsonSerializerSettings)
			};

			if (configuration.IgnoreSSLWarnings)
			{
				clientOptions.HttpClientFactory = () =>
				{
					HttpMessageHandler httpMessageHandler = new HttpClientHandler
					{
						ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
					};

					return new(httpMessageHandler);
				};
			}

			return new(configuration.ConnectionString, clientOptions);
		});
	}
}
