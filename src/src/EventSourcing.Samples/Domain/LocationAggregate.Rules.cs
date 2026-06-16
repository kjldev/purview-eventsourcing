namespace Purview.EventSourcing.Samples.Domain;

partial class LocationAggregate
{
	partial void OnRaisingCreatedEvent(ref string locationId, ref string locationName)
	{
		if (Details.SavedVersion > 0)
			throw new InvalidOperationException("Location has already been created.");
	}
}
