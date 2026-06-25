using Purview.EventSourcing.Fixtures.SqlServer;

namespace Purview.EventSourcing.Samples.ValueObjects;

[ClassDataSource<SqlServerSnapshotEventStoreFixture>(Shared = SharedType.PerAssembly)]
public class UserCaptureTests(SqlServerSnapshotEventStoreFixture fixture)
{
	static readonly Faker Faker = new();

	[Test]
	public async Task UserCapture_GivenComplexValueObject_DoesNotThrowOnSnapshot(CancellationToken cancellationToken)
	{
		// Arrange
		var eventStore = fixture.CreateSnapshotStore<ValueObjectTestAggregate>();
		var aggregate = CreateTestAggregate();

		// Act
		var act = () => eventStore.SnapshotAsync(aggregate, cancellationToken);

		// Assert
		await Assert.That(act).ThrowsNothing();
	}

	[Test]
	public async Task UserCapture_GivenComplexValueObject_CanStoreAndRetrieveInEF(CancellationToken cancellationToken)
	{
		// Arrange
		var eventStore = fixture.CreateSnapshotStore<ValueObjectTestAggregate>();
		var aggregate = CreateTestAggregate();

		// Act
		await eventStore.SnapshotAsync(aggregate, cancellationToken);
		var retrievedAggregate = await eventStore.GetAsync(aggregate.Id(), cancellationToken);

		// Assert
		await Assert.That(retrievedAggregate).IsNotNull();
		await Assert.That(retrievedAggregate!.UserCaptureRecord).IsEqualTo(aggregate.UserCaptureRecord);
	}

	static UserCaptureRecord CreateSUT(UserDetails? user = null, DateTimeOffset? occurredAt = null) =>
		UserCaptureRecord.Create(user ?? CreateUserDetails(), occurredAt ?? Faker.Date.RecentOffset());

	static UserDetails CreateUserDetails(Guid? id = null, string? displayName = null, bool isActive = true) =>
		new(id ?? Faker.Random.Guid(), displayName ?? Faker.Person.FullName, isActive);

	static ValueObjectTestAggregate CreateTestAggregate(UserCaptureRecord? user = null)
	{
		ValueObjectTestAggregate aggregate = new();
		aggregate.Details.Id = Faker.Random.Guid().ToString();

		aggregate.SetUserCapture(user ?? CreateSUT());

		return aggregate;
	}
}
