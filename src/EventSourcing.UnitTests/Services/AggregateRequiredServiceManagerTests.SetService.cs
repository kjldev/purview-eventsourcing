using Purview.EventSourcing.Aggregates.Test;

namespace Purview.EventSourcing.Services;

partial class AggregateRequiredServiceManagerTests
{
	[Test]
	public async Task Populate_GivenAggregateHasIRequirement_PopulatesService()
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<AggregateRequirementsTest>();
		var testService = CreateTestService();
		var serviceProvider = TestHelpers.ServiceProvider(new ServiceDefinition(typeof(ITestService), testService));

		var serviceManager = CreateServiceManager(serviceProvider);

		// Act
		serviceManager.Fulfil(aggregate);

		// Assert
		await Assert.That(aggregate.TestService).IsSameReferenceAs(testService);
	}

	[Test]
	public async Task Populate_GivenAggregateHasMultipleIRequirements_PopulatesServices()
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<AggregateMultipleRequirementsTest>();

		var testService = CreateTestService();
		var testService2 = CreateTestService2();

		var serviceProvider = TestHelpers.ServiceProvider(
			new ServiceDefinition(typeof(ITestService), testService),
			new ServiceDefinition(typeof(ITestService2), testService2)
		);

		var serviceManager = CreateServiceManager(serviceProvider);

		// Act
		serviceManager.Fulfil(aggregate);

		// Assert
		await Assert.That(aggregate.TestService).IsSameReferenceAs(testService);
		await Assert.That(aggregate.TestService2).IsSameReferenceAs(testService2);
	}

	[Test]
	public async Task Populate_GivenAggregateHasNoIRequirements_DoesNotThrow()
	{
		// Arrange
		var aggregate = TestHelpers.Aggregate<TestAggregate>();
		var serviceProvider = TestHelpers.ServiceProvider();

		var serviceManager = CreateServiceManager(serviceProvider);

		// Act
		var act = () => serviceManager.Fulfil(aggregate);

		// Assert
		await Assert.That(act).ThrowsNothing();
	}
}
