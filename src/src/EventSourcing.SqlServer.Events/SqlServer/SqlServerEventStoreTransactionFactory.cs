namespace Purview.EventSourcing.SqlServer;

sealed class SqlServerEventStoreTransactionFactory(IEventStoreCorrelationIdProvider correlationIdProvider)
	: IEventStoreTransactionFactory
{
	private readonly IEventStoreTransactionFactory innerFactory = new EventStoreTransactionFactory(correlationIdProvider);

	public IEventStoreTransaction Create(string? correlationId = null) =>
		innerFactory.Create(correlationId);
}
