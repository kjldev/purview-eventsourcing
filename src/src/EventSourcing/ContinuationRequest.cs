using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Purview.EventSourcing;

/// <summary>
/// Allows to query/ list result sets to be continuation.
/// </summary>
public class ContinuationRequest
{
	/// <summary>
	/// The default number of records to return.
	/// </summary>
	public const int DefaultMaxRecords = 20;

	/// <summary>
	/// The continuation token, provided from <see cref="ContinuationResponse{T}.ContinuationToken"/>.
	/// </summary>
	public string? ContinuationToken { get; set; }

	/// <summary>
	/// The maximum number of records to return per-operation. Defaults to 20. Minimum is 1, maximum is 1000.
	/// </summary>
	[Range(1, 1000)]
	[DefaultValue(DefaultMaxRecords)]
	public int MaxRecords { get; set; } = DefaultMaxRecords;
}
