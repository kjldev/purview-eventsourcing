namespace Purview.EventSourcing;

public sealed class EventStoreTransactionFactoryTests
{
    [Test]
    public async Task Create_GivenNullCorrelationId_UsesAmbientProviderValue()
    {
        // Arrange
        var correlationIdProvider = Substitute.For<IEventStoreCorrelationIdProvider>();
        correlationIdProvider.GetCorrelationId().Returns("ambient-correlation");
        var factory = new EventStoreTransactionFactory(correlationIdProvider);

        // Act
        await using var transaction = factory.Create();

        // Assert
        await Assert.That(transaction.CorrelationId).IsEqualTo("ambient-correlation");
    }

    [Test]
    public async Task Create_GivenExplicitCorrelationId_PrefersExplicitValueOverAmbientProvider()
    {
        // Arrange
        var correlationIdProvider = Substitute.For<IEventStoreCorrelationIdProvider>();
        correlationIdProvider.GetCorrelationId().Returns("ambient-correlation");
        var factory = new EventStoreTransactionFactory(correlationIdProvider);

        // Act
        await using var transaction = factory.Create("explicit-correlation");

        // Assert
        await Assert.That(transaction.CorrelationId).IsEqualTo("explicit-correlation");
        correlationIdProvider.DidNotReceive().GetCorrelationId();
    }
}
