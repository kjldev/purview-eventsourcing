using Microsoft.CodeAnalysis.Text;

namespace Purview.EventSourcing.SourceGenerator.Common;

/// <summary>
/// Composite result of aggregate analysis: the model consumed by the emitters, the semantic
/// event contract used by the manifest, and value-based source locations used to position
/// manifest diagnostics. The contract and locations are intentionally separate so that
/// location-only changes never invalidate manifest comparison.
/// </summary>
sealed record AggregateTarget(AggregateInfo Info, AggregateContract Contract, AggregateContractLocations Locations);

/// <summary>
/// Semantic event contract for an aggregate. Deliberately location-free so the serialized
/// manifest and baseline comparison are invariant to trivia and unrelated formatting.
/// </summary>
sealed record AggregateContract(
	string AggregateName,
	string AggregateNamespace,
	EquatableArray<EventContractEntry> Events
);

/// <summary>
/// A single generated event contract entry, keyed by event identity and schema version.
/// </summary>
sealed record EventContractEntry(
	string EventName,
	string EventNamespace,
	string MethodName,
	int SchemaVersion,
	EquatableArray<EventContractField> Fields
);

/// <summary>
/// A persisted event field. <see cref="Type"/> is a fully-qualified, culture-invariant type
/// token and <see cref="ElementType"/> captures the element type for array fields.
/// </summary>
sealed record EventContractField(
	string Name,
	string Type,
	string? ElementType,
	bool IsArray,
	bool IsNullable,
	bool IsRequired,
	bool IsString
);

/// <summary>
/// Value-based source locations used to place manifest diagnostics. These are never serialized
/// into the manifest; they only position diagnostics at the current declaration.
/// </summary>
sealed record AggregateContractLocations(
	string AggregateKey,
	ContractLocation? Aggregate,
	EquatableArray<ContractLocationEntry> Entries
);

/// <summary>
/// Maps an event method (by composite key) to its current source location.
/// </summary>
sealed record ContractLocationEntry(string Key, ContractLocation Location);

/// <summary>
/// A location captured by value (file path and text-span coordinates) rather than by syntax
/// tree reference, so incremental equality is based on position rather than tree identity.
/// </summary>
readonly record struct ContractLocation(
	string FilePath,
	int StartLine,
	int StartCharacter,
	int EndLine,
	int EndCharacter,
	int SpanStart,
	int SpanLength
)
{
	public static ContractLocation? FromLocation(Location? location)
	{
		if (location is null || !location.IsInSource || location.SourceTree is null)
			return null;

		var lineSpan = location.GetLineSpan();
		var span = location.SourceSpan;
		return new(
			location.SourceTree.FilePath,
			lineSpan.StartLinePosition.Line,
			lineSpan.StartLinePosition.Character,
			lineSpan.EndLinePosition.Line,
			lineSpan.EndLinePosition.Character,
			span.Start,
			span.Length
		);
	}

	public static Location ToRoslynLocation(ContractLocation? location)
	{
		if (location is null)
			return Location.None;

		var value = location.Value;
		TextSpan textSpan = new(value.SpanStart, value.SpanLength);
		LinePositionSpan lineSpan = new(
			new LinePosition(value.StartLine, value.StartCharacter),
			new LinePosition(value.EndLine, value.EndCharacter)
		);
		return Location.Create(value.FilePath, textSpan, lineSpan);
	}
}

/// <summary>
/// The aggregated, deterministic, machine-readable event-contract manifest.
/// </summary>
sealed record EventContractManifest(int FormatVersion, EquatableArray<AggregateContract> Aggregates);

/// <summary>
/// Result of parsing the baseline manifest. At most one of <see cref="Manifest"/> or
/// <see cref="Error"/> is set.
/// </summary>
sealed record BaselineState(EventContractManifest? Manifest, BaselineError? Error);

/// <summary>
/// Describes why a baseline manifest could not be used.
/// </summary>
sealed record BaselineError(string Message);

/// <summary>
/// The result of comparing the current contracts against an approved baseline.
/// </summary>
sealed record EventContractComparison(EquatableArray<ContractIssue> Issues);

/// <summary>
/// A single contract-compatibility issue ready to be reported as a diagnostic. Message arguments
/// are plain strings so the record stays equatable and culture-invariant.
/// </summary>
sealed record ContractIssue(
	DiagnosticDescriptor Descriptor,
	EquatableArray<string> MessageArgs,
	string? EventKey,
	string? AggregateKey
);
