using System.Linq.Expressions;

namespace Purview.EventSourcing.MongoDB.Snapshots;

partial class MongoDBSnapshotEventStore<T>
{
	///<inheritdoc/>
	public IAsyncEnumerable<T> GetQueryEnumerableAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	) =>
		_mongoDBClient
			.GetQueryEnumerableAsync(whereClause, orderByClause, maxRecordsPerIteration, cancellationToken)
			.SelectAsync(FulfilRequirements);

	///<inheritdoc/>
	public IAsyncEnumerable<T> GetListEnumerableAsync(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
		CancellationToken cancellationToken = default
	) =>
		_mongoDBClient
			.GetListEnumerableAsync(orderByClause, maxRecordsPerIteration, cancellationToken)
			.SelectAsync(FulfilRequirements);

	///<inheritdoc/>
	public async Task<T?> SingleOrDefaultAsync(
		Expression<Func<T, bool>> whereClause,
		CancellationToken cancellationToken = default
	)
	{
		// Two so SingleOrDefault throws if it's greater than 1.
		var query = await QueryAsync(whereClause, null, new ContinuationRequest { MaxRecords = 2 }, cancellationToken);

		var result = query.Results.SingleOrDefault();
		if (result != null)
			FulfilRequirements(result);

		return result;
	}

	///<inheritdoc/>
	public async Task<T?> FirstOrDefaultAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		CancellationToken cancellationToken = default
	)
	{
		var query = await QueryAsync(
			whereClause,
			orderByClause,
			new ContinuationRequest { MaxRecords = 1 },
			cancellationToken
		);

		var result = query.Results.FirstOrDefault();
		if (result != null)
			FulfilRequirements(result);

		return result;
	}

	///<inheritdoc/>
	public async Task<ContinuationResponse<T>> QueryAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		ContinuationRequest request,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(whereClause, nameof(whereClause));
		ArgumentNullException.ThrowIfNull(request, nameof(request));

		var result = await _mongoDBClient.QueryAsync(whereClause, orderByClause, request, cancellationToken);

		result.Results = [.. result.Results.Select(FulfilRequirements)];
		if (request.IncludeTotalCount)
			result.TotalCount = await _mongoDBClient.CountAsync(whereClause, cancellationToken);

		return result;
	}

	///<inheritdoc/>
	public async Task<ContinuationResponse<T>> ListAsync(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		ContinuationRequest request,
		CancellationToken cancellationToken = default
	)
	{
		var results = await _mongoDBClient.ListAsync(orderByClause, request, cancellationToken);

		results.Results = [.. results.Results.Select(FulfilRequirements)];
		if (request.IncludeTotalCount)
			results.TotalCount = await _mongoDBClient.CountAsync<T>(cancellationToken);

		return results;
	}

	///<inheritdoc/>
	public Task<long> CountAsync(
		Expression<Func<T, bool>>? whereClause,
		CancellationToken cancellationToken = default
	) => _mongoDBClient.CountAsync(whereClause, cancellationToken);
}
