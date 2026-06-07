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
    public string Email { get; private set; } = default!;
    public string? PhoneNumber { get; private set; }
    public bool IsActive { get; private set; }

    // Commands
    public void RegisterCustomer(string name, string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        CustomerRegistered(name, email, isActive: true);
    }

    public void ChangeName(string newName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newName);
        if (newName == Name)
            return;

        CustomerNameChanged(newName);
    }

    public void ChangeEmail(string newEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);
        if (newEmail == Email)
            return;

        CustomerEmailChanged(newEmail);
    }

    public void ChangePhoneNumber(string? phoneNumber) => CustomerPhoneChanged(phoneNumber);

    /// <summary>
    /// Updates one or more customer details in a single operation, raising a granular event
    /// for each field that has actually changed. Pass <see langword="null"/> for any field
    /// that should remain unchanged. To clear the phone number, use <see cref="ChangePhoneNumber"/> directly.
    /// </summary>
    public void UpdateDetails(string? name = null, string? email = null, string? phoneNumber = null)
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
            if (email != Email)
                CustomerEmailChanged(email);
        }

        if (phoneNumber is not null && phoneNumber != PhoneNumber)
            CustomerPhoneChanged(phoneNumber);
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        CustomerDeactivated(isActive: false);
    }

    public void Reactivate()
    {
        if (IsActive)
            return;

        CustomerReactivated(isActive: true);
    }

    [GenerateAggregateEvent]
    public partial void CustomerRegistered(string name, string email, bool isActive);

    [GenerateAggregateEvent]
    public partial void CustomerNameChanged(string name);

    [GenerateAggregateEvent]
    public partial void CustomerEmailChanged(string email);

    [GenerateAggregateEvent]
    public partial void CustomerPhoneChanged(string? phoneNumber);

    [GenerateAggregateEvent]
    public partial void CustomerDeactivated(bool isActive);

    [GenerateAggregateEvent]
    public partial void CustomerReactivated(bool isActive);
}
