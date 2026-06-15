using Purview.EventSourcing.Samples.Domain.CustomerEvents;

namespace Purview.EventSourcing.Samples.Domain;

partial class CustomerAggregate
{
	partial void OnShouldApplyDeactivatedEvent(DeactivatedEvent @event, ref bool shouldApply) => shouldApply = IsActive;

	private partial void Apply(DeactivatedEvent @event) => IsActive = false;

	partial void OnShouldApplyReactivatedEvent(ReactivatedEvent @event, ref bool shouldApply) =>
		shouldApply = !IsActive;

	private partial void Apply(ReactivatedEvent @event) => IsActive = true;
}
