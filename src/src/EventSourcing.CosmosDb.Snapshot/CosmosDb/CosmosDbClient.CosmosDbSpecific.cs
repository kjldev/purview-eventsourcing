using System.Linq.Expressions;
using LinqKit;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Purview.EventSourcing.CosmosDb;

partial class CosmosDbClient
{
	public async Task<ContinuationResponse<T>> ListAsync<T>(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		ContinuationRequest request,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		QueryRequestOptions requestOptions = new() { MaxItemCount = request.MaxRecords, PartitionKey = partitionKey };

		var queryIterator = await FeedQueryAsync(
			orderByClause,
			request.ContinuationToken,
			requestOptions,
			cancellationToken: cancellationToken
		);
		if (queryIterator.HasMoreResults)
		{
			var currentSet = await queryIterator.ReadNextAsync(cancellationToken);

			return new ContinuationResponse<T>
			{
				ContinuationToken = currentSet.ContinuationToken,
				RequestedCount = request.MaxRecords,
				Results = [.. currentSet],
			};
		}

		return new ContinuationResponse<T>
		{
			ContinuationToken = null,
			RequestedCount = request.MaxRecords,
			Results = [],
		};
	}

	public async Task<long> CountAsync<T>(
		Expression<Func<T, bool>>? whereClause,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
	{
		var container = await _container.GetValueAsync(cancellationToken);

		QueryRequestOptions requestOptions = new() { MaxItemCount = 1, PartitionKey = partitionKey };

		var queryable = container.GetItemLinqQueryable<T>(requestOptions: requestOptions).AsQueryable();
		if (whereClause != null)
		{
			queryable = queryable.Where(whereClause);
		}

		return await queryable.CountAsync(cancellationToken);
	}

	public async Task<ContinuationResponse<T>> QueryAsync<T>(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		ContinuationRequest request,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		QueryRequestOptions requestOptions = new() { MaxItemCount = request.MaxRecords, PartitionKey = partitionKey };

		var queryIterator = await FeedQueryAsync(
			whereClause,
			orderByClause,
			request.ContinuationToken,
			requestOptions,
			cancellationToken: cancellationToken
		);
		if (queryIterator.HasMoreResults)
		{
			var currentSet = await queryIterator.ReadNextAsync(cancellationToken);
			return new()
			{
				ContinuationToken = currentSet.ContinuationToken,
				RequestedCount = request.MaxRecords,
				Results = [.. currentSet],
			};
		}

		return new()
		{
			ContinuationToken = null,
			RequestedCount = request.MaxRecords,
			Results = [],
		};
	}

	async Task<FeedIterator<T>> FeedQueryAsync<T>(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		string? continuationToken = null,
		QueryRequestOptions? requestOptions = null,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		var container = await _container.GetValueAsync(cancellationToken);

		cancellationToken.ThrowIfCancellationRequested();

		var listQuery = container
			.GetItemLinqQueryable<T>(continuationToken: continuationToken, requestOptions: requestOptions)
			.AsQueryable();

		if (orderByClause != null)
			listQuery = orderByClause(listQuery);

		return listQuery.ToFeedIterator();
	}

	async Task<FeedIterator<T>> FeedQueryAsync<T>(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		string? continuationToken = null,
		QueryRequestOptions? requestOptions = null,
		CancellationToken cancellationToken = default
	)
	{
		var container = await _container.GetValueAsync(cancellationToken);
		var c = whereClause.Expand();

		cancellationToken.ThrowIfCancellationRequested();

		var whereQuery = container
			.GetItemLinqQueryable<T>(continuationToken: continuationToken, requestOptions: requestOptions)
			.Where(c);

		if (orderByClause != null)
			whereQuery = orderByClause(whereQuery);

		return whereQuery.ToFeedIterator();
	}

	public async Task<T?> GetAsync<T>(
		string id,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));
		var container = await _container.GetValueAsync(cancellationToken);

		var response = await container.ReadItemStreamAsync(id, partitionKey, cancellationToken: cancellationToken);
		if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
			return null;

