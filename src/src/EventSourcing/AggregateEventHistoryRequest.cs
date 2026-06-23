namespace Purview.EventSourcing;

/// <summary>
/// Request model for querying aggregate event history.
/// </summary>
public sealed class AggregateEventHistoryRequest
{
	/// <summary>
	/// Continuation token from a previous <see cref="ContinuationResponse{T}" /> page.
	/// </summary>
	public string? ContinuationToken { get; set; }

	/// <summary>
	/// Maximum number of records to return for this page.
	/// </summary>
	public int MaxRecords { get; set; } = ContinuationRequest.DefaultMaxRecords;

	/// <summary>
	/// Optional inclusive starting aggregate version.
	/// </summary>
	public int? FromVersion { get; set; }

	/// <summary>
	/// Optional inclusive ending aggregate version.
	/// </summary>
	public int? ToVersion { get; set; }

	/// <summary>
	/// Optional inclusive lower bound for event timestamp in UTC.
	/// </summary>
	public DateTimeOffset? FromUtc { get; set; }

	/// <summary>
	/// Optional inclusive upper bound for event timestamp in UTC.
	/// </summary>
	public DateTimeOffset? ToUtc { get; set; }
}
