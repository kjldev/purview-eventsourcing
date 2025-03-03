using Microsoft.Extensions.Logging;
using Purview.Telemetry.Logging;

namespace Purview.EventSourcing.ChangeFeed;

[Logger]
public interface IAggregateChangeFeedNotifierTelemetry
{
	[Log(LogLevel.Information)]
	void AfterSaveNotificationStart(string id, string aggregateType, bool isNewAggregate, int eventCount);

	[Log(LogLevel.Information)]
	void ProcessingStart(string id, string changeFeedProcessorType);

	[Log(LogLevel.Information)]
	void ProcessingComplete(string id, string changeFeedProcessorType, long elapsedMilliseconds, bool success);

	[Log(LogLevel.Error)]
	void ProcessingFailed(string id, string changeFeedProcessorType, Exception exception);

	[Log(LogLevel.Information)]
	void AfterSaveNotificationComplete(string id, string aggregateType, bool isNewAggregate, int eventCount, long elapsedMilliseconds);

	[Log(LogLevel.Information)]
	void BeforeSaveNotificationComplete(string id, string aggregateType, bool isNewAggregate, long elapsedMilliseconds);

	[Log(LogLevel.Information)]
	void BeforeSaveNotificationStart(string id, string aggregateType, bool isNewAggregate);

	[Log(LogLevel.Error)]
	void FailureNotificationStart(string id, string aggregateType, Exception exception);

	[Log(LogLevel.Information)]
	void FailureNotificationComplete(string id, string aggregateType, long elapsedMilliseconds);

	[Log(LogLevel.Information)]
	void AfterDeleteNotificationComplete(string id, string aggregateType, long elapsedMilliseconds);

	[Log(LogLevel.Information)]
	void AfterDeleteNotificationStart(string id, string aggregateType);

	[Log(LogLevel.Information)]
	void BeforeDeleteNotificationComplete(string id, string aggregateType, long elapsedMilliseconds);

	[Log(LogLevel.Information)]
	void BeforeDeleteNotificationStart(string id, string aggregateType);
}
