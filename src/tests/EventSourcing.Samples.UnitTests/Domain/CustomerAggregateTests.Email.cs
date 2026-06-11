namespace Purview.EventSourcing.Samples.Domain;

partial class CustomerAggregateTests
{
	[Test]
	[Arguments("testing@eventsourcing-sample.com")]
	[Arguments("testing@eventsourcing-sample.co.uk")]
	[Arguments("testing@sub-domain.eventsourcing-sample.co.uk")]
	public async Task Email_WhenEmailIsSetToEmployeeDomain_ThrowsArgumentException(string email)
	{
		// Arrange
		var customer = new CustomerAggregate();

		// Act
		void Act() => customer.ChangeEmail(email);

		// Assert
		await Assert
			.That(Act)
			.Throws<ArgumentException>()
			.WithMessage("Employees of Event-Sourcing-Sample PLC cannot be customers", StringComparison.Ordinal);

		// Checking we have no unsaved changes.
		await Assert.That(customer.HasUnsavedEvents()).IsFalse();
	}
}
