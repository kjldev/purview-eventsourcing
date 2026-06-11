using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Samples.ValueObjects;

namespace Purview.EventSourcing.Samples.Domain;

/// <summary>
/// Simple aggregate demonstrating basic customer management.
/// Shows: single-property events, string manipulation, validation.
/// </summary>
[GenerateAggregate]
public sealed partial class CustomerAggregate : AggregateBase
{
	public Name Name { get; private set; }

	public EmailAddress Email { get; private set; }

	public string? PhoneNumber { get; private set; }

	public bool IsActive { get; private set; }

	partial void OnEmailChanging(ref EmailAddress email)
	{
		if (email.Domain.Contains("eventsourcing-sample.", StringComparison.Ordinal))
			throw new ArgumentException("Employees of Event-Sourcing-Sample PLC cannot be customers");
	}

	/// <summary>
	/// Updates one or more customer details in a single operation, raising a granular event
	/// for each field that has actually changed. Pass <see langword="null"/> for any field
	/// that should remain unchanged. To clear the phone number, use <see cref="ChangePhoneNumber"/> directly.
	/// </summary>
	public CustomerAggregate UpdateDetails(string? name = null, string? email = null, string? phoneNumber = null)
	{
		if (name is not null)
			ChangeName(name);

		if (email is not null)
			ChangeEmail(email);

		if (phoneNumber is not null)
			ChangePhoneNumber(phoneNumber);

		return this;
	}

	public CustomerAggregate Deactivate() => IsActive ? ChangeIsActive(isActive: false) : this;

	public CustomerAggregate Reactivate() => IsActive ? this : ChangeIsActive(isActive: true);

	// Generated methods.

	[GenerateAggregateEvent]
	public partial CustomerAggregate RegisterCustomer(string name, string email, bool isActive = true);

	[GenerateAggregateEvent]
	public partial CustomerAggregate ChangeName(string name);

	[GenerateAggregateEvent]
	public partial CustomerAggregate ChangeEmail(string email);

	[GenerateAggregateEvent]
	public partial CustomerAggregate ChangePhoneNumber(string? phoneNumber);

	[GenerateAggregateEvent]
	private partial CustomerAggregate ChangeIsActive(bool isActive);
}
