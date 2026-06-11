namespace Purview.EventSourcing.ValueObjects;

public interface IContextualValueObject<TSelf, TValue, TAggregate>
	where TSelf : IValueObject
{
	static abstract TSelf Create(TValue value, in ValueObjectContext<TAggregate> context);
}
