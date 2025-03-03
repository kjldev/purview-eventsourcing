using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Aggregates.Test;

namespace Purview.EventSourcing.Services;

public partial class AggregateRequiredServiceManagerTests
{
	static AggregateRequiredServiceManager CreateServiceManager(IServiceProvider serviceProvider)
		=> new(serviceProvider);

	static ITestService CreateTestService() => Substitute.For<ITestService>();

	static ITestService2 CreateTestService2() => Substitute.For<ITestService2>();

	class AggregateRequirementsTest : TestAggregate, IRequirement<ITestService>
	{
		public ITestService? TestService { get; set; }

		public void SetService(ITestService service)
			=> TestService = service;
	}

	class AggregateMultipleRequirementsTest : AggregateRequirementsTest, IRequirement<ITestService2>
	{
		public ITestService2? TestService2 { get; set; }

		public void SetService(ITestService2 service)
			=> TestService2 = service;
	}

	internal interface ITestService
	{
	}

	internal interface ITestService2
	{
	}
}
