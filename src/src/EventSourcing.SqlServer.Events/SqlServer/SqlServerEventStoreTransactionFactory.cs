namespace Purview.EventSourcing.SqlServer;

sealed class SqlServerEventStoreTransactionFactory(
    IEventStoreCorrelationIdProvider correlationIdProvider
) : IEventStoreTransactionFactory
{
    readonly EventStoreTransactionFactory _innerFactory = new(correlationIdProvider);

    public IEventStoreTransaction Create(string? correlationId = null) =>
        _innerFactory.Create(correlationId);
}
