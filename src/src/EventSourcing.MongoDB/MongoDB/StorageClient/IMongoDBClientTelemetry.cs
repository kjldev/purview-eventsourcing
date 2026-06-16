using Purview.Telemetry;

namespace Purview.EventSourcing.MongoDB.StorageClient;

[Logger]
public interface IMongoDBClientTelemetry
{
	[Warning]
	void DeleteResultedInNoOp(string id);

	[Error]
	void FailedToWriteBatch(Exception exception);

	void Initialized();

	void EventsInitialized();
}
