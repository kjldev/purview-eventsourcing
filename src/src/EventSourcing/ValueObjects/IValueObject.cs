namespace Purview.EventSourcing.ValueObjects;

public interface IValueObject { }

public interface IValueObject<TSelf> : IValueObject, IComparable<TSelf>, IComparable
	where TSelf : IValueObject<TSelf> { }
