namespace Purview.EventSourcing.SqlServer;

sealed class SqlServerEventStoreTransactionFactory(IEventStoreCorrelationIdProvider correlationIdProvider)
	: IEventStoreTransactionFactory
{
	public IEventStoreTransaction Create(string? correlationId = null) =>
		new EventStoreTransaction(correlationId ?? correlationIdProvider.GetCorrelationId());
}
