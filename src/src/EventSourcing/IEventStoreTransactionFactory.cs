namespace Purview.EventSourcing;

/// <summary>
/// Creates logical event-store transactions for coordinating multi-aggregate saves.
/// </summary>
public interface IEventStoreTransactionFactory
{
    /// <summary>
    /// Creates a new transaction.
    /// </summary>
    /// <param name="correlationId">
    /// Optional correlation ID shared by all enlisted aggregate saves.
    /// When <see langword="null"/>, the ambient correlation ID provider is consulted before generating a new correlation ID.
    /// </param>
    IEventStoreTransaction Create(string? correlationId = null);
}

public sealed class EventStoreTransactionFactory(
    IEventStoreCorrelationIdProvider correlationIdProvider
) : IEventStoreTransactionFactory
{
    public IEventStoreTransaction Create(string? correlationId = null) =>
        new EventStoreTransaction(correlationId ?? correlationIdProvider.GetCorrelationId());
}
