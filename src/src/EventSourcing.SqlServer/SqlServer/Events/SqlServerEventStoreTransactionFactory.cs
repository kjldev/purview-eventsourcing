namespace Purview.EventSourcing.SqlServer.Events;

/// <summary>
/// Creates SQL Server transaction coordinators that support enlisting additional SQL work
/// alongside event-store aggregate saves.
/// </summary>
public interface ISqlServerEventStoreTransactionFactory
{
	/// <summary>
	/// Creates a new SQL Server transaction.
	/// </summary>
	/// <param name="correlationId">
	/// Optional correlation ID shared by all enlisted aggregate saves.
	/// When <see langword="null"/>, the ambient correlation ID provider is consulted before generating a new correlation ID.
	/// </param>
	ISqlServerEventStoreTransaction CreateSqlServerTransaction(string? correlationId = null);
}

public sealed class SqlServerEventStoreTransactionFactory(IEventStoreCorrelationIdProvider correlationIdProvider)
	: IEventStoreTransactionFactory,
		ISqlServerEventStoreTransactionFactory
{
	public IEventStoreTransaction Create(string? correlationId = null) => CreateSqlServerTransaction(correlationId);

	public ISqlServerEventStoreTransaction CreateSqlServerTransaction(string? correlationId = null) =>
		new SqlServerEventStoreTransaction(correlationId ?? correlationIdProvider.GetCorrelationId());
}
