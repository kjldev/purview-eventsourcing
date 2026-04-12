using Purview.EventSourcing.Samples.Domain;
using Purview.EventSourcing.Samples.Domain.Events;

namespace Purview.EventSourcing.Samples.UnitTests.Domain;

public class CustomerAggregateTests
{
	static CustomerAggregate CreateCustomer(string? id = null)
	{
		var customer = new CustomerAggregate();
		if (id is not null)
			customer.Details.Id = id;
		return customer;
	}

	#region RegisterCustomer Tests

	[Test]
	public async Task RegisterCustomer_GivenValidData_SetsPropertiesCorrectly(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");

		// Act
		customer.RegisterCustomer("Jane Smith", "jane@test.com");

		// Assert
		await Assert.That(customer.Name).IsEqualTo("Jane Smith");
		await Assert.That(customer.Email).IsEqualTo("jane@test.com");
		await Assert.That(customer.IsActive).IsTrue();
	}

	[Test]
	public async Task RegisterCustomer_GivenValidData_RecordsOneEvent(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");

		// Act
		customer.RegisterCustomer("Jane Smith", "jane@test.com");

		// Assert
		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(1);
		await Assert.That(customer.GetUnsavedEvents().First()).IsTypeOf<CustomerRegisteredEvent>();
	}

	[Test]
	public void RegisterCustomer_GivenNullName_ThrowsArgumentException()
	{
		// Arrange
		var customer = CreateCustomer("cust-1");

		// Act & Assert
		Assert.Throws<ArgumentException>(() => customer.RegisterCustomer(null!, "email@test.com"));
	}

	[Test]
	public void RegisterCustomer_GivenEmptyEmail_ThrowsArgumentException()
	{
		// Arrange
		var customer = CreateCustomer("cust-1");

		// Act & Assert
		Assert.Throws<ArgumentException>(() => customer.RegisterCustomer("Name", ""));
	}

	#endregion

	#region ChangeName Tests

	[Test]
	public async Task ChangeName_GivenNewName_UpdatesName(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane Smith", "jane@test.com");

		// Act
		customer.ChangeName("Jane Doe");

		// Assert
		await Assert.That(customer.Name).IsEqualTo("Jane Doe");
	}

	[Test]
	public async Task ChangeName_GivenNewName_RecordsEvent(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane Smith", "jane@test.com");
		var countBefore = customer.GetUnsavedEvents().Count();

		// Act
		customer.ChangeName("Jane Doe");

		// Assert
		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(countBefore + 1);
	}

	[Test]
	public async Task ChangeName_GivenSameName_DoesNotRecordEvent(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane Smith", "jane@test.com");
		var countBefore = customer.GetUnsavedEvents().Count();

		// Act
		customer.ChangeName("Jane Smith");

		// Assert — no new event recorded
		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(countBefore);
	}

	[Test]
	public void ChangeName_GivenEmptyName_ThrowsArgumentException()
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane Smith", "jane@test.com");

