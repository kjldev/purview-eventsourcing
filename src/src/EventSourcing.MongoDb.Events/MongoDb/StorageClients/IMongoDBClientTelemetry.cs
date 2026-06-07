using Microsoft.Extensions.Logging;
using Purview.Telemetry;

namespace Purview.EventSourcing.MongoDB.StorageClients;

[Logger]
public interface IMongoDBClientTelemetry
{
    [Log(LogLevel.Warning)]
    void DeleteResultedInNoOp(string id);

    [Log(LogLevel.Error)]
    void FailedToWriteBatch(Exception exception);

    void Initialized();

    void EventsInitialized();
}
