namespace Purview.EventSourcing.Aggregates;

public interface IRequirement<T>
{
	void SetService(T service);
}
