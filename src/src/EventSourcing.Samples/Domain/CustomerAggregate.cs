using System.ComponentModel.DataAnnotations;
using Purview.EventSourcing.Aggregates;

namespace Purview.EventSourcing.Samples.Domain;

/// <summary>
/// Simple aggregate demonstrating basic customer management.
/// Shows: single-property events, string manipulation, validation.
/// </summary>
[GenerateAggregate]
public sealed partial class CustomerAggregate : AggregateBase
{
	public string Name { get; private set; } = default!;

	[EmailAddress]
	public string Email { get; private set; } = default!;

	public string? PhoneNumber { get; private set; }

	public bool IsActive { get; private set; }

	// Commands
	public CustomerAggregate RegisterCustomer(string name, string email)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentException.ThrowIfNullOrWhiteSpace(email);

		// Transform email: trim and lowercase
		email = email.Trim().ToLowerInvariant();

		return CustomerRegistered(name, email, isActive: true);
	}

	public CustomerAggregate ChangeName(string newName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(newName);

		return CustomerNameChanged(newName);
	}

	public CustomerAggregate ChangeEmail(string newEmail)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);

		// Transform email: trim and lowercase
		newEmail = newEmail.Trim().ToLowerInvariant();

		return CustomerEmailChanged(newEmail);
	}

	public CustomerAggregate ChangePhoneNumber(string? phoneNumber) => CustomerPhoneChanged(phoneNumber);

	/// <summary>
	/// Updates one or more customer details in a single operation, raising a granular event
	/// for each field that has actually changed. Pass <see langword="null"/> for any field
	/// that should remain unchanged. To clear the phone number, use <see cref="ChangePhoneNumber"/> directly.
	/// </summary>
	public CustomerAggregate UpdateDetails(string? name = null, string? email = null, string? phoneNumber = null)
	{
		if (name is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(name);
			if (name != Name)
				CustomerNameChanged(name);
		}

		if (email is not null)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(email);
			// Transform email: trim and lowercase
			email = email.Trim().ToLowerInvariant();
			CustomerEmailChanged(email);
		}

		if (phoneNumber is not null && phoneNumber != PhoneNumber)
			CustomerPhoneChanged(phoneNumber);

		return this;
	}

	public CustomerAggregate Deactivate()
	{
		if (!IsActive)
			return this;

		return CustomerDeactivated(isActive: false);
	}

	public CustomerAggregate Reactivate()
	{
		if (IsActive)
			return this;

		return CustomerReactivated(isActive: true);
	}

	[GenerateAggregateEvent]
	public partial CustomerAggregate CustomerRegistered(string name, string email, bool isActive);

	[GenerateAggregateEvent]
	public partial CustomerAggregate CustomerNameChanged(string name);

	[GenerateAggregateEvent]
	public partial CustomerAggregate CustomerEmailChanged(string email);

	[GenerateAggregateEvent]
	public partial CustomerAggregate CustomerPhoneChanged(string? phoneNumber);

	[GenerateAggregateEvent]
	public partial CustomerAggregate CustomerDeactivated(bool isActive);

	[GenerateAggregateEvent]
	public partial CustomerAggregate CustomerReactivated(bool isActive);
}
