using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.InMemory.Snapshots;

public sealed class InMemorySnapshotStore<T>(
	ChangeFeed.IAggregateChangeFeedNotifier<T> aggregateChangeNotifier,
	IAggregateRequirementsManager aggregateRequirementsManager,
	FluentValidation.IValidator<T>? validator = null,
	IAggregateIdFactory? aggregateIdFactory = null
)
	: Events.InMemoryEventStore<T>(
		aggregateChangeNotifier,
		aggregateRequirementsManager,
		validator,
		aggregateIdFactory
	),
		IInMemorySnapshotStore<T>
	where T : class, IAggregate, new()
{
	public Task<long> CountAsync(
		Expression<Func<T, bool>>? whereClause,
		CancellationToken cancellationToken = default
	) => Task.FromResult(whereClause is null ? Aggregates.LongCount() : Aggregates.LongCount(whereClause.Compile()));

	public Task<T?> FirstOrDefaultAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		CancellationToken cancellationToken = default
	)
	{
		var results = Aggregates;
		if (orderByClause != null)
			results = orderByClause(results.AsQueryable()).AsEnumerable();

		if (whereClause != null)
			results = results.Where(whereClause.Compile());

		return Task.FromResult(results.FirstOrDefault());
	}

	public IAsyncEnumerable<T> GetListEnumerableAsync(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordsPerIteration = 20,
		CancellationToken cancellationToken = default
	)
	{
		var results = Aggregates;
		if (orderByClause != null)
			results = orderByClause(results.AsQueryable()).AsEnumerable();

		return results.ToAsyncEnumerable();
	}

	public IAsyncEnumerable<T> GetQueryEnumerableAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordsPerIteration = 20,
		CancellationToken cancellationToken = default
	)
	{
		var results = Aggregates;
		if (orderByClause != null)
			results = orderByClause(results.AsQueryable()).AsEnumerable();

		if (whereClause != null)
			results = results.Where(whereClause.Compile());

		return results.ToAsyncEnumerable();
	}

	public async Task<ContinuationResponse<T>> ListAsync(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		[NotNull] ContinuationRequest request,
		CancellationToken cancellationToken = default
	)
	{
		var results = Aggregates;
		if (orderByClause != null)
			results = orderByClause(results.AsQueryable()).AsEnumerable();

		var totalCount = request.IncludeTotalCount ? await CountAsync(null, cancellationToken) : -1L;
		var skip = 0;
		if (int.TryParse(request.ContinuationToken, out var continuationToken))
			skip = continuationToken;

		T[] resultsArray = [.. results.Skip(skip).Take(request.MaxRecords)];
		return new ContinuationResponse<T>()
		{
			Results = resultsArray,
			TotalCount = totalCount,
			RequestedCount = request.MaxRecords,
			ContinuationToken = (skip + resultsArray.Length).ToString(CultureInfo.InvariantCulture),
		};
	}

	public async Task<ContinuationResponse<T>> QueryAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		[NotNull] ContinuationRequest request,
		CancellationToken cancellationToken = default
	)
	{
		var results = Aggregates;
		if (orderByClause != null)
			results = orderByClause(results.AsQueryable()).AsEnumerable();
		if (whereClause != null)
			results = results.Where(whereClause.Compile());

		var totalCount = request.IncludeTotalCount ? await CountAsync(null, cancellationToken) : -1L;
		var skip = 0;
		if (int.TryParse(request.ContinuationToken, out var continuationToken))
			skip = continuationToken;

		T[] resultsArray = [.. results.Skip(skip).Take(request.MaxRecords)];
		return new ContinuationResponse<T>()
		{
			Results = resultsArray,
			TotalCount = totalCount,
			RequestedCount = request.MaxRecords,
			ContinuationToken = (skip + resultsArray.Length).ToString(CultureInfo.InvariantCulture),
		};
	}

	public Task<T?> SingleOrDefaultAsync(
		Expression<Func<T, bool>> whereClause,
		CancellationToken cancellationToken = default
	)
	{
		var results = Aggregates;
		if (whereClause != null)
			results = results.Where(whereClause.Compile());

		return Task.FromResult(results.SingleOrDefault());
	}
}
