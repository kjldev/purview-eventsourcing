using System.Linq.Expressions;
using LinqKit;

namespace Purview.EventSourcing.CosmosDb.Snapshot;

partial class CosmosDbSnapshotEventStore<T>
{
	public IAsyncEnumerable<T> GetQueryEnumerableAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
	{
		var expressionToRun = BuildQueryExpression(whereClause);

		return _cosmosDbClient
			.GetQueryEnumerableAsync(whereClause, orderByClause, _partitionKey, maxRecordsPerIteration, cancellationToken)
			.SelectAsync(FulfilRequirements);
	}

	public IAsyncEnumerable<T> GetListEnumerableAsync(Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords, CancellationToken cancellationToken = default)
	{
		return _cosmosDbClient
			.GetListEnumerableAsync(orderByClause, _partitionKey, maxRecordsPerIteration, cancellationToken)
			.SelectAsync(FulfilRequirements);
	}

	public async Task<ContinuationResponse<T>> QueryAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, ContinuationRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(whereClause, nameof(whereClause));
		ArgumentNullException.ThrowIfNull(request, nameof(request));

		var expressionToRun = BuildQueryExpression(whereClause);
		var results = await _cosmosDbClient.QueryAsync(expressionToRun, orderByClause, request, _partitionKey, cancellationToken);

		results.Results = [.. results.Results.Select(FulfilRequirements)];

		return results;
	}

	public async Task<ContinuationResponse<T>> ListAsync(Func<IQueryable<T>, IQueryable<T>>? orderByClause, ContinuationRequest request, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(request, nameof(request));

		var expressionToRun = BuildQueryExpression();
		var results = await _cosmosDbClient.QueryAsync(expressionToRun, orderByClause, request, _partitionKey, cancellationToken);

		results.Results = [.. results.Results.Select(FulfilRequirements)];

		return results;
	}

	public async Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> whereClause, CancellationToken cancellationToken = default)
	{
		// Leave as 2 as it'll throw when expected.
		var query = await GetSpecificNumberAsync(whereClause, null, 2, cancellationToken: cancellationToken);
		return query.SingleOrDefault();
	}

	public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, CancellationToken cancellationToken = default)
	{
		var query = await GetSpecificNumberAsync(whereClause, orderByClause, 1, cancellationToken: cancellationToken);
		return query.FirstOrDefault();
	}

	public Task<long> CountAsync(Expression<Func<T, bool>>? whereClause, CancellationToken cancellationToken = default)
	{
		var expressionToRun = BuildQueryExpression(whereClause);

		return _cosmosDbClient.CountAsync(expressionToRun, _partitionKey, cancellationToken);
	}

	async Task<IEnumerable<T>> GetSpecificNumberAsync(Expression<Func<T, bool>> whereClause, Func<IQueryable<T>, IQueryable<T>>? orderByClause, int maxCount, CancellationToken cancellationToken = default)
	{
		// We call QueryAsync because we need the query by the AggregateType.
		var query = await QueryAsync(whereClause, orderByClause, request: new ContinuationRequest { MaxRecords = maxCount }, cancellationToken: cancellationToken);

		return query.Results.Select(FulfilRequirements);
	}

	Expression<Func<T, bool>> BuildQueryExpression(Expression<Func<T, bool>>? whereClause = null)
	{
		var aggregateTypeName = GetAggregateTypeName();
		Expression<Func<T, bool>> defaultClause = m => m.AggregateType == aggregateTypeName;

		if (whereClause == null)
			return PredicateBuilder.New(defaultClause);

		var aggregateClause = PredicateBuilder.Extend(defaultClause, whereClause, PredicateOperator.And);
		var expressionToRun = aggregateClause.Expand();

		return expressionToRun;
	}
}
