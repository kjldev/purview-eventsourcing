using Microsoft.Extensions.Options;
using Purview.EventSourcing.Fixtures.SqlServer;
using Purview.EventSourcing.Internal;
using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.SqlServer.Snapshot;

namespace Purview.EventSourcing.SqlServer.Snapshots;

[ClassDataSource<SqlServerSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed class CustomerAggregateSnapshotEventStoreTests(SqlServerSnapshotEventStoreFixture fixture)
{
	[Test]
	public async Task SnapshotAsync_GivenCustomerAggregateWithEmailAddress_QueriesByEmail(
		CancellationToken cancellationToken
	)
	{
		var context = fixture.CreateContext(tableName: $"Snapshots_{Guid.NewGuid():N}");
		var email = "updated@test.com";
		var aggregate = new CustomerAggregate();
		aggregate.Details.Id = Guid.NewGuid().ToString("D");
		aggregate.RegisterCustomer("Jane Smith", "jane@test.com");
		aggregate.ChangeEmail(email);
		var innerEventStore = Substitute.For<INonQueryableEventStore<CustomerAggregate>>();
		innerEventStore
			.FulfilRequirements(Arg.Any<CustomerAggregate>())
			.Returns(callInfo => callInfo.Arg<CustomerAggregate>());

		var store = new SqlServerSnapshotEventStore<CustomerAggregate>(
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

		await store.SnapshotAsync(aggregate, cancellationToken);

		var count = await store.CountAsync(m => m.Email == email, cancellationToken);
		var query = await store.QueryAsync(m => m.Email == email, cancellationToken: cancellationToken);
		var result = query.Results.SingleOrDefault();

		await Assert.That(count).IsEqualTo(1);
		await Assert.That(result).IsNotNull();
		await Assert.That(result!.Name).IsEqualTo("Jane Smith");
		await Assert.That(result.Email).IsEqualTo(email);
	}

	[Test]
	public async Task QueryAsync_GivenOrderByScalarValueObject_TranslatesAndOrders(CancellationToken cancellationToken)
	{
		var context = fixture.CreateContext(tableName: $"Snapshots_{Guid.NewGuid():N}");
		var innerEventStore = Substitute.For<INonQueryableEventStore<CustomerAggregate>>();
		innerEventStore
			.FulfilRequirements(Arg.Any<CustomerAggregate>())
			.Returns(callInfo => callInfo.Arg<CustomerAggregate>());

		var store = new SqlServerSnapshotEventStore<CustomerAggregate>(
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

		var charlie = new CustomerAggregate { Details = { Id = Guid.NewGuid().ToString("D") } };
		charlie.RegisterCustomer("Charlie", "charlie@test.com");
		await store.SnapshotAsync(charlie, cancellationToken);

		var alice = new CustomerAggregate { Details = { Id = Guid.NewGuid().ToString("D") } };
		alice.RegisterCustomer("Alice", "alice@test.com");
		await store.SnapshotAsync(alice, cancellationToken);

		var result = await store.QueryAsync(
			m => m.IsActive,
			q => q.OrderBy(c => c.Name),
			new ContinuationRequest { MaxRecords = 10 },
			cancellationToken
		);

		await Assert.That(result.Results).Count().IsEqualTo(2);
		await Assert.That(result.Results[0].Name).IsEqualTo("Alice");
		await Assert.That(result.Results[1].Name).IsEqualTo("Charlie");
	}

	[Test]
	public async Task QueryAsync_GivenOrderByScalarInnerValue_FailsWithClearMessage(CancellationToken cancellationToken)
	{
		var context = fixture.CreateContext(tableName: $"Snapshots_{Guid.NewGuid():N}");
		var innerEventStore = Substitute.For<INonQueryableEventStore<CustomerAggregate>>();
		innerEventStore
			.FulfilRequirements(Arg.Any<CustomerAggregate>())
			.Returns(callInfo => callInfo.Arg<CustomerAggregate>());

		var store = new SqlServerSnapshotEventStore<CustomerAggregate>(
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

		var customer = new CustomerAggregate { Details = { Id = Guid.NewGuid().ToString("D") } };
		customer.RegisterCustomer("Zed", "zed@test.com");
		await store.SnapshotAsync(customer, cancellationToken);

		Task<ContinuationResponse<CustomerAggregate>> Act() =>
			store.QueryAsync(
				m => m.IsActive,
				q => q.OrderBy(c => c.Name.Value),
				new ContinuationRequest { MaxRecords = 10 },
				cancellationToken
			);

		var ex = await Assert.That(Act!).Throws<InvalidOperationException>();
		await Assert.That(ex).IsNotNull();
		await Assert.That(ex!.Message).Contains("Order by the scalar value object property itself");
	}
}
