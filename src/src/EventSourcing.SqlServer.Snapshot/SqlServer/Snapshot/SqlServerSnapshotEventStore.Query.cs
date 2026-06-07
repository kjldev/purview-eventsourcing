using System.Linq.Expressions;
using LinqKit;

namespace Purview.EventSourcing.SqlServer.Snapshot;

partial class SqlServerSnapshotEventStore<T>
{
    public async IAsyncEnumerable<T> GetQueryEnumerableAsync(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        var request = new ContinuationRequest { MaxRecords = maxRecordsPerIteration };
        ContinuationResponse<T>? response;
        do
        {
            response = await QueryAsync(whereClause, orderByClause, request, cancellationToken);
            foreach (var result in response.Results)
                yield return FulfilRequirements(result);

            request = response;
        } while (response.HasMoreRecords);
    }

    public async IAsyncEnumerable<T> GetListEnumerableAsync(
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default
    )
    {
        var request = new ContinuationRequest { MaxRecords = maxRecordsPerIteration };
        ContinuationResponse<T>? response;
        do
        {
            response = await ListAsync(orderByClause, request, cancellationToken);
            foreach (var result in response.Results)
                yield return FulfilRequirements(result);

            request = response;
        } while (response.HasMoreRecords);
    }

    public async Task<ContinuationResponse<T>> QueryAsync(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        ContinuationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(whereClause, nameof(whereClause));
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var expressionToRun = BuildQueryExpression(whereClause);
        return await ExecuteQueryAsync(expressionToRun, orderByClause, request, cancellationToken);
    }

    public async Task<ContinuationResponse<T>> ListAsync(
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        ContinuationRequest request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request, nameof(request));

        var expressionToRun = BuildQueryExpression();
        return await ExecuteQueryAsync(expressionToRun, orderByClause, request, cancellationToken);
    }

    public async Task<T?> SingleOrDefaultAsync(
        Expression<Func<T, bool>> whereClause,
        CancellationToken cancellationToken = default
    )
    {
        // Leave as 2 as it'll throw when expected.
        var query = await GetSpecificNumberAsync(
            whereClause,
            null,
            2,
            cancellationToken: cancellationToken
        );
        return query.SingleOrDefault();
    }

    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        CancellationToken cancellationToken = default
    )
    {
        var query = await GetSpecificNumberAsync(
            whereClause,
            orderByClause,
            1,
            cancellationToken: cancellationToken
        );
        return query.FirstOrDefault();
    }

    public async Task<long> CountAsync(
        Expression<Func<T, bool>>? whereClause,
        CancellationToken cancellationToken = default
    )
    {
        if (whereClause == null)
        {
            // Fast path: push COUNT(*) down to the database — no payload deserialization needed.
            return await _sqlServerClient.CountByAggregateTypeAsync(
                GetAggregateTypeName(),
                cancellationToken
            );
        }

        // Filtered path: load items and apply predicate in memory.
        var expressionToRun = BuildQueryExpression(whereClause);
        var allItems = await _sqlServerClient.QueryByAggregateTypeAsync<T>(
            GetAggregateTypeName(),
            cancellationToken
        );

        var compiledExpression = expressionToRun.Compile();
        return allItems.Count(compiledExpression);
    }

    async Task<IEnumerable<T>> GetSpecificNumberAsync(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        int maxCount,
        CancellationToken cancellationToken = default
    )
    {
        var query = await QueryAsync(
            whereClause,
            orderByClause,
            request: new ContinuationRequest { MaxRecords = maxCount },
            cancellationToken: cancellationToken
        );

        return query.Results.Select(FulfilRequirements);
    }

    async Task<ContinuationResponse<T>> ExecuteQueryAsync(
        Expression<Func<T, bool>> whereClause,
        Func<IQueryable<T>, IQueryable<T>>? orderByClause,
        ContinuationRequest request,
        CancellationToken cancellationToken
    )
    {
        var aggregateTypeName = GetAggregateTypeName();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _telemetry.SnapshotQueryStart(aggregateTypeName, request.MaxRecords);
        using var activity = _telemetry.SnapshotQuery(aggregateTypeName);

        try
        {
            if (!int.TryParse(request.ContinuationToken, out var skipCount))
                skipCount = 0;

            var allItems = await _sqlServerClient.QueryByAggregateTypeAsync<T>(
                aggregateTypeName,
                cancellationToken
            );

            IQueryable<T> query = allItems.AsQueryable().Where(whereClause);

            if (orderByClause != null)
                query = orderByClause(query);

            var results = query.Skip(skipCount).Take(request.MaxRecords).ToArray();

            results = [.. results.Select(FulfilRequirements)];

            sw.Stop();
            _telemetry.SnapshotQueried(aggregateTypeName);
            _telemetry.QueryCompleted(activity, results.Length);
            _telemetry.SnapshotQueryComplete(
                aggregateTypeName,
                results.Length,
                sw.ElapsedMilliseconds
            );

            return new ContinuationResponse<T>
            {
                Results = results,
                RequestedCount = request.MaxRecords,
                ContinuationToken =
                    results.Length == 0 ? null : $"{skipCount + request.MaxRecords}",
            };
        }
        catch (Exception ex)
        {
            _telemetry.SnapshotQueryFailed(aggregateTypeName, ex);
            throw;
        }
    }

    Expression<Func<T, bool>> BuildQueryExpression(Expression<Func<T, bool>>? whereClause = null)
    {
        var aggregateTypeName = GetAggregateTypeName();
        Expression<Func<T, bool>> defaultClause = m => m.AggregateType == aggregateTypeName;

        if (whereClause == null)
            return PredicateBuilder.New(defaultClause);

        var aggregateClause = PredicateBuilder.Extend(
            defaultClause,
            whereClause,
            PredicateOperator.And
        );
        var expressionToRun = aggregateClause.Expand();

        return expressionToRun;
    }
}
