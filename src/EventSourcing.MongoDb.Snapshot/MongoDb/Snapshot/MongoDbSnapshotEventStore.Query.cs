using System.Linq.Expressions;

namespace Purview.EventSourcing.MongoDB.Snapshot;

partial class MongoDBSnapshotEventStore<T>
{
	public IAsyncEnumerable<T> GetQueryEnumerableAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		=> _mongoDbClient
				.GetQueryEnumerableAsync(whereClause, orderByClause, maxRecordsPerIteration, cancellationToken)
				.SelectAsync(FulfilRequirements);

	public IAsyncEnumerable<T> GetListEnumerableAsync(Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
		=> _mongoDbClient
				.GetListEnumerableAsync(orderByClause, maxRecordsPerIteration, cancellationToken)
				.SelectAsync(FulfilRequirements);

	public async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> whereClause, CancellationToken cancellationToken = default)
	{
		// Two so SingleOrDefault throws if it's greater than 1.
		var query = await QueryAsync(whereClause, null, new ContinuationRequest { MaxRecords = 2 }, cancellationToken);

		var result = query.Results.SingleOrDefault();
		if (result != null)
			FulfilRequirements(result);

		return result;
	}

	public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, CancellationToken cancellationToken = default)
	{
		var query = await QueryAsync(whereClause, orderByClause, new ContinuationRequest { MaxRecords = 1 }, cancellationToken);

		var result = query.Results.FirstOrDefault();
		if (result != null)
			FulfilRequirements(result);

		return result;
	}

	public async Task<ContinuationResponse<T>> QueryAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, ContinuationRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(whereClause, nameof(whereClause));
		ArgumentNullException.ThrowIfNull(request, nameof(request));

		var result = await _mongoDbClient.QueryAsync(whereClause, orderByClause, request, cancellationToken);

		result.Results = result.Results.Select(FulfilRequirements).ToArray();

		return result;
	}

	public async Task<ContinuationResponse<T>> ListAsync(Func<IQueryable<T>, IQueryable<T>>? orderByClause, ContinuationRequest request, CancellationToken cancellationToken = default)
	{
		var results = await _mongoDbClient.ListAsync(orderByClause, request, cancellationToken);

		results.Results = results.Results.Select(FulfilRequirements).ToArray();

		return results;
	}

	public Task<long> CountAsync(Expression<Func<T, bool>>? whereClause, CancellationToken cancellationToken = default)
		=> _mongoDbClient.CountAsync(whereClause, cancellationToken);
}
