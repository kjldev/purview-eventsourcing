using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Samples.Domain;

/// <summary>
/// Represents a physical storage facility that can hold inventory.
/// </summary>
[GenerateAggregate]
public sealed partial class LocationAggregate : AggregateBase
{
	public string LocationId { get; private set; } = default!;

	public string LocationName { get; private set; } = default!;

	[GenerateAggregateEvent]
	public partial void Initialize(string locationId, string locationName);

	[GenerateAggregateEvent]
	public partial void Rename(string locationName);
}
