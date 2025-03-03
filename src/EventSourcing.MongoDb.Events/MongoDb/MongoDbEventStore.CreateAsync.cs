using Purview.EventSourcing.Services;

namespace Purview.EventSourcing.MongoDB;

partial class MongoDBEventStore<T>
{
	public async Task<T> CreateAsync(string? aggregateId = null, CancellationToken cancellationToken = default)
	{
		if (string.IsNullOrWhiteSpace(aggregateId))
		{
			if (_aggregateIdFactory != null)
			{
				aggregateId = await _aggregateIdFactory.CreateAsync<T>(cancellationToken);
				if (string.IsNullOrWhiteSpace(aggregateId))
					throw new NullReferenceException($"The {typeof(IAggregateIdFactory).FullName} implementation ({_aggregateIdFactory.GetType().FullName}) generated a null or empty Id.");
			}
			else
				aggregateId = $"{Guid.NewGuid()}".ToLowerSafe();
		}

		var aggregate = new T
		{
			Details = {
				Id = aggregateId
			}
		};

		return FulfilRequirements(aggregate);
	}
}
