namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStoreTests
{
	public static IEnumerable<(Type, int)> HighEventCountTestData()
	{
		const int maximum = StorageClients.Table.AzureTableClient.MaximumBatchSize;

		List<(Type, int)> data = [];
		foreach (var aggregateType in GetAggregateTestTypes())
		{
			data.Add((aggregateType, maximum - 2));
			data.Add((aggregateType, maximum + (maximum / 2)));
			data.Add((aggregateType, maximum * 2));
			data.Add((aggregateType, maximum * 3));
			data.Add((aggregateType, maximum * 4));
			data.Add((aggregateType, maximum * 5));
			data.Add((aggregateType, maximum * 9));
		}

		return data;
	}

	public static IEnumerable<(Type, int)> TooManyEventCountTestData()
	{
		List<(Type, int)> data = [];
		foreach (var aggregateType in GetAggregateTestTypes())
		{
			data.Add((aggregateType, 1_001));
			data.Add((aggregateType, 10_000));
			data.Add((aggregateType, 100_000));
		}

		return data;
	}

	public static IEnumerable<(Type, int, int)> SteppedAggregateCountWithDeletedAggregateIdCountTestData()
	{
		List<(Type, int, int)> data = [];
		foreach (var aggregateType in GetAggregateTestTypes())
		{
			data.Add((aggregateType, 1, 1));
			data.Add((aggregateType, 1, 10));
			data.Add((aggregateType, 5, 5));
			data.Add((aggregateType, 5, 10));
			data.Add((aggregateType, 10, 10));
			data.Add((aggregateType, 10, 20));
			data.Add((aggregateType, 20, 20));
			data.Add((aggregateType, 20, 40));
		}

		return data;
	}

	public static IEnumerable<(Type, int, int)> SteppedEventCountWithOldEventCountTestData()
	{
		List<(Type, int, int)> data = [];
		foreach (var aggregateType in GetAggregateTestTypes())
		{
			data.Add((aggregateType, 1, 1));
			data.Add((aggregateType, 5, 2));
			data.Add((aggregateType, 10, 5));
			data.Add((aggregateType, 20, 20));
		}

		return data;
	}

	public static IEnumerable<(Type, int, int, int?)> RequestedRangeOfEventsTestData()
	{
		List<(Type, int, int, int?)> data = [];
		foreach (var aggregateType in GetAggregateTestTypes())
		{
			data.Add((aggregateType, 5, 1, 5));
			data.Add((aggregateType, 5, 1, null));
			data.Add((aggregateType, 10, 2, 5));
			data.Add((aggregateType, 10, 2, null));
			data.Add((aggregateType, 15, 15, null));
			data.Add((aggregateType, 15, 15, 15));
			// Larger request than actual events exist.
			data.Add((aggregateType, 5, 1, 20));
			data.Add((aggregateType, 5, 1, 20000));
		}

		return data;
	}

	public static IEnumerable<(Type, int, int, int?, int)> RequestedRangeOfEventsWithExpectedEventCountTestData()
	{
		List<(Type, int, int, int?, int)> data = [];
		foreach (var aggregateType in GetAggregateTestTypes())
		{
			data.Add((aggregateType, 5, 1, 5, 5));
			data.Add((aggregateType, 5, 1, null, 5));
			data.Add((aggregateType, 10, 2, 5, 4));
			data.Add((aggregateType, 10, 2, null, 9));
			data.Add((aggregateType, 15, 15, null, 1));
			data.Add((aggregateType, 15, 15, 15, 1));
			// Larger request than actual events exist.
			data.Add((aggregateType, 5, 1, 20, 5));
			data.Add((aggregateType, 5, 1, 20000, 5));
		}

		return data;
	}

	public static IEnumerable<(Type, int)> SteppedCountTestData()
	{
		List<(Type, int)> data = [];
		foreach (var aggregateType in GetAggregateTestTypes())
		{
			data.Add((aggregateType, 1));
			data.Add((aggregateType, 10));
			data.Add((aggregateType, 20));
			data.Add((aggregateType, 50));
		}

		return data;
	}

	public static IEnumerable<(Type, int)> GetSnapshotEventCountTestData()
	{
		List<(Type, int)> data = [];
		foreach (var aggregateType in GetAggregateTestTypes())
		{
			data.Add((aggregateType, 10));
			data.Add((aggregateType, 20));
			data.Add((aggregateType, 50));
			data.Add((aggregateType, 80));
			data.Add((aggregateType, 100));
		}

		return data;
	}

	public static IEnumerable<Type> GetAggregateTestTypes()
	{
		List<Type> data = [];

		data.Add(typeof(Aggregates.Persistence.PersistenceAggregate));

		return data;
	}

	public ITableEventStoreTests CreateTableStoreTests(Type aggregateType)
	{
		var testType = typeof(GenericTableEventStoreTests<>).MakeGenericType(aggregateType);

		return (ITableEventStoreTests)Activator.CreateInstance(testType, args: [fixture])!;
	}
}
