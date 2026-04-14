namespace Purview.EventSourcing.SqlServer;

sealed class SqlServerEventStoreTransactionFactory : IEventStoreTransactionFactory
{
	public IEventStoreTransaction Create(string? correlationId = null) =>
		new EventStoreTransaction(correlationId);
}