		var client = GetOrCreateClient(_cosmosDbOptions);
		return client.ClientOptions.Serializer.FromStream<T>(response.Content);
	}

	public async Task<ResponseMessage> DeleteAsync<T>(
		T document,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(document, nameof(document));

		var responses = await DeleteAsync([document], partitionKey, cancellationToken);

		return responses.Single();
	}

	public Task<IEnumerable<ResponseMessage>> DeleteAsync<T>(
		T[] documents,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(documents, nameof(documents));
		if (documents.Length == 0)
			return Task.FromResult(Enumerable.Empty<ResponseMessage>());

		var ids = documents.Where(m => m != null).Select(CosmosDbUtilities.GetDocumentId).ToArray();
		return DeleteAsync(ids, partitionKey, cancellationToken);
	}

	public async Task<ResponseMessage> DeleteAsync(
		string id,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(id, nameof(id));

		var responses = await DeleteAsync([id], partitionKey, cancellationToken);
		return responses.Single();
	}

	public async Task<IEnumerable<ResponseMessage>> DeleteAsync(
		string[] ids,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(ids, nameof(ids));
		if (ids.Length == 0)
			return [];

		var container = await _container.GetValueAsync(cancellationToken);
		return await Task.WhenAll(
			ids.Where(m => m != null)
				.Select(async id =>
				{
					var response = await container.DeleteItemStreamAsync(
						id,
						partitionKey,
						cancellationToken: cancellationToken
					);

					return response;
				})
		);
	}

	public async Task<ResponseMessage> InsertAsync<T>(
		T document,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(document, nameof(document));

		var responses = await InsertAsync([document], partitionKey, cancellationToken);
		return responses.Single();
	}

	public async Task<IEnumerable<ResponseMessage>> InsertAsync<T>(
		T[] documents,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(documents, nameof(documents));
		if (documents.Length == 0)
			return [];

		var container = await _container.GetValueAsync(cancellationToken);
		return await Task.WhenAll(
			documents.Select(async document =>
			{
				var stream = await CosmosDbUtilities.SerializeDocumentAsync(document, cancellationToken);
				var response = await container.CreateItemStreamAsync(
					stream,
					partitionKey,
					cancellationToken: cancellationToken
				);

				return response;
			})
		);
	}

	public async Task<ResponseMessage> UpsertAsync<T>(
		T document,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(document, nameof(document));

		var responses = await UpsertAsync([document], partitionKey, cancellationToken);
		return responses.Single();
	}

	public async Task<IEnumerable<ResponseMessage>> UpsertAsync<T>(
		T[] documents,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(documents, nameof(documents));
		if (documents.Length == 0)
			return [];

		var container = await _container.GetValueAsync(cancellationToken);
		return await Task.WhenAll(
			documents
				.Where(m => m != null)
				.Select(async document =>
				{
					var stream = await CosmosDbUtilities.SerializeDocumentAsync(document, cancellationToken);
					var response = await container.UpsertItemStreamAsync(
						stream,
						partitionKey,
						cancellationToken: cancellationToken
					);

					return response;
				})
		);
	}

	public async Task<ResponseMessage> ReplaceAsync<T>(
		T document,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(document, nameof(document));

		var responses = await ReplaceAsync([document], partitionKey, cancellationToken);
		return responses.Single();
	}

	public async Task<IEnumerable<ResponseMessage>> ReplaceAsync<T>(
		T[] documents,
		PartitionKey partitionKey,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(documents, nameof(documents));
		if (documents.Length == 0)
			return [];

		var container = await _container.GetValueAsync(cancellationToken);

		return await Task.WhenAll(
			documents
				.Where(m => m != null)
				.Select(async document =>
				{
					var documentId = CosmosDbUtilities.GetDocumentId(document);
					var stream = await CosmosDbUtilities.SerializeDocumentAsync(document, cancellationToken);
					var response = await container.ReplaceItemStreamAsync(
						stream,
						documentId,
						partitionKey,
						cancellationToken: cancellationToken
					);

					return response;
				})
		);
	}

	public async Task DeleteDatabaseAsync(CancellationToken cancellationToken = default)
	{
		await _container.GetValueAsync(cancellationToken);
		await _database.DeleteAsync(cancellationToken: cancellationToken);

		_ = CreatedDatabases.TryRemove(_databaseCreatedKey, out _);
	}

	public async Task DeleteContainerAsync(CancellationToken cancellationToken = default)
	{
		var container = await _container.GetValueAsync(cancellationToken);
		await container.DeleteContainerAsync(cancellationToken: cancellationToken);

		_ = CreatedContainers.TryRemove(_containerCreatedKey, out _);
	}
}
