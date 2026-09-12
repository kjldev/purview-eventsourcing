using Microsoft.Extensions.DependencyInjection;

namespace Purview.EventSourcing.Capabilities;

public sealed class EventStoreCapabilitiesTests
{
	[Test]
	public async Task Default_IsConservative()
	{
		var capabilities = EventStoreCapabilities.Default;

		await Assert.That(capabilities.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.BestEffort);
		await Assert.That(capabilities.SupportsEventStreams).IsFalse();
		await Assert.That(capabilities.SupportsSnapshots).IsFalse();
		await Assert.That(capabilities.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.None);
		await Assert.That(capabilities.PreservedMetadata).IsEqualTo(PreservedEventMetadata.None);
		await Assert.That(capabilities.SupportsQueries).IsFalse();
		await Assert.That(capabilities.SupportsIdempotencyMarkers).IsFalse();
		await Assert.That(capabilities.Concurrency).IsEqualTo(ConcurrencyGuarantee.LastWriterWins);
	}

	[Test]
	public async Task Merge_EmptyParts_ReturnsDefault()
	{
		var merged = EventStoreCapabilities.Merge([]);

		await Assert.That(merged).IsEqualTo(EventStoreCapabilities.Default);
	}

	[Test]
	public async Task Merge_Union_StrongestGuaranteeAndMetadataWin()
	{
		EventStoreCapabilities eventPart = new(
			EventStoreTransactionGuarantee.BestEffort,
			SupportsEventStreams: true,
			SupportsSnapshots: false,
			SnapshotSchemaVersioning: SnapshotSchemaSupport.None,
			PreservedMetadata: PreservedEventMetadata.CorrelationId | PreservedEventMetadata.SchemaVersion,
			SupportsQueries: false,
			SupportsIdempotencyMarkers: true,
			Concurrency: ConcurrencyGuarantee.Optimistic,
			OperationalLimitations: ["limitation-a"]
		);
		EventStoreCapabilities snapshotPart = new(
			EventStoreTransactionGuarantee.Atomic,
			SupportsEventStreams: false,
			SupportsSnapshots: true,
			SnapshotSchemaVersioning: SnapshotSchemaSupport.Versioned,
			PreservedMetadata: PreservedEventMetadata.UserId,
			SupportsQueries: true,
			SupportsIdempotencyMarkers: false,
			Concurrency: ConcurrencyGuarantee.LastWriterWins,
			OperationalLimitations: ["limitation-b"]
		);

		var merged = EventStoreCapabilities.Merge([eventPart, snapshotPart]);

		await Assert.That(merged.TransactionGuarantee).IsEqualTo(EventStoreTransactionGuarantee.Atomic);
		await Assert.That(merged.SupportsEventStreams).IsTrue();
		await Assert.That(merged.SupportsSnapshots).IsTrue();
		await Assert.That(merged.SnapshotSchemaVersioning).IsEqualTo(SnapshotSchemaSupport.Versioned);
		await Assert
			.That(merged.PreservedMetadata)
			.IsEqualTo(
				PreservedEventMetadata.CorrelationId
					| PreservedEventMetadata.SchemaVersion
					| PreservedEventMetadata.UserId
			);
		await Assert.That(merged.SupportsQueries).IsTrue();
		await Assert.That(merged.SupportsIdempotencyMarkers).IsTrue();
		await Assert.That(merged.Concurrency).IsEqualTo(ConcurrencyGuarantee.LastWriterWins);
		await Assert.That(merged.OperationalLimitations).IsEquivalentTo(["limitation-a", "limitation-b"]);
	}

	[Test]
	public async Task AddEventSourcing_WithoutProvider_ReportsDefaultCapabilities()
	{
		ServiceCollection services = new();
		services.AddEventSourcing();

		using var provider = services.BuildServiceProvider();
		var capabilities = provider.GetRequiredService<IEventStoreCapabilitiesProvider>().GetCapabilities();

		await Assert.That(capabilities).IsEqualTo(EventStoreCapabilities.Default);
	}

	[Test]
	public async Task AddEventStoreCapabilities_CustomProvider_ReportsRegisteredCapabilities()
	{
		EventStoreCapabilities custom = new(
			EventStoreTransactionGuarantee.BestEffort,
			SupportsEventStreams: true,
			SupportsSnapshots: false,
			SnapshotSchemaVersioning: SnapshotSchemaSupport.None,
			PreservedMetadata: PreservedEventMetadata.AggregateVersion,
			SupportsQueries: false,
			SupportsIdempotencyMarkers: false,
			Concurrency: ConcurrencyGuarantee.LastWriterWins,
			OperationalLimitations: []
		);

		ServiceCollection services = new();
		services.AddEventSourcing();
		services.AddEventStoreCapabilities(custom);

		using var provider = services.BuildServiceProvider();
		var capabilities = provider.GetRequiredService<IEventStoreCapabilitiesProvider>().GetCapabilities();

		await Assert.That(capabilities).IsEqualTo(custom);
	}
}
