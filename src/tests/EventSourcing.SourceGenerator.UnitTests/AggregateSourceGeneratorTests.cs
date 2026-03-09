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

	/// <summary>
	/// Helper to get the generated source text for the aggregate file (excludes attribute files).
	/// </summary>
	static string GetAggregateGeneratedSource(GeneratorDriverRunResult result)
	{
		var aggregateTree = result.GeneratedTrees
			.FirstOrDefault(t =>
				!t.FilePath.EndsWith("GenerateAggregateAttribute.g.cs", StringComparison.Ordinal) &&
				!t.FilePath.EndsWith("GenerateAggregateEventAttribute.g.cs", StringComparison.Ordinal));

		return aggregateTree?.GetText().ToString() ?? string.Empty;
	}

	#region Tree Count Tests

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

	#endregion

	#region Generated Content Verification Tests

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsEventClass(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateOrder(string customerId);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — event class is generated in the Events namespace
		await Assert.That(generatedSource).Contains("namespace Testing.Events");
		await Assert.That(generatedSource).Contains("public sealed class CreateOrderEvent");
		await Assert.That(generatedSource).Contains(": global::Purview.EventSourcing.Aggregates.Events.EventBase");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsEventProperties(CancellationToken cancellationToken)
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
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — event properties are generated with PascalCase names
		await Assert.That(generatedSource).Contains("public string CustomerId { get; set; }");
		await Assert.That(generatedSource).Contains("public decimal Total { get; set; }");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsBuildEventHash(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; }
		public int Count { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetOrder(string name, int count);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — BuildEventHash adds each property
		await Assert.That(generatedSource).Contains("protected override void BuildEventHash(ref global::System.HashCode hash)");
		await Assert.That(generatedSource).Contains("hash.Add(Name);");
		await Assert.That(generatedSource).Contains("hash.Add(Count);");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsRegisterEvents(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateOrder(string customerId);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void UpdateOrder(string customerId);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — RegisterEvents contains Register calls for each event
		await Assert.That(generatedSource).Contains("protected override void RegisterEvents()");
		await Assert.That(generatedSource).Contains("Register<global::Testing.Events.CreateOrderEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.Events.UpdateOrderEvent>(Apply);");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsApplyMethods(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateOrder(string customerId);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — Apply method is generated with property assignments from event
		await Assert.That(generatedSource).Contains("void Apply(global::Testing.Events.CreateOrderEvent @event)");
		await Assert.That(generatedSource).Contains("CustomerId = @event.CustomerId;");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsCommandMethod(CancellationToken cancellationToken)
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
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — command method calls RecordAndApply with a new event
		await Assert.That(generatedSource).Contains("public partial void CreateOrder(string customerId, decimal total)");
		await Assert.That(generatedSource).Contains("RecordAndApply(new global::Testing.Events.CreateOrderEvent");
		await Assert.That(generatedSource).Contains("CustomerId = customerId,");
		await Assert.That(generatedSource).Contains("Total = total,");
	}

	[Test]
	public async Task Generate_GivenParameterlessEvent_GeneratedSourceContainsEmptyEventAndRecordAndApplyWithNew(CancellationToken cancellationToken)
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
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — parameterless event uses () constructor
		await Assert.That(generatedSource).Contains("public partial void Increment()");
		await Assert.That(generatedSource).Contains("RecordAndApply(new global::Testing.Events.IncrementEvent());");
	}

	[Test]
	public async Task Generate_GivenAggregateWithNoEvents_GeneratedSourceContainsEmptyRegisterEvents(CancellationToken cancellationToken)
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
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — RegisterEvents exists but has no Register calls
		await Assert.That(generatedSource).Contains("protected override void RegisterEvents()");
		// No Events namespace section should be generated
		await Assert.That(generatedSource).DoesNotContain("namespace Testing.Events");
	}

	[Test]
	public async Task Generate_GivenMultipleEvents_GeneratedSourceContainsAllEventClasses(CancellationToken cancellationToken)
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
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — both event classes exist
		await Assert.That(generatedSource).Contains("public sealed class CreateOrderEvent");
		await Assert.That(generatedSource).Contains("public sealed class UpdateTotalEvent");
	}

	#endregion

	#region Edge Case Tests

	[Test]
	public async Task Generate_GivenClassNotInheritingAggregateBase_DoesNotGenerate(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class NotAnAggregate
	{
		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void DoSomething(string value);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert — Only the 2 attribute files, no generated aggregate
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(2);
	}

	[Test]
	public async Task Generate_GivenNonPartialMethod_MethodIsSkipped(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class MixedAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetName(string name);

		// This method is NOT partial, so it should be ignored even though it has the attribute
		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public void NonPartialMethod(string value) { }
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — only the partial method generates an event, the non-partial is skipped
		await Assert.That(generatedSource).Contains("SetNameEvent");
		await Assert.That(generatedSource).DoesNotContain("NonPartialMethodEvent");
	}

	[Test]
	public async Task Generate_GivenInternalAggregate_GeneratesInternalAccessModifier(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	internal partial class InternalAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetValue(string value);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — the generated partial class uses 'internal' access modifier
		await Assert.That(generatedSource).Contains("internal partial class InternalAggregate");
	}

	[Test]
	public async Task Generate_GivenAttributeFiles_ContainsGenerateAggregateAttribute(CancellationToken cancellationToken)
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

		// Assert — attribute files are generated
		var attributeSources = result.GeneratedTrees
			.Where(t =>
				t.FilePath.EndsWith("GenerateAggregateAttribute.g.cs", StringComparison.Ordinal) ||
				t.FilePath.EndsWith("GenerateAggregateEventAttribute.g.cs", StringComparison.Ordinal))
			.Select(t => t.GetText().ToString())
			.ToList();

		await Assert.That(attributeSources).Count().IsEqualTo(2);

		var allAttributeSource = string.Join("\n", attributeSources);
		await Assert.That(allAttributeSource).Contains("class GenerateAggregateAttribute");
		await Assert.That(allAttributeSource).Contains("class GenerateAggregateEventAttribute");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_OutputCompilationHasNoErrors(CancellationToken cancellationToken)
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
		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);

		// Assert — no generator exceptions
		foreach (var genResult in result.Results)
		{
			await Assert.That(genResult.Exception).IsNull();
		}

		// Assert — no compilation errors
		var errors = outputCompilation.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();

		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenGeneratedFile_HasAutoGeneratedHeader(CancellationToken cancellationToken)
	{
		// Arrange
		var source = AggregateBaseStub + @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class SimpleAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void DoWork();
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — generated file starts with auto-generated header
		await Assert.That(generatedSource).Contains("// <auto-generated />");
		await Assert.That(generatedSource).Contains("#nullable enable");
	}

	#endregion
}
