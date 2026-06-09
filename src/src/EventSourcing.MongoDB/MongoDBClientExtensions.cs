using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Purview.EventSourcing.MongoDB.StorageClient;

namespace Purview.EventSourcing;

[EditorBrowsable(EditorBrowsableState.Never)]
static class MongoDBClientExtensions
{
	extension(MongoDBClient client)
	{
		#region GetQueryEnumerableAsync

		public async IAsyncEnumerable<T> GetQueryEnumerableAsync<T>(
			Expression<Func<T, bool>> whereClause,
			Func<IQueryable<T>, IQueryable<T>>? orderByClause,
			int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
			[EnumeratorCancellation] CancellationToken cancellationToken = default
		)
			where T : class
		{
			var request = new ContinuationRequest { MaxRecords = maxRecordsPerOperation };
			do
			{
				var response = await client.QueryAsync(whereClause, orderByClause, request, cancellationToken);
				foreach (var result in response.Results)
					yield return result;

				request = response.ToRequest();
			} while (request.ContinuationToken != null);
		}

		public IAsyncEnumerable<T> GetQueryEnumerableAsync<T>(
			Expression<Func<T, bool>> whereClause,
			int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
			CancellationToken cancellationToken = default
		)
			where T : class =>
			client.GetQueryEnumerableAsync(whereClause, null, maxRecordsPerOperation, cancellationToken);

		public IAsyncEnumerable<T> GetQueryEnumerableAsync<T, TOrderBy>(
			Expression<Func<T, bool>> whereClause,
			Expression<Func<T, TOrderBy>> orderByAscending,
			int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
			CancellationToken cancellationToken = default
		)
			where T : class =>
			client.GetQueryEnumerableAsync(
				whereClause,
				m => m.OrderBy(orderByAscending),
				maxRecordsPerOperation,
				cancellationToken
			);

		#endregion GetQueryEnumerableAsync

		#region GetListEnumerableAsync

		public async IAsyncEnumerable<T> GetListEnumerableAsync<T>(
			Func<IQueryable<T>, IQueryable<T>>? orderByClause,
			int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
			[EnumeratorCancellation] CancellationToken cancellationToken = default
		)
			where T : class
		{
			var request = new ContinuationRequest { MaxRecords = maxRecordsPerOperation };
			do
			{
				var response = await client.ListAsync(orderByClause, request, cancellationToken);
				foreach (var result in response.Results)
					yield return result;

				request = response.ToRequest();
			} while (request.ContinuationToken != null);
		}

		public IAsyncEnumerable<T> GetListEnumerableAsync<T>(
			int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
			CancellationToken cancellationToken = default
		)
			where T : class => client.GetListEnumerableAsync<T>(null, maxRecordsPerOperation, cancellationToken);

		public IAsyncEnumerable<T> GetListEnumerableAsync<T, TOrderBy>(
			Expression<Func<T, TOrderBy>> orderByAscending,
			int maxRecordsPerOperation = ContinuationRequest.DefaultMaxRecords,
			CancellationToken cancellationToken = default
		)
			where T : class =>
			client.GetListEnumerableAsync<T>(
				m => m.OrderBy(orderByAscending),
				maxRecordsPerOperation,
				cancellationToken
			);

		#endregion GetListEnumerableAsync

		#region QueryAsync

		public Task<ContinuationResponse<T>> QueryAsync<T>(
			Expression<Func<T, bool>> whereClause,
			int maxRecords = ContinuationRequest.DefaultMaxRecords,
			CancellationToken cancellationToken = default
		)
			where T : class => client.QueryAsync(whereClause, null, maxRecords, cancellationToken);

		public Task<ContinuationResponse<T>> QueryAsync<T>(
			Expression<Func<T, bool>> whereClause,
			ContinuationRequest request,
			CancellationToken cancellationToken = default
		)
			where T : class => client.QueryAsync(whereClause, null, request, cancellationToken);

		public Task<ContinuationResponse<T>> QueryAsync<T>(
			Expression<Func<T, bool>> whereClause,
			Func<IQueryable<T>, IQueryable<T>>? orderByClause,
			int maxRecords = ContinuationRequest.DefaultMaxRecords,
			CancellationToken cancellationToken = default
		)
			where T : class =>
			client.QueryAsync(
				whereClause,
				orderByClause,
				new ContinuationRequest { MaxRecords = maxRecords },
				cancellationToken
			);

		#endregion QueryAsync

		#region ListAsync

		public Task<ContinuationResponse<T>> ListAsync<T>(
			int maxRecords = ContinuationRequest.DefaultMaxRecords,
			CancellationToken cancellationToken = default
		)
			where T : class => client.ListAsync<T>(null, maxRecords, cancellationToken);

		public Task<ContinuationResponse<T>> ListAsync<T>(
			ContinuationRequest request,
			CancellationToken cancellationToken = default
		)
			where T : class => client.ListAsync<T>(null, request, cancellationToken);

		public Task<ContinuationResponse<T>> ListAsync<T>(
			Func<IQueryable<T>, IQueryable<T>>? orderByClause,
			int maxRecords = ContinuationRequest.DefaultMaxRecords,
			CancellationToken cancellationToken = default
		)
			where T : class =>
			client.ListAsync(orderByClause, new ContinuationRequest { MaxRecords = maxRecords }, cancellationToken);

		#endregion ListAsync

		public Task<long> CountAsync<T>(CancellationToken cancellationToken = default)
			where T : class => client.CountAsync<T>(null, cancellationToken);
	}
}
