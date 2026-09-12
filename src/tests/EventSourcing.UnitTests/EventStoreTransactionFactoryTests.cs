namespace Purview.EventSourcing;

public sealed class EventStoreTransactionFactoryTests
{
	[Test]
	public async Task Create_GivenOptions_PropagatesRequiredGuarantee()
	{
		var correlationIdProvider = IEventStoreCorrelationIdProvider.Mock();
		correlationIdProvider.GetCorrelationId().Returns("ambient-correlation");
		EventStoreTransactionFactory factory = new(correlationIdProvider);

		await using var transaction = factory.Create(
			new EventStoreTransactionOptions { RequiredGuarantee = EventStoreTransactionGuarantee.Atomic }
		);

		await Assert.That(transaction.CorrelationId).IsEqualTo("ambient-correlation");
		await Assert.That(transaction.RequiredGuarantee).IsEqualTo(EventStoreTransactionGuarantee.Atomic);
	}

	[Test]
	public async Task Create_GivenNullCorrelationId_UsesAmbientProviderValue()
	{
		// Arrange
		var correlationIdProvider = IEventStoreCorrelationIdProvider.Mock();
		correlationIdProvider.GetCorrelationId().Returns("ambient-correlation");
		EventStoreTransactionFactory factory = new(correlationIdProvider);

		// Act
		await using var transaction = factory.Create();

		// Assert
		await Assert.That(transaction.CorrelationId).IsEqualTo("ambient-correlation");
	}

	[Test]
	public async Task Create_GivenExplicitCorrelationId_PrefersExplicitValueOverAmbientProvider()
	{
		// Arrange
		var correlationIdProvider = IEventStoreCorrelationIdProvider.Mock();
		correlationIdProvider.GetCorrelationId().Returns("ambient-correlation");
		EventStoreTransactionFactory factory = new(correlationIdProvider);

		// Act
		await using var transaction = factory.Create("explicit-correlation");

		// Assert
		await Assert.That(transaction.CorrelationId).IsEqualTo("explicit-correlation");
		correlationIdProvider.GetCorrelationId().WasNeverCalled();
	}
}
