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

	public void Initialize(string locationId, string locationName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(locationId);
		ArgumentException.ThrowIfNullOrWhiteSpace(locationName);

		LocationInitialized(locationId, locationName);
	}

	public void Rename(string locationName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(locationName);

		if (!string.Equals(LocationName, locationName, StringComparison.Ordinal))
			LocationRenamed(locationName);
	}

	[GenerateAggregateEvent]
	public partial void LocationInitialized(string locationId, string locationName);

	[GenerateAggregateEvent]
	public partial void LocationRenamed(string locationName);
}
