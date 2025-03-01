using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Services;

public interface IAggregateRequirementsManager
{
	void Fulfil(IAggregate aggregate);
}
