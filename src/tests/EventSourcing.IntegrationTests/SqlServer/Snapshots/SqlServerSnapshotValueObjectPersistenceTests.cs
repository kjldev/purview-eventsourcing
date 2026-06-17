using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Purview.EventSourcing.Aggregates.Events;
using Purview.EventSourcing.ChangeFeed;
using Purview.EventSourcing.Fixtures.SqlServer;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.ValueObjects;
using Purview.EventSourcing.Services;
using Purview.EventSourcing.SqlServer.Events;
using Purview.EventSourcing.SqlServer.Snapshot;

namespace Purview.EventSourcing.SqlServer.Snapshots;

[ClassDataSource<SqlServerSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed class SqlServerSnapshotValueObjectPersistenceTests(SqlServerSnapshotEventStoreFixture fixture)
{
	[Test]
	public async Task SaveAsync_GivenComplexValueObjects_PersistsEventSnapshotAndSupportsSnapshotLinqQueries(
		CancellationToken cancellationToken
	)
	{
		var context = fixture.CreateContext(tableName: $"Snapshots_{Guid.NewGuid():N}");
		var aggregateId = Guid.NewGuid().ToString("D");
		var eventTableName = $"EventStore_{Guid.NewGuid():N}";
		var displayName = "Jane Snapshot";
		var displayName2 = "Jane Snapshot 2";

		var innerEventStore = new SqlServerEventStore<SnapshotValueObjectsAggregate>(
			eventNameMapper: new AggregateEventNameMapper(),
			sqlServerOptions: Options.Create(
				new SqlServerEventStoreOptions
				{
					ConnectionString = context.SqlServerConnectionString,
					TableName = eventTableName,
					SchemaName = "dbo",
					AutoCreateTable = true,
					TimeoutInSeconds = 60,
				}
			),
			distributedCache: Substitute.For<IDistributedCache>(),
			eventStoreTelemetry: Substitute.For<ISqlServerEventStoreTelemetry>(),
			aggregateChangeNotifier: Substitute.For<IAggregateChangeFeedNotifier<SnapshotValueObjectsAggregate>>(),
			aggregateRequirementsManager: Substitute.For<IAggregateRequirementsManager>()
		);

		var store = new SqlServerSnapshotEventStore<SnapshotValueObjectsAggregate>(
			innerEventStore,
			Options.Create(
				new SqlServerSnapshotEventStoreOptions
				{
					ConnectionString = context.SqlServerConnectionString,
					TableName = $"Snapshots_{Guid.NewGuid():N}",
					SchemaName = "dbo",
					AutoCreateTable = true,
				}
			),
			Substitute.For<ISqlServerSnapshotEventStoreTelemetry>()
		);

		var aggregate = new SnapshotValueObjectsAggregate();
		aggregate.Details.Id = aggregateId;
		aggregate.CaptureUserDetails(
			UserDetails.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"), displayName),
			UserDetails2.Create(Guid.Parse("22222222-2222-2222-2222-222222222222"), displayName2)
		);

		var saveResult = await store.SaveAsync(aggregate, operationContext: null, cancellationToken);

		var snapshotQuery = await store.QueryAsync(
			m => m.UserDetails.DisplayName == displayName && m.UserDetails2.DisplayName == displayName2,
			cancellationToken: cancellationToken
		);
		var snapshot = snapshotQuery.Results.SingleOrDefault();

		var events = new List<(IEvent @event, string eventType)>();
		await foreach (
			var @event in innerEventStore.GetEventRangeAsync(
				aggregateId,
				versionFrom: 1,
				versionTo: null,
				cancellationToken
			)
		)
		{
			events.Add(@event);
		}

		var eventPayload = events.Single().@event;
		var eventUserDetails = eventPayload.GetType().GetProperty(nameof(SnapshotValueObjectsAggregate.UserDetails))!.GetValue(eventPayload)!;
		var eventUserDetails2 = eventPayload.GetType().GetProperty(nameof(SnapshotValueObjectsAggregate.UserDetails2))!.GetValue(eventPayload)!;
		var eventDisplayName = (string)eventUserDetails.GetType().GetProperty(nameof(UserDetails.DisplayName))!.GetValue(eventUserDetails)!;
		var eventDisplayName2 = (string)eventUserDetails2.GetType().GetProperty(nameof(UserDetails2.DisplayName))!.GetValue(eventUserDetails2)!;

		await Assert.That(saveResult.Saved).IsTrue();
		await Assert.That(events).Count().IsEqualTo(1);
		await Assert.That(eventDisplayName).IsEqualTo(displayName);
		await Assert.That(eventDisplayName2).IsEqualTo(displayName2);
		await Assert.That(snapshot).IsNotNull();
		await Assert.That(snapshot!.UserDetails.DisplayName).IsEqualTo(displayName);
		await Assert.That(snapshot.UserDetails2.DisplayName).IsEqualTo(displayName2);
	}
}
