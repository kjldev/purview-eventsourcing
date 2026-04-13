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
	/// When <see langword="null"/>, a new correlation ID is generated.
	/// </param>
	IEventStoreTransaction Create(string? correlationId = null);
}

sealed class EventStoreTransactionFactory : IEventStoreTransactionFactory
{
	public IEventStoreTransaction Create(string? correlationId = null) => new EventStoreTransaction(correlationId);
}
