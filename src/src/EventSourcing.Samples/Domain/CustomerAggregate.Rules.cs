using Purview.EventSourcing.Samples.ValueObjects;

namespace Purview.EventSourcing.Samples.Domain;

partial class CustomerAggregate
{
	partial void OnEmailChanging(ref EmailAddress email)
	{
		if (email.Domain.Contains("eventsourcing-sample.", StringComparison.Ordinal))
			throw new ArgumentException("Employees of Event-Sourcing-Sample PLC cannot be customers");
	}
}
