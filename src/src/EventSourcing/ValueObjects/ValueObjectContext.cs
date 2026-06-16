namespace Purview.EventSourcing.ValueObjects;

public readonly record struct ValueObjectContext<TAggregate>(
	TAggregate Aggregate,
	string MemberName,
	string? EventName = null
);
