using Microsoft.Extensions.DependencyInjection;

namespace Purview.EventSourcing.Capabilities;

/// <summary>
/// Contract tests asserting every built-in provider registers truthful capabilities through DI,
/// without constructing a store or probing live storage.
/// </summary>
public sealed class ProviderCapabilitiesTests
{
	static EventStoreCapabilities Resolve(Action<IServiceCollection> register)
	{
		ServiceCollection services = new();
		register(services);
		using var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IEventStoreCapabilitiesProvider>().GetCapabilities();
	}

	[Test]
	public async Task InMemoryEventStore_ReportsTruthfulCapabilities()
	{
		var capabilities = Resolve(static services => services.AddInMemoryEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.BestEffort);
		await Assert.That(capabilities.SupportsEventStreams).IsTrue();
		await Assert.That(capabilities.SupportsSnapshots).IsFalse();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.None);
		await Assert.That(capabilities.PreservedMetadata).IsEqualTo(PreservedEventMetadata.All);
		await Assert.That(capabilities.SupportsQueries).IsFalse();
		await Assert.That(capabilities.SupportsIdempotencyMarkers).IsTrue();
		await Assert.That(capabilities.Concurrency).IsEqualTo(ConcurrencyGuarantee.Optimistic);
		await Assert.That(capabilities.SupportsTransactionalOutbox).IsFalse();
		await Assert.That(capabilities.OperationalLimitations).Contains(EventStoreOperationalLimitation.NonPersistent);
	}

	[Test]
	public async Task InMemorySnapshotStore_ReportsTruthfulCapabilities()
	{
		var capabilities = Resolve(static services => services.AddInMemorySnapshotEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.BestEffort);
		await Assert.That(capabilities.SupportsEventStreams).IsTrue();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.SingleVersion);
		await Assert.That(capabilities.SupportsQueries).IsTrue();
		await Assert.That(capabilities.OperationalLimitations).Contains(EventStoreOperationalLimitation.NonPersistent);
	}

	[Test]
	public async Task SqlServerEventStore_ReportsAtomicTransactions()
	{
		var capabilities = Resolve(static services => services.AddSqlServerEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.Atomic);
		await Assert.That(capabilities.SupportsEventStreams).IsTrue();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.Versioned);
		await Assert.That(capabilities.PreservedMetadata).IsEqualTo(PreservedEventMetadata.All);
		await Assert.That(capabilities.SupportsQueries).IsFalse();
		await Assert.That(capabilities.SupportsIdempotencyMarkers).IsTrue();
		await Assert.That(capabilities.Concurrency).IsEqualTo(ConcurrencyGuarantee.Optimistic);
		await Assert.That(capabilities.SupportsTransactionalOutbox).IsTrue();
	}

	[Test]
	public async Task SqlServerSnapshotQueryStore_ReportsQuerySnapshotsWithoutEventStream()
	{
		var capabilities = Resolve(static services => services.AddSqlServerSnapshotQueryableEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.Atomic);
		await Assert.That(capabilities.SupportsEventStreams).IsFalse();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.SingleVersion);
		await Assert.That(capabilities.PreservedMetadata).IsEqualTo(PreservedEventMetadata.None);
		await Assert.That(capabilities.SupportsQueries).IsTrue();
		await Assert.That(capabilities.SupportsIdempotencyMarkers).IsFalse();
	}

	[Test]
	public async Task SqlServerEventAndSnapshotStores_CombineIntoUnion()
	{
		var capabilities = Resolve(static services =>
		{
			services.AddSqlServerEventStore();
			services.AddSqlServerSnapshotQueryableEventStore();
		});

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.Atomic);
		await Assert.That(capabilities.SupportsEventStreams).IsTrue();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.Versioned);
		await Assert.That(capabilities.PreservedMetadata).IsEqualTo(PreservedEventMetadata.All);
		await Assert.That(capabilities.SupportsQueries).IsTrue();
		await Assert.That(capabilities.SupportsIdempotencyMarkers).IsTrue();
	}

	[Test]
	public async Task PostgresEventStore_ReportsAtomicTransactions()
	{
		var capabilities = Resolve(static services => services.AddPostgresEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.Atomic);
		await Assert.That(capabilities.SupportsEventStreams).IsTrue();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.Versioned);
		await Assert.That(capabilities.PreservedMetadata).IsEqualTo(PreservedEventMetadata.All);
		await Assert.That(capabilities.SupportsQueries).IsFalse();
		await Assert.That(capabilities.SupportsIdempotencyMarkers).IsTrue();
		await Assert.That(capabilities.SupportsTransactionalOutbox).IsTrue();
	}

	[Test]
	public async Task PostgresSnapshotQueryStore_ReportsQuerySnapshotsWithoutEventStream()
	{
		var capabilities = Resolve(static services => services.AddPostgresSnapshotQueryableEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.Atomic);
		await Assert.That(capabilities.SupportsEventStreams).IsFalse();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.SingleVersion);
		await Assert.That(capabilities.SupportsQueries).IsTrue();
	}

	[Test]
	public async Task AzureStorageEventStore_ReportsBestEffortTransactions()
	{
		var capabilities = Resolve(static services => services.AddAzureStorageEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.BestEffort);
		await Assert.That(capabilities.SupportsEventStreams).IsTrue();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.Versioned);
		await Assert.That(capabilities.PreservedMetadata).IsEqualTo(PreservedEventMetadata.All);
		await Assert.That(capabilities.SupportsQueries).IsFalse();
		await Assert.That(capabilities.SupportsIdempotencyMarkers).IsTrue();
	}

	[Test]
	public async Task MongoDBEventStore_ReportsBestEffortTransactions()
	{
		var capabilities = Resolve(static services => services.AddMongoDBEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.BestEffort);
		await Assert.That(capabilities.SupportsEventStreams).IsTrue();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.Versioned);
		await Assert.That(capabilities.PreservedMetadata).IsEqualTo(PreservedEventMetadata.All);
		await Assert.That(capabilities.SupportsQueries).IsFalse();
		await Assert.That(capabilities.SupportsIdempotencyMarkers).IsTrue();
	}

	[Test]
	public async Task MongoDBSnapshotQueryStore_ReportsQuerySnapshotsWithoutEventStream()
	{
		var capabilities = Resolve(static services => services.AddMongoDBSnapshotQueryableEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.BestEffort);
		await Assert.That(capabilities.SupportsEventStreams).IsFalse();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.SingleVersion);
		await Assert.That(capabilities.SupportsQueries).IsTrue();
	}

	[Test]
	public async Task CosmosDbSnapshotQueryStore_ReportsSnapshotOnlyCapabilities()
	{
		var capabilities = Resolve(static services => services.AddCosmosDbSnapshotQueryableEventStore());

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.BestEffort);
		await Assert.That(capabilities.SupportsEventStreams).IsFalse();
		await Assert.That(capabilities.SupportsSnapshots).IsTrue();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.SingleVersion);
		await Assert.That(capabilities.SupportsQueries).IsTrue();
		await Assert.That(capabilities.PreservedMetadata).IsEqualTo(PreservedEventMetadata.None);
		await Assert.That(capabilities.OperationalLimitations).Contains(EventStoreOperationalLimitation.NoEventStream);
	}
}
