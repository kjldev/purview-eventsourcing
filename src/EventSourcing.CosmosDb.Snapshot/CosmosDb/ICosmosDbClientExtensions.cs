using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;

namespace Purview.EventSourcing.CosmosDb;

[EditorBrowsable(EditorBrowsableState.Never)]
static class CosmosDbClientExtensions
{
	#region GetQueryEnumerableAsync

	public static async IAsyncEnumerable<T> GetQueryEnumerableAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, PartitionKey partitionKey, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where T : class
	{
		ContinuationRequest request = new() { MaxRecords = maxRecordsPerOperation };
		do
		{
			var response = await cosmosDbClient.QueryAsync(whereClause, orderByClause, request, partitionKey, cancellationToken);
			foreach (var result in response.Results)
				yield return result;

			request = response.ToRequest();
		}
		while (request.ContinuationToken != null);
	}

	public static IAsyncEnumerable<T> GetQueryEnumerableAsync<T>(this CosmosDbClient cosmosDbClient, Expression<Func<T, bool>> whereClause, PartitionKey partitionKey, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.GetQueryEnumerableAsync(whereClause, null, partitionKey, maxRecordsPerOperation, cancellationToken);

	public static IAsyncEnumerable<T> GetQueryEnumerableAsync<T, TOrderBy>(this CosmosDbClient cosmosDbClient, Expression<Func<T, bool>> whereClause, Expression<Func<T, TOrderBy>> orderByAscending, PartitionKey partitionKey, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.GetQueryEnumerableAsync(whereClause, m => m.OrderBy(orderByAscending), partitionKey, maxRecordsPerOperation, cancellationToken);

	#endregion GetQueryEnumerableAsync

	#region GetListEnumerableAsync

	public static async IAsyncEnumerable<T> GetListEnumerableAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, Func<IQueryable<T>, IQueryable<T>>? orderByClause, PartitionKey partitionKey, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where T : class
	{
		var request = new ContinuationRequest { MaxRecords = maxRecordsPerOperation };
		do
		{
			var response = await cosmosDbClient.ListAsync(orderByClause, request, partitionKey, cancellationToken);
			foreach (var result in response.Results)
				yield return result;

			request = response.ToRequest();
		}
		while (request.ContinuationToken != null);
	}

	public static IAsyncEnumerable<T> GetListEnumerableAsync<T>(this CosmosDbClient cosmosDbClient, PartitionKey partitionKey, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.GetListEnumerableAsync<T>(null, partitionKey, maxRecordsPerOperation, cancellationToken);

	public static IAsyncEnumerable<T> GetListEnumerableAsync<T, TOrderBy>(this CosmosDbClient cosmosDbClient, Expression<Func<T, TOrderBy>> orderByAscending, PartitionKey partitionKey, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.GetListEnumerableAsync<T>(m => m.OrderBy(orderByAscending), partitionKey, maxRecordsPerOperation, cancellationToken);

	#endregion GetListEnumerableAsync

	#region QueryAsync

	public static Task<ContinuationResponse<T>> QueryAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, PartitionKey partitionKey, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.QueryAsync(whereClause, orderByClause, new ContinuationRequest { MaxRecords = maxRecords, ContinuationToken = null }, partitionKey, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, Expression<Func<T, bool>> whereClause, PartitionKey partitionKey, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.QueryAsync(whereClause, null, partitionKey, maxRecords, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, Expression<Func<T, bool>> whereClause, ContinuationRequest request, PartitionKey partitionKey, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.QueryAsync(whereClause, null, request, partitionKey, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T, TOrderBy>([NotNull] this CosmosDbClient cosmosDbClient, Expression<Func<T, bool>> whereClause, Expression<Func<T, TOrderBy>> orderByAscending, PartitionKey partitionKey, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.QueryAsync(whereClause, m => m.OrderBy(orderByAscending), partitionKey, maxRecords, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T, TOrderBy>([NotNull] this CosmosDbClient cosmosDbClient, Expression<Func<T, bool>> whereClause, Expression<Func<T, TOrderBy>> orderByAscending, ContinuationRequest request, PartitionKey partitionKey, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.QueryAsync(whereClause, m => m.OrderBy(orderByAscending), request, partitionKey, cancellationToken);

	#endregion QueryAsync

	#region ListAsync

	public static Task<ContinuationResponse<T>> ListAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, Func<IQueryable<T>, IQueryable<T>>? orderByClause, PartitionKey partitionKey, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.ListAsync(orderByClause, new ContinuationRequest
		{
			MaxRecords = maxRecords,
			ContinuationToken = null
		}, partitionKey, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, PartitionKey partitionKey, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.ListAsync<T>(null, partitionKey, maxRecords, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, ContinuationRequest request, PartitionKey partitionKey, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.ListAsync<T>(null, request, partitionKey, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T, TOrderBy>([NotNull] this CosmosDbClient cosmosDbClient, Expression<Func<T, TOrderBy>> orderByAscending, PartitionKey partitionKey, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.ListAsync<T>(m => m.OrderBy(orderByAscending), partitionKey, maxRecords, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T, TOrderBy>([NotNull] this CosmosDbClient cosmosDbClient, Expression<Func<T, TOrderBy>> orderByAscending, ContinuationRequest request, PartitionKey partitionKey, CancellationToken cancellationToken = default)
		where T : class => cosmosDbClient.ListAsync<T>(m => m.OrderBy(orderByAscending), request, partitionKey, cancellationToken);

	#endregion ListAsync

	#region CRUD

	public static async Task<IEnumerable<ResponseMessage>> DeleteAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, IEnumerable<T> documents, PartitionKey partitionKey, CancellationToken cancellationToken = default)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(documents, nameof(documents));

		return await cosmosDbClient.DeleteAsync(documents.ToArray(), partitionKey, cancellationToken);
	}

	public static async Task<IEnumerable<ResponseMessage>> InsertAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, IEnumerable<T> documents, PartitionKey partitionKey, CancellationToken cancellationToken = default)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(documents, nameof(documents));

		return await cosmosDbClient.InsertAsync(documents.ToArray(), partitionKey, cancellationToken);
	}

	public static async Task<IEnumerable<ResponseMessage>> UpsertAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, IEnumerable<T> documents, PartitionKey partitionKey, CancellationToken cancellationToken = default)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(documents, nameof(documents));

		return await cosmosDbClient.UpsertAsync(documents.ToArray(), partitionKey, cancellationToken);
	}

	public static async Task<IEnumerable<ResponseMessage>> ReplaceAsync<T>([NotNull] this CosmosDbClient cosmosDbClient, IEnumerable<T> documents, PartitionKey partitionKey, CancellationToken cancellationToken = default)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(documents, nameof(documents));

		return await cosmosDbClient.ReplaceAsync(documents.ToArray(), partitionKey, cancellationToken);
	}

	#endregion CRUD
}
