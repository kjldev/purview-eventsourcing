using Purview.EventSourcing.Aggregates.Events;

namespace Purview.EventSourcing.Samples.Domain.Events;

public sealed class CustomerRegisteredEvent : EventBase
{
	public string Name { get; set; } = default!;
	public string Email { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash)
	{
		hash.Add(Name);
		hash.Add(Email);
	}
}

public sealed class CustomerEmailChangedEvent : EventBase
{
	public string Email { get; set; } = default!;

	protected override void BuildEventHash(ref HashCode hash) => hash.Add(Email);
}

public sealed class CustomerPhoneChangedEvent : EventBase
{
	public string? PhoneNumber { get; set; }

	protected override void BuildEventHash(ref HashCode hash) => hash.Add(PhoneNumber);
}

public sealed class CustomerDeactivatedEvent : EventBase
{
	protected override void BuildEventHash(ref HashCode hash) { }
}

public sealed class CustomerReactivatedEvent : EventBase
{
	protected override void BuildEventHash(ref HashCode hash) { }
}
