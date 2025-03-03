namespace Purview.EventSourcing.AzureStorage.Exceptions;
#pragma warning disable CA1032 // Implement standard exception constructors
public class CommitException(int errorCode, string aggregateId, string idempotencyId, int versionAttempted, int version, string httpStatusMessage)
	: Exception($"Failed to commit events.\n\tErrorCode: {errorCode} - {httpStatusMessage}\n\tAggregateId: {aggregateId}\n\tIdempotencyId: {idempotencyId}\n\tVersionAttempted: {versionAttempted}\n\tVersionPresent: {version}")
#pragma warning restore CA1032 // Implement standard exception constructors
{
	public string AggregateId { get; } = aggregateId ?? throw new ArgumentNullException(nameof(aggregateId));

	public int VersionAttempted { get; } = versionAttempted;

	public string IdempotencyId { get; } = idempotencyId ?? throw new ArgumentNullException(nameof(idempotencyId));

	public int Version { get; } = version;

	public string HttpStatusMessage { get; } = httpStatusMessage;
}
