using Microsoft.CodeAnalysis;

namespace Purview.EventSourcing.SourceGenerator;

public class AggregateSourceGeneratorTests
	: SourceGeneratorTestBase<AggregateSourceGenerator>
{
	// Stub for AggregateBase so the source generator can find the base class
	const string AggregateBaseStub = @"
namespace Purview.EventSourcing.Aggregates
{
	public abstract class AggregateBase
	{
		protected abstract void RegisterEvents();
		protected void Register<TEvent>(System.Action<TEvent> applier) where TEvent : class { }
		protected AggregateBase RecordAndApply<TEvent>(TEvent @event) where TEvent : class => this;
	}
}

namespace Purview.EventSourcing.Aggregates.Events
{
	public abstract class EventBase
	{
		protected abstract void BuildEventHash(ref System.HashCode hash);
	}
}
";

	[Test]
	public async Task Generate_GivenEmptySource_GeneratesAttributesOnly(CancellationToken cancellationToken)
	{
		// Arrange
		const string source = @"
namespace Testing
{
	public class Empty { }
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratesExpectedCode(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; }
		public decimal Total { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateOrder(string customerId, decimal total);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void UpdateTotal(decimal total);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert — 2 attribute files + 1 generated aggregate file
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(3);
	}

	[Test]
	public async Task Generate_GivenAggregateWithNoEvents_GeneratesEmptyRegisterEvents(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class EmptyAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(3);
	}

	[Test]
	public async Task Generate_GivenAggregateWithParameterlessEvent_GeneratesCorrectly(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class CounterAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public int Count { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Increment();
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(3);
	}

	[Test]
	public async Task Generate_GivenNonPartialClass_DoesNotGenerate(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public class NonPartialAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		protected override void RegisterEvents() { }
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert — Only the 2 attribute files, no generated aggregate
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Generate_GivenMultipleParameters_GeneratesAllProperties(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ProductAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; }
		public decimal Price { get; private set; }
		public int Quantity { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetProduct(string name, decimal price, int quantity);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(3);
	}

	[Test]
	public async Task Generate_ProducesNoDiagnosticErrors(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class SimpleAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetValue(string value);
	}
}
";

		// Act
		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);

		// Assert — no generator exceptions
		foreach (var genResult in result.Results)
		{
			await Assert.That(genResult.Exception).IsNull();
		}

		// Assert — no errors in the output compilation (excluding pre-existing diagnostic warnings)
		var errors = outputCompilation.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();

		await Assert.That(errors).IsEmpty();
	}
}
