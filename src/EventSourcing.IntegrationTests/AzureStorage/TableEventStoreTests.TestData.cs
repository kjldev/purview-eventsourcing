namespace Purview.EventSourcing.AzureStorage;

partial class TableEventStoreTests
{
	public static TheoryData<Type, int> HighEventCountTestData
	{
		get
		{
			const int maximum = StorageClients.Table.AzureTableClient.MaximumBatchSize;

			TheoryData<Type, int> data = [];
			foreach (var aggregateType in AggregateTestTypes)
			{
				data.Add(aggregateType, maximum - 2);
				data.Add(aggregateType, maximum + (maximum / 2));
				data.Add(aggregateType, maximum * 2);
				data.Add(aggregateType, maximum * 3);
				data.Add(aggregateType, maximum * 4);
				data.Add(aggregateType, maximum * 5);
				data.Add(aggregateType, maximum * 9);
			}

			return data;
		}
	}

	public static TheoryData<Type, int> TooManyEventCountTestData
	{
		get
		{
			TheoryData<Type, int> data = [];
			foreach (var aggregateType in AggregateTestTypes)
			{
				data.Add(aggregateType, 1_001);
				data.Add(aggregateType, 10_000);
				data.Add(aggregateType, 100_000);
			}

			return data;
		}
	}

	public static TheoryData<Type, int, int> SteppedAggregateCountWithDeletedAggregateIdCountTestData
	{
		get
		{
			TheoryData<Type, int, int> data = [];
			foreach (var aggregateType in AggregateTestTypes)
			{
				data.Add(aggregateType, 1, 1);
				data.Add(aggregateType, 1, 10);
				data.Add(aggregateType, 5, 5);
				data.Add(aggregateType, 5, 10);
				data.Add(aggregateType, 10, 10);
				data.Add(aggregateType, 10, 20);
				data.Add(aggregateType, 20, 20);
				data.Add(aggregateType, 20, 40);
			}

			return data;
		}
	}

	public static TheoryData<Type, int, int> SteppedEventCountWithOldEventCountTestData
	{
		get
		{

			TheoryData<Type, int, int> data = [];
			foreach (var aggregateType in AggregateTestTypes)
			{
				data.Add(aggregateType, 1, 1);
				data.Add(aggregateType, 5, 2);
				data.Add(aggregateType, 10, 5);
				data.Add(aggregateType, 20, 20);
			}

			return data;
		}
	}

	public static TheoryData<Type, int, int, int?> RequestedRangeOfEventsTestData
	{
		get
		{
			TheoryData<Type, int, int, int?> data = [];
			foreach (var aggregateType in AggregateTestTypes)
			{
				data.Add(aggregateType, 5, 1, 5);
				data.Add(aggregateType, 5, 1, null);
				data.Add(aggregateType, 10, 2, 5);
				data.Add(aggregateType, 10, 2, null);
				data.Add(aggregateType, 15, 15, null);
				data.Add(aggregateType, 15, 15, 15);
				// Larger request than actual events exist.
				data.Add(aggregateType, 5, 1, 20);
				data.Add(aggregateType, 5, 1, 20000);
			}

			return data;
		}
	}

	public static TheoryData<Type, int, int, int?, int> RequestedRangeOfEventsWithExpectedEventCountTestData
	{
		get
		{
			TheoryData<Type, int, int, int?, int> data = [];
			foreach (var aggregateType in AggregateTestTypes)
			{
				data.Add(aggregateType, 5, 1, 5, 5);
				data.Add(aggregateType, 5, 1, null, 5);
				data.Add(aggregateType, 10, 2, 5, 4);
				data.Add(aggregateType, 10, 2, null, 9);
				data.Add(aggregateType, 15, 15, null, 1);
				data.Add(aggregateType, 15, 15, 15, 1);
				// Larger request than actual events exist.
				data.Add(aggregateType, 5, 1, 20, 5);
				data.Add(aggregateType, 5, 1, 20000, 5);
			}

			return data;
		}
	}

	public static TheoryData<Type, int> SteppedCountTestData
	{
		get
		{
			TheoryData<Type, int> data = [];
			foreach (var aggregateType in AggregateTestTypes)
			{
				data.Add(aggregateType, 1);
				data.Add(aggregateType, 10);
				data.Add(aggregateType, 20);
				data.Add(aggregateType, 50);
			}

			return data;
		}
	}

	public static TheoryData<Type, int> SnapshotEventCountTestData
	{
		get
		{
			TheoryData<Type, int> data = [];
			foreach (var aggregateType in AggregateTestTypes)
			{
				data.Add(aggregateType, 10);
				data.Add(aggregateType, 20);
				data.Add(aggregateType, 50);
				data.Add(aggregateType, 80);
				data.Add(aggregateType, 100);
			}

			return data;
		}
	}

	public static TheoryData<Type> AggregateTestTypes
	{
		get
		{
			TheoryData<Type> data = [];

			data.Add(typeof(Aggregates.Persistence.PersistenceAggregate));

			return data;
		}
	}

	public ITableEventStoreTests CreateTableStoreTests(Type aggregateType)
	{
		var testType = typeof(GenericTableEventStoreTests<>).MakeGenericType(aggregateType);

		return (ITableEventStoreTests)Activator.CreateInstance(testType, args: [fixture])!;
	}
}
