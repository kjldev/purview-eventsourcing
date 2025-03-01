using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Purview.EventSourcing.MongoDB.StorageClients;

namespace Purview.EventSourcing.MongoDB;

[EditorBrowsable(EditorBrowsableState.Never)]
static class MongoDBClientExtensions
{
	#region QueryEnumerableAsync

	public static async IAsyncEnumerable<T> QueryEnumerableAsync<T>(this MongoDBClient mongoDbClient, Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where T : class
	{
		var request = new ContinuationRequest { MaxRecords = maxRecordsPerOperation };
		do
		{
			var response = await mongoDbClient.QueryAsync(whereClause, orderByClause, request, cancellationToken);
			foreach (var result in response.Results)
				yield return result;

			request = response.ToRequest();
		}
		while (request.ContinuationToken != null);
	}

	public static IAsyncEnumerable<T> QueryEnumerableAsync<T>(this MongoDBClient mongoDbClient, Expression<Func<T, bool>> whereClause, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class
		=> mongoDbClient.QueryEnumerableAsync(whereClause, null, maxRecordsPerOperation, cancellationToken);

	public static IAsyncEnumerable<T> QueryEnumerableAsync<T, TOrderBy>(this MongoDBClient mongoDbClient, Expression<Func<T, bool>> whereClause, Expression<Func<T, TOrderBy>> orderByAscending, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class
		=> mongoDbClient.QueryEnumerableAsync(whereClause, m => m.OrderBy(orderByAscending), maxRecordsPerOperation, cancellationToken);

	#endregion QueryEnumerableAsync

	#region ListEnumerableAsync

	public static async IAsyncEnumerable<T> ListEnumerableAsync<T>(this MongoDBClient mongoDbClient, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, [EnumeratorCancellation] CancellationToken cancellationToken = default)
		where T : class
	{
		ContinuationRequest request = new() { MaxRecords = maxRecordsPerOperation };
		do
		{
			var response = await mongoDbClient.ListAsync(orderByClause, request, cancellationToken);
			foreach (var result in response.Results)
				yield return result;

			request = response.ToRequest();
		}
		while (request.ContinuationToken != null);
	}

	public static IAsyncEnumerable<T> ListEnumerableAsync<T>(this MongoDBClient mongoDbClient, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class
		=> mongoDbClient.ListEnumerableAsync<T>(null, maxRecordsPerOperation, cancellationToken);

	public static IAsyncEnumerable<T> ListEnumerableAsync<T, TOrderBy>(this MongoDBClient mongoDbClient, Expression<Func<T, TOrderBy>> orderByAscending, int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class
		=> mongoDbClient.ListEnumerableAsync<T>(m => m.OrderBy(orderByAscending), maxRecordsPerOperation, cancellationToken);

	#endregion ListEnumerableAsync

	#region QueryAsync

	public static Task<ContinuationResponse<T>> QueryAsync<T>(this MongoDBClient client, Expression<Func<T, bool>> whereClause, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class
		=> client.QueryAsync(whereClause, null, maxRecords, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>(this MongoDBClient client, Expression<Func<T, bool>> whereClause, ContinuationRequest request, CancellationToken cancellationToken = default)
		where T : class
		=> client.QueryAsync(whereClause, null, request, cancellationToken);

	public static Task<ContinuationResponse<T>> QueryAsync<T>(this MongoDBClient client, Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class
		=> client.QueryAsync(whereClause, orderByClause, new ContinuationRequest { MaxRecords = maxRecords }, cancellationToken);

	#endregion QueryAsync

	#region ListAsync

	public static Task<ContinuationResponse<T>> ListAsync<T>(this MongoDBClient client, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class
		=> client.ListAsync<T>(null, maxRecords, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>(this MongoDBClient client, ContinuationRequest request, CancellationToken cancellationToken = default)
		where T : class
		=> client.ListAsync<T>(null, request, cancellationToken);

	public static Task<ContinuationResponse<T>> ListAsync<T>(this MongoDBClient client, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecords = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		where T : class
		=> client.ListAsync(orderByClause, new ContinuationRequest { MaxRecords = maxRecords }, cancellationToken);

	#endregion ListAsync

	public static Task<long> CountAsync<T>(this MongoDBClient client, CancellationToken cancellationToken = default)
		where T : class
		=> client.CountAsync<T>(null, cancellationToken);
}
