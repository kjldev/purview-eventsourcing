using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Azure.Data.Tables;

namespace Purview.EventSourcing.AzureStorage.StorageClients.Table;

sealed class AzureTableClient
{
	public const int MaximumBatchSize = 100;

	readonly AsyncLazy<TableClient> _tableClient;
	readonly AzureStorageEventStoreOptions _configuration;
	readonly string _tableName;

	static readonly string[] SelectedColumnsForEntityExistsQuery = [nameof(ITableEntity.PartitionKey)];

	public AzureTableClient(AzureStorageEventStoreOptions configuration, string? tableOverride = null)
	{
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

		_tableName = tableOverride ?? _configuration.Table;
		_tableClient = new AsyncLazy<TableClient>(InitializeAsync);
	}

	public async Task<T?> OperationAsync<T>(TableTransactionActionType action, T entity, CancellationToken cancellationToken = default)
		where T : class, ITableEntity, new()
	{
		ArgumentNullException.ThrowIfNull(entity, nameof(entity));

		var table = await _tableClient;
		Azure.Response response;
		switch (action)
		{
			case TableTransactionActionType.Add:
				response = await table.AddEntityAsync(entity, cancellationToken);
				break;
			case TableTransactionActionType.Delete:
				response = await table.DeleteEntityAsync(entity.PartitionKey, entity.RowKey, entity.ETag, cancellationToken);
				break;
			case TableTransactionActionType.UpdateMerge:
			case TableTransactionActionType.UpdateReplace:
				var updateMode = action == TableTransactionActionType.UpdateMerge
					? TableUpdateMode.Merge
					: TableUpdateMode.Replace;
				response = await table.UpdateEntityAsync(entity, entity.ETag, updateMode, cancellationToken);
				break;
			case TableTransactionActionType.UpsertMerge:
			case TableTransactionActionType.UpsertReplace:
				var upsertMode = action == TableTransactionActionType.UpsertMerge
					? TableUpdateMode.Merge
					: TableUpdateMode.Replace;
				response = await table.UpsertEntityAsync(entity, upsertMode, cancellationToken);
				break;
			default:
				throw new InvalidEnumArgumentException(nameof(action), (int)action, typeof(TableTransactionActionType));
		}

		var status = response.Status;
		if (status >= 400)
			throw new TableOperationException(entity, action, response);

		return action == TableTransactionActionType.Delete
			? null
			: entity;
	}

	public async Task<BatchOperationResult> SubmitBatchAsync(BatchOperation batchOperation, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(batchOperation, nameof(batchOperation));
		if (!batchOperation.Any())
			throw new InvalidOperationException("No entities in batch.");

		var tableClient = await _tableClient;
		var sets = batchOperation.GetActions().Chunk(size: MaximumBatchSize);

		Dictionary<int, Azure.Response[]> responses = [];
		var idx = 0;
		foreach (var actions in sets)
		{
			var response = await tableClient.SubmitTransactionAsync(actions, cancellationToken);
			if (response != null)
				responses.Add(idx, [.. response.Value]);

			idx++;
		}

		return new(responses);
	}

	public async IAsyncEnumerable<T> QueryEnumerableAsync<T>(Expression<Func<T, bool>> whereClause, int maxPerPage = 50, IEnumerable<string>? fields = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where T : class, ITableEntity, new()
	{
		var table = await _tableClient;

		var results = table.QueryAsync(whereClause, maxPerPage, fields, cancellationToken);
		string? continuationToken = null;
		await foreach (var result in results.AsPages(continuationToken, maxPerPage))
		{
			foreach (var entity in result.Values)
				yield return entity;

			continuationToken = result.ContinuationToken;
		}
	}

	public async IAsyncEnumerable<T> QueryEnumerableAsync<T>(string? filter = null, int maxPerPage = 50, IEnumerable<string>? fields = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where T : class, ITableEntity, new()
	{
		var table = await _tableClient;

		var results = table.QueryAsync<T>(filter, maxPerPage, fields, cancellationToken);
		string? continuationToken = null;
		await foreach (var result in results.AsPages(continuationToken, maxPerPage))
		{
			foreach (var entity in result.Values)
				yield return entity;

			continuationToken = result.ContinuationToken;
		}
	}

	public async Task<ContinuationResponse<T>> QueryAsync<T>(Expression<Func<T, bool>> whereClause, int maxPerPage = 50, IEnumerable<string>? fields = null, string? continuationToken = null, CancellationToken cancellationToken = default)
		where T : class, ITableEntity, new()
	{
		var table = await _tableClient;

		var results = table.QueryAsync(whereClause, maxPerPage, fields, cancellationToken);
		await foreach (var result in results.AsPages(continuationToken, maxPerPage))
		{
			return new()
			{
				ContinuationToken = result.ContinuationToken,
				Results = [.. result.Values]
			};
		}

		return new();
	}

	public async Task<ContinuationResponse<T>> QueryAsync<T>(string? filter = null, int maxPerPage = 50, IEnumerable<string>? fields = null, string? continuationToken = null, CancellationToken cancellationToken = default)
		where T : class, ITableEntity, new()
	{
		var table = await _tableClient;

		var results = table.QueryAsync<T>(filter, maxPerPage, fields, cancellationToken);
		await foreach (var result in results.AsPages(continuationToken, maxPerPage))
		{
			return new()
			{
				ContinuationToken = result.ContinuationToken,
				Results = [.. result.Values]
			};
		}

		return new();
	}

	public Task<T?> GetAsync<T>(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
		where T : class, ITableEntity, new()
		=> GetAsync<T>(partitionKey, rowKey, null, cancellationToken);

	public async Task<T?> GetAsync<T>(string partitionKey, string rowKey, IEnumerable<string>? fields, CancellationToken cancellationToken = default)
		where T : class, ITableEntity, new()
	{
		ArgumentNullException.ThrowIfNull(partitionKey, nameof(partitionKey));
		ArgumentNullException.ThrowIfNull(rowKey, nameof(rowKey));

		var table = await _tableClient;
		try
		{
			var pagedResults = table.QueryAsync<T>(e => e.PartitionKey == partitionKey && e.RowKey == rowKey, maxPerPage: 1, select: fields, cancellationToken);
			// The foreach is because it's a paged result set.
			await foreach (var entity in pagedResults)
				return entity;

			return null;
			//return await table.GetEntityAsync<T>(partitionKey, rowKey, fields, cancellationToken);
		}
		catch (Azure.RequestFailedException ex) when (ex.Status == 404)
		{
			return null;
		}
	}

	public async Task DeleteTableAsync(CancellationToken cancellationToken = default)
	{
		var table = await _tableClient;
		await table.DeleteAsync(cancellationToken);
	}

	public async Task<bool> EntityExistsAsync(string partitionKey, string rowKey, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(partitionKey, nameof(partitionKey));
		ArgumentNullException.ThrowIfNull(rowKey, nameof(rowKey));

		var entity = await GetAsync<TableEntity>(partitionKey, rowKey, SelectedColumnsForEntityExistsQuery, cancellationToken);

		return entity != null;
	}

	async Task<TableClient> InitializeAsync(CancellationToken cancellationToken = default)
	{
		var tableClient = CreateTableServiceClient().GetTableClient(_tableName);
		await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

		return tableClient;
	}

	TableServiceClient CreateTableServiceClient()
	{
		TableClientOptions clientOptions = new();
		if (_configuration.TimeoutInSeconds.HasValue)
			clientOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(_configuration.TimeoutInSeconds.Value);

		return new(_configuration.ConnectionString, clientOptions);
	}
}
