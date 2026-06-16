namespace Purview.EventSourcing.ValueObjects;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Design",
	"CA1040:Avoid empty interfaces",
	Justification = "Required to identify value objects in the domain model"
)]
public interface IValueObject { }

public interface IValueObject<TSelf> : IValueObject, IComparable<TSelf>, IComparable
	where TSelf : IValueObject<TSelf> { }
