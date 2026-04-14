using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.SqlServer;

namespace Purview.EventSourcing.Samples.Domain;

[ClassDataSource<SqlServerEventStoreFixture>(Shared = SharedType.PerAssembly)]
public sealed class CustomerAggregateIntegrationTests(SqlServerEventStoreFixture fixture)
{
	#region Round-Trip Persistence

	[Test]
	public async Task SaveAsync_GivenRegisteredCustomer_LoadedStateMatchesOriginal(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("Jane Smith", "jane@test.com");

		using var store = fixture.CreateEventStore<CustomerAggregate>();
		await store.SaveAsync(customer, null, cancellationToken);

		var loaded = await store.GetAsync<CustomerAggregate>(id, null, cancellationToken);

		await Assert.That(loaded).IsNotNull();
		await Assert.That(loaded!.Name).IsEqualTo("Jane Smith");
		await Assert.That(loaded.Email).IsEqualTo("jane@test.com");
		await Assert.That(loaded.IsActive).IsTrue();
		await Assert.That(loaded.PhoneNumber).IsNull();
	}

	[Test]
	public async Task SaveAsync_GivenCustomerWithPhoneNumber_LoadedPhoneMatches(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("Bob Jones", "bob@test.com");
		customer.ChangePhoneNumber("+1-555-0199");

		using var store = fixture.CreateEventStore<CustomerAggregate>();
		await store.SaveAsync(customer, null, cancellationToken);

		var loaded = await store.GetAsync<CustomerAggregate>(id, null, cancellationToken);

		await Assert.That(loaded).IsNotNull();
		await Assert.That(loaded!.PhoneNumber).IsEqualTo("+1-555-0199");
	}

	[Test]
	public async Task SaveAsync_GivenMultipleEmailChanges_LoadedEmailMatchesFinalValue(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("Alice", "v1@test.com");
		customer.ChangeEmail("v2@test.com");
		customer.ChangeEmail("v3@test.com");

		using var store = fixture.CreateEventStore<CustomerAggregate>();
		await store.SaveAsync(customer, null, cancellationToken);

		var loaded = await store.GetAsync<CustomerAggregate>(id, null, cancellationToken);

		await Assert.That(loaded).IsNotNull();
		await Assert.That(loaded!.Email).IsEqualTo("v3@test.com");
	}

	[Test]
	public async Task SaveAsync_GivenDeactivatedCustomer_LoadedIsActiveFalse(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("Eve", "eve@test.com");
		customer.Deactivate();

		using var store = fixture.CreateEventStore<CustomerAggregate>();
		await store.SaveAsync(customer, null, cancellationToken);

		var loaded = await store.GetAsync<CustomerAggregate>(id, null, cancellationToken);

		await Assert.That(loaded).IsNotNull();
		await Assert.That(loaded!.IsActive).IsFalse();
	}

	[Test]
	public async Task SaveAsync_GivenReactivatedCustomer_LoadedIsActiveTrue(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("Carol", "carol@test.com");
		customer.Deactivate();
		customer.Reactivate();

		using var store = fixture.CreateEventStore<CustomerAggregate>();
		await store.SaveAsync(customer, null, cancellationToken);

		var loaded = await store.GetAsync<CustomerAggregate>(id, null, cancellationToken);

		await Assert.That(loaded).IsNotNull();
		await Assert.That(loaded!.IsActive).IsTrue();
	}

	#endregion

	#region Version Tracking

	[Test]
	public async Task SaveAsync_GivenMultipleOperations_VersionIsTrackedCorrectly(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("David", "david@test.com"); // v1
		customer.ChangeEmail("david2@test.com");              // v2
		customer.ChangePhoneNumber("+44-20-0000-0001");       // v3

		using var store = fixture.CreateEventStore<CustomerAggregate>();
		await store.SaveAsync(customer, null, cancellationToken);

		var loaded = await store.GetAsync<CustomerAggregate>(id, null, cancellationToken);

		await Assert.That(loaded).IsNotNull();
		await Assert.That(loaded!.Details.SavedVersion).IsEqualTo(3);
		await Assert.That(loaded.Details.CurrentVersion).IsEqualTo(3);
	}

	#endregion

	#region Delete and Restore

	[Test]
	public async Task DeleteAsync_GivenSavedCustomer_AggregateIsMarkedDeleted(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("Frank", "frank@test.com");

		using var store = fixture.CreateEventStore<CustomerAggregate>();
		await store.SaveAsync(customer, null, cancellationToken);
		await store.DeleteAsync(customer, null, cancellationToken);

		var isDeleted = await store.IsDeletedAsync<CustomerAggregate>(id, cancellationToken);

		await Assert.That(isDeleted).IsTrue();
	}

	[Test]
	public async Task RestoreAsync_GivenDeletedCustomer_AggregateIsRestoredWithOriginalState(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("Grace", "grace@test.com");

		using var store = fixture.CreateEventStore<CustomerAggregate>();
		await store.SaveAsync(customer, null, cancellationToken);
		await store.DeleteAsync(customer, null, cancellationToken);

		var deleted = await store.GetDeletedAsync<CustomerAggregate>(id, cancellationToken);
		await store.RestoreAsync(deleted!, null, cancellationToken);

		var restored = await store.GetAsync<CustomerAggregate>(id, null, cancellationToken);

		await Assert.That(restored).IsNotNull();
		await Assert.That(restored!.Name).IsEqualTo("Grace");
		await Assert.That(await store.IsDeletedAsync<CustomerAggregate>(id, cancellationToken)).IsFalse();
	}

	#endregion

	#region Point-in-Time Replay

	[Test]
	public async Task GetAtAsync_GivenCustomerWithEmailChange_ReturnsOriginalEmailAtVersion1(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("Heidi", "original@test.com"); // v1
		customer.ChangeEmail("updated@test.com");                 // v2

		using var store = fixture.CreateEventStore<CustomerAggregate>();
		await store.SaveAsync(customer, null, cancellationToken);

		var atVersion1 = await store.GetAtAsync<CustomerAggregate>(id, 1, null, cancellationToken);

		await Assert.That(atVersion1).IsNotNull();
		await Assert.That(atVersion1!.Email).IsEqualTo("original@test.com");
		await Assert.That(atVersion1.Details.CurrentVersion).IsEqualTo(1);
	}

	#endregion

	#region Event Replay Without Snapshots

	[Test]
	public async Task SaveAsync_GivenCustomerWithNoSnapshot_EventReplayRestoresState(CancellationToken cancellationToken)
	{
		var id = $"{Guid.NewGuid()}";
		var customer = new CustomerAggregate();
		customer.Details.Id = id;
		customer.RegisterCustomer("Ivan", "ivan@test.com");
		customer.ChangeEmail("ivan2@test.com");
		customer.Deactivate();

		// Use a very high snapshot interval to force pure event replay
		using var store = fixture.CreateEventStore<CustomerAggregate>(snapshotRecalculationInterval: int.MaxValue);
		await store.SaveAsync(customer, null, cancellationToken);

		var loaded = await store.GetAsync<CustomerAggregate>(id, null, cancellationToken);

		await Assert.That(loaded).IsNotNull();
		await Assert.That(loaded!.Name).IsEqualTo("Ivan");
		await Assert.That(loaded.Email).IsEqualTo("ivan2@test.com");
		await Assert.That(loaded.IsActive).IsFalse();
		await Assert.That(loaded.Details.SavedVersion).IsEqualTo(3);
	}

	#endregion
}