		// Act & Assert
		Assert.Throws<ArgumentException>(() => customer.ChangeName("  "));
	}

	#endregion

	#region ChangeEmail Tests

	[Test]
	public async Task ChangeEmail_GivenNewEmail_UpdatesEmail(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane", "old@test.com");

		// Act
		customer.ChangeEmail("new@test.com");

		// Assert
		await Assert.That(customer.Email).IsEqualTo("new@test.com");
	}

	[Test]
	public async Task ChangeEmail_GivenSameEmail_DoesNotRecordEvent(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane", "same@test.com");
		var eventCountBefore = customer.GetUnsavedEvents().Count();

		// Act
		customer.ChangeEmail("same@test.com");

		// Assert — no new event recorded
		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(eventCountBefore);
	}

	[Test]
	public void ChangeEmail_GivenEmptyEmail_ThrowsArgumentException()
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane", "old@test.com");

		// Act & Assert
		Assert.Throws<ArgumentException>(() => customer.ChangeEmail("  "));
	}

	#endregion

	#region Deactivate/Reactivate Tests

	[Test]
	public async Task Deactivate_GivenActiveCustomer_SetsIsActiveFalse(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane", "jane@test.com");

		// Act
		customer.Deactivate();

		// Assert
		await Assert.That(customer.IsActive).IsFalse();
	}

	[Test]
	public async Task Deactivate_GivenAlreadyInactive_DoesNotRecordEvent(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane", "jane@test.com");
		customer.Deactivate();
		var eventCountBefore = customer.GetUnsavedEvents().Count();

		// Act
		customer.Deactivate();

		// Assert
		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(eventCountBefore);
	}

	[Test]
	public async Task Reactivate_GivenInactiveCustomer_SetsIsActiveTrue(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane", "jane@test.com");
		customer.Deactivate();

		// Act
		customer.Reactivate();

		// Assert
		await Assert.That(customer.IsActive).IsTrue();
	}

	#endregion

	#region UpdateDetails Tests

	[Test]
	public async Task UpdateDetails_GivenNameAndEmail_RaisesTwoEvents(CancellationToken cancellationToken)
	{
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane Smith", "jane@test.com");
		var countBefore = customer.GetUnsavedEvents().Count();

		customer.UpdateDetails(name: "Jane Doe", email: "janedoe@test.com");

		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(countBefore + 2);
		await Assert.That(customer.Name).IsEqualTo("Jane Doe");
		await Assert.That(customer.Email).IsEqualTo("janedoe@test.com");
	}

	[Test]
	public async Task UpdateDetails_GivenAllThreeFields_RaisesThreeEvents(CancellationToken cancellationToken)
	{
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane Smith", "jane@test.com");
		var countBefore = customer.GetUnsavedEvents().Count();

		customer.UpdateDetails(name: "Jane Doe", email: "janedoe@test.com", phoneNumber: "+44 7700 900123");

		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(countBefore + 3);
		await Assert.That(customer.Name).IsEqualTo("Jane Doe");
		await Assert.That(customer.Email).IsEqualTo("janedoe@test.com");
		await Assert.That(customer.PhoneNumber).IsEqualTo("+44 7700 900123");
	}

	[Test]
	public async Task UpdateDetails_GivenOnlyName_RaisesOneEvent(CancellationToken cancellationToken)
	{
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane Smith", "jane@test.com");
		var countBefore = customer.GetUnsavedEvents().Count();

		customer.UpdateDetails(name: "Jane Doe");

		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(countBefore + 1);
		await Assert.That(customer.Name).IsEqualTo("Jane Doe");
		await Assert.That(customer.Email).IsEqualTo("jane@test.com");
	}

	[Test]
	public async Task UpdateDetails_GivenSameValues_RaisesNoEvents(CancellationToken cancellationToken)
	{
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane Smith", "jane@test.com");
		var countBefore = customer.GetUnsavedEvents().Count();

		customer.UpdateDetails(name: "Jane Smith", email: "jane@test.com");

		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(countBefore);
	}

	[Test]
	public void UpdateDetails_GivenWhitespaceName_ThrowsArgumentException()
	{
		var customer = CreateCustomer("cust-1");
		customer.RegisterCustomer("Jane Smith", "jane@test.com");

		Assert.Throws<ArgumentException>(() => customer.UpdateDetails(name: "  "));
	}

	#endregion

	#region Version Tracking Tests

	[Test]
	public async Task MultipleOperations_TracksVersionCorrectly(CancellationToken cancellationToken)
	{
		// Arrange
		var customer = CreateCustomer("cust-1");

		// Act
		customer.RegisterCustomer("Jane", "jane@test.com");
		customer.ChangeEmail("new@test.com");
		customer.ChangePhoneNumber("+1-555-0100");

		// Assert — 3 events = version 3
		await Assert.That(customer.Details.CurrentVersion).IsEqualTo(3);
		await Assert.That(customer.GetUnsavedEvents().Count()).IsEqualTo(3);
	}

	#endregion
}
