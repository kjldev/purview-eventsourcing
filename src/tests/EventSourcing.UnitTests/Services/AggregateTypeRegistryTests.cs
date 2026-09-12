using Microsoft.Extensions.DependencyInjection;
using Purview.EventSourcing.Aggregates.Persistence;
using Purview.EventSourcing.Aggregates.Persistence.Events;

namespace Purview.EventSourcing.Services;

public sealed class AggregateTypeRegistryTests
{
	[Test]
	public async Task TryResolve_GivenInitializedAggregate_ReturnsClrType()
	{
		ServiceCollection services = new();
		services.AddEventSourcing();
		using var provider = services.BuildServiceProvider();

		var mapper = provider.GetRequiredService<IAggregateEventNameMapper>();
		var aggregateName = mapper.InitializeAggregate<PersistenceAggregate>();

		var registry = provider.GetRequiredService<IAggregateTypeRegistry>();
		var resolved = registry.TryResolve(aggregateName, out var aggregateType);

		await Assert.That(resolved).IsTrue();
		await Assert.That(aggregateType).IsEqualTo(typeof(PersistenceAggregate));
	}

	[Test]
	public async Task TryResolve_GivenUnknownName_ReturnsFalse()
	{
		ServiceCollection services = new();
		services.AddEventSourcing();
		using var provider = services.BuildServiceProvider();

		var registry = provider.GetRequiredService<IAggregateTypeRegistry>();
		var resolved = registry.TryResolve("NoSuchAggregate", out _);

		await Assert.That(resolved).IsFalse();
	}

	[Test]
	public async Task GetTypeName_GivenRegisteredEventName_ReturnsAssemblyQualifiedTypeName()
	{
		ServiceCollection services = new();
		services.AddEventSourcing();
		using var provider = services.BuildServiceProvider();

		var mapper = provider.GetRequiredService<IAggregateEventNameMapper>();
		mapper.InitializeAggregate<PersistenceAggregate>();
		var eventName = mapper.GetName<PersistenceAggregate>(typeof(StringValueSetEvent));

		var resolved = mapper.GetTypeName(eventName);

		await Assert.That(resolved).IsNotNull();
		await Assert.That(resolved).Contains("StringValueSetEvent");
	}

	[Test]
	public async Task GetTypeName_GivenUnknownEventName_ReturnsNull()
	{
		ServiceCollection services = new();
		services.AddEventSourcing();
		using var provider = services.BuildServiceProvider();

		var mapper = provider.GetRequiredService<IAggregateEventNameMapper>();

		await Assert.That(mapper.GetTypeName("NotARegisteredEvent")).IsNull();
	}
}
