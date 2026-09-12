using System.Linq.Expressions;

namespace Purview.EventSourcing.Postgres.Snapshot;

partial class PostgresSnapshotEventStore<T>
{
	///<inheritdoc/>
	public async IAsyncEnumerable<T> GetQueryEnumerableAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		ContinuationRequest request = new() { MaxRecords = maxRecordsPerIteration };
		ContinuationResponse<T>? response;
		do
		{
			response = await QueryAsync(whereClause, orderByClause, request, cancellationToken);
			foreach (var result in response.Results)
				yield return FulfilRequirements(result);

			request = response;
		} while (response.HasMoreRecords);
	}

	///<inheritdoc/>
	public async IAsyncEnumerable<T> GetListEnumerableAsync(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		int maxRecordsPerIteration = ContinuationRequest.DefaultMaxRecords,
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default
	)
	{
		ContinuationRequest request = new() { MaxRecords = maxRecordsPerIteration };
		ContinuationResponse<T>? response;
		do
		{
			response = await ListAsync(orderByClause, request, cancellationToken);
			foreach (var result in response.Results)
				yield return FulfilRequirements(result);

			request = response;
		} while (response.HasMoreRecords);
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
		return await ExecuteQueryAsync(whereClause, orderByClause, request, cancellationToken);
	}

	///<inheritdoc/>
	public async Task<ContinuationResponse<T>> ListAsync(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		ContinuationRequest request,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentNullException.ThrowIfNull(request, nameof(request));

		return await ExecuteQueryAsync(null, orderByClause, request, cancellationToken);
	}

	///<inheritdoc/>
	public async Task<T?> SingleOrDefaultAsync(
		Expression<Func<T, bool>> whereClause,
		CancellationToken cancellationToken = default
	)
	{
		// Leave as 2 as it'll throw when expected.
		var query = await GetSpecificNumberAsync(whereClause, null, 2, cancellationToken: cancellationToken);
		return query.SingleOrDefault();
	}

	///<inheritdoc/>
	public async Task<T?> FirstOrDefaultAsync(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		CancellationToken cancellationToken = default
	)
	{
		var query = await GetSpecificNumberAsync(whereClause, orderByClause, 1, cancellationToken: cancellationToken);
		return query.FirstOrDefault();
	}

	///<inheritdoc/>
	public async Task<long> CountAsync(
		Expression<Func<T, bool>>? whereClause,
		CancellationToken cancellationToken = default
	)
	{
		return await _sqlServerClient.CountByAggregateTypeAsync(GetAggregateTypeName(), whereClause, cancellationToken);
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

	///<inheritdoc/>
	public async Task<ContinuationResponse<T>> WherePayloadContainsAsync(
		string jsonFragment,
		ContinuationRequest request,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(jsonFragment);
		ArgumentNullException.ThrowIfNull(request);

		if (!int.TryParse(request.ContinuationToken, out var skipCount))
			skipCount = 0;

		var aggregateTypeName = GetAggregateTypeName();
		var results = await _sqlServerClient.QueryByAggregateTypeJsonContainsAsync<T>(
			aggregateTypeName,
			jsonFragment,
			skipCount,
			request.MaxRecords,
			cancellationToken
		);
		var fulfilledResults = results.Select(FulfilRequirements).ToArray();

		return new ContinuationResponse<T>
		{
			Results = fulfilledResults,
			RequestedCount = request.MaxRecords,
			ContinuationToken = fulfilledResults.Length == 0 ? null : $"{skipCount + request.MaxRecords}",
			TotalCount = null,
		};
	}

	///<inheritdoc/>
	public async Task<ContinuationResponse<T>> WherePayloadHasKeyAsync(
		string key,
		ContinuationRequest request,
		CancellationToken cancellationToken = default
	)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(key);
		ArgumentNullException.ThrowIfNull(request);

		if (!int.TryParse(request.ContinuationToken, out var skipCount))
			skipCount = 0;

		var aggregateTypeName = GetAggregateTypeName();
		var results = await _sqlServerClient.QueryByAggregateTypeJsonExistsAsync<T>(
			aggregateTypeName,
			key,
			skipCount,
			request.MaxRecords,
			cancellationToken
		);
		var fulfilledResults = results.Select(FulfilRequirements).ToArray();

		return new ContinuationResponse<T>
		{
			Results = fulfilledResults,
			RequestedCount = request.MaxRecords,
			ContinuationToken = fulfilledResults.Length == 0 ? null : $"{skipCount + request.MaxRecords}",
			TotalCount = null,
		};
	}

	async Task<ContinuationResponse<T>> ExecuteQueryAsync(
		Expression<Func<T, bool>>? whereClause,
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

			long? totalCount = request.IncludeTotalCount
				? await _sqlServerClient.CountByAggregateTypeAsync<T>(aggregateTypeName, whereClause, cancellationToken)
				: null;

			var results = await _sqlServerClient.QueryByAggregateTypeAsync(
				aggregateTypeName,
				whereClause,
				orderByClause,
				skipCount,
				request.MaxRecords,
				cancellationToken
			);

			var fulfilledResults = results.Select(FulfilRequirements).ToArray();

			sw.Stop();
			_telemetry.QueryCompleted(activity, aggregateTypeName, fulfilledResults.Length, sw.ElapsedMilliseconds);

			return new ContinuationResponse<T>
			{
				Results = fulfilledResults,
				RequestedCount = request.MaxRecords,
				ContinuationToken = fulfilledResults.Length == 0 ? null : $"{skipCount + request.MaxRecords}",
				TotalCount = totalCount,
			};
		}
		catch (Exception ex)
		{
			_telemetry.SnapshotQueryFailed(aggregateTypeName, ex);
			throw;
		}
	}
}
