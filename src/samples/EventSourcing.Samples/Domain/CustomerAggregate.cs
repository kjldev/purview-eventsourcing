using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.Domain.Events;

namespace Purview.EventSourcing.Samples.Domain;

/// <summary>
/// Simple aggregate demonstrating basic customer management.
/// Shows: single-property events, string manipulation, validation.
/// </summary>
public sealed class CustomerAggregate : AggregateBase
{
	public string Name { get; private set; } = default!;
	public string Email { get; private set; } = default!;
	public string? PhoneNumber { get; private set; }
	public bool IsActive { get; private set; }

	protected override void RegisterEvents()
	{
		Register<CustomerRegisteredEvent>(Apply);
		Register<CustomerEmailChangedEvent>(Apply);
		Register<CustomerPhoneChangedEvent>(Apply);
		Register<CustomerDeactivatedEvent>(Apply);
		Register<CustomerReactivatedEvent>(Apply);
	}

	// Commands
	public void RegisterCustomer(string name, string email)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(email);

		RecordAndApply(new CustomerRegisteredEvent { Name = name, Email = email });
	}

	public void ChangeEmail(string newEmail)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);
		if (newEmail == Email) return;

		RecordAndApply(new CustomerEmailChangedEvent { Email = newEmail });
	}

	public void ChangePhoneNumber(string? phoneNumber)
	{
		RecordAndApply(new CustomerPhoneChangedEvent { PhoneNumber = phoneNumber });
	}

	public void Deactivate()
	{
		if (!IsActive) return;
		RecordAndApply(new CustomerDeactivatedEvent());
	}

	public void Reactivate()
	{
		if (IsActive) return;
		RecordAndApply(new CustomerReactivatedEvent());
	}

	// Apply methods
	void Apply(CustomerRegisteredEvent @event)
	{
		Name = @event.Name;
		Email = @event.Email;
		IsActive = true;
	}

	void Apply(CustomerEmailChangedEvent @event) => Email = @event.Email;
	void Apply(CustomerPhoneChangedEvent @event) => PhoneNumber = @event.PhoneNumber;
	void Apply(CustomerDeactivatedEvent _) => IsActive = false;
	void Apply(CustomerReactivatedEvent _) => IsActive = true;
}
