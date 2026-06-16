namespace Purview.EventSourcing.ValueObjects;

public interface IScalarValueObject<TSelf, TValue> : IValueObject, IComparable<TSelf>, IComparable
	where TSelf : IScalarValueObject<TSelf, TValue>
{
	TValue Value { get; }

	int CompareTo(TValue other);

	static abstract TSelf Create(TValue value);

	static abstract TSelf Hydrate(TValue value);
}
