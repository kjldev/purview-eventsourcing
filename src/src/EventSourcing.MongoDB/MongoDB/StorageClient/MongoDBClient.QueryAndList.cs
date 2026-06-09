using System.Linq.Expressions;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace Purview.EventSourcing.MongoDB.StorageClient;

partial class MongoDBClient
{
	public async Task<ContinuationResponse<T>> QueryAsync<T>(
		Expression<Func<T, bool>> whereClause,
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		ContinuationRequest request,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(request, nameof(request));

		var collection = GetCollection<T>();
		if (!int.TryParse(request.ContinuationToken, out var skipCount))
			skipCount = 0;

		var whereQuery = collection.AsQueryable().Where(whereClause);
		if (orderByClause != null)
			whereQuery = orderByClause(whereQuery);

		var results = whereQuery.Skip(skipCount).Take(request.MaxRecords);
		var itemResults = (await results.ToListAsync(cancellationToken)).ToArray();
		var response = new ContinuationResponse<T>
		{
			Results = itemResults,
			RequestedCount = request.MaxRecords,
			ContinuationToken = itemResults.Length == 0 ? null : $"{skipCount + request.MaxRecords}",
		};

		return response;
	}

	public async Task<ContinuationResponse<T>> ListAsync<T>(
		Func<IQueryable<T>, IQueryable<T>>? orderByClause,
		ContinuationRequest request,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		ArgumentNullException.ThrowIfNull(request, nameof(request));

		var collection = GetCollection<T>();
		if (!int.TryParse(request.ContinuationToken, out var skipCount))
			skipCount = 0;

		var listQuery = collection.AsQueryable();
		if (orderByClause != null)
			listQuery = orderByClause(listQuery);

		var results = listQuery.Skip(skipCount).Take(request.MaxRecords);
		var itemResults = (await results.ToListAsync(cancellationToken)).ToArray();
		var response = new ContinuationResponse<T>
		{
			Results = itemResults,
			RequestedCount = request.MaxRecords,
			ContinuationToken = itemResults.Length == 0 ? null : $"{skipCount + request.MaxRecords}",
		};

		return response;
	}

	public Task<long> CountAsync<T>(
		Expression<Func<T, bool>>? whereClause,
		CancellationToken cancellationToken = default
	)
		where T : class
	{
		var queryable = GetCollection<T>().AsQueryable();
		return whereClause == null
			? queryable.LongCountAsync(cancellationToken: cancellationToken)
			: queryable.LongCountAsync(whereClause, cancellationToken: cancellationToken);
	}
}
