using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Purview.EventSourcing.SourceGenerator;

public sealed class AggregateSourceGeneratorTests : SourceGeneratorTestBase<AggregateSourceGenerator>
{
	const int ExpectedFileCount = 3;
	const int ExpectedFileCountPlusGen = ExpectedFileCount + 1;

	const int HintNameHashHexLength = 16;
	const string GeneratedSourceFileSuffix = ".g.cs";

	// Stub for AggregateBase so the source generator can find the base class
	const string AggregateBaseStub =
		@"#nullable enable

namespace Purview.EventSourcing.Aggregates
{
	public interface IAggregate
	{
	}

	public sealed class AggregateDetails
	{
		public string? Id { get; set; }
	}

	public abstract class AggregateBase : IAggregate
	{
		readonly System.Collections.Generic.Dictionary<System.Type, System.Delegate> _appliers = new();

		protected AggregateBase()
		{
			RegisterEvents();
		}

		public AggregateDetails Details { get; init; } = new();
		protected abstract void RegisterEvents();
		protected void Register<TEvent>(System.Action<TEvent> applier) where TEvent : class => _appliers[typeof(TEvent)] = applier;
		protected AggregateBase RecordAndApply<TEvent>(TEvent @event) where TEvent : class
		{
			((System.Action<TEvent>)_appliers[typeof(TEvent)])(@event);
			return this;
		}
	}
}

namespace Purview.EventSourcing.Aggregates.Events
{
	public sealed class EventDetails
	{
		public string? CorrelationId { get; set; }
	}

	public abstract class EventBase
	{
		public EventDetails Details { get; init; } = new();
		public virtual int SchemaVersion => 1;
		protected abstract void BuildEventHash(ref System.HashCode hash);
	}
}

";

	/// <summary>
	/// Helper to get the generated source text for the aggregate file (excludes attribute files).
	/// </summary>
	static string GetAggregateGeneratedSource(GeneratorDriverRunResult result)
	{
		var aggregateTree = result.GeneratedTrees.FirstOrDefault(t =>
			!t.FilePath.EndsWith("EmbeddedAttribute.cs", StringComparison.Ordinal)
			&& !t.FilePath.EndsWith("GenerateAggregateAttribute.g.cs", StringComparison.Ordinal)
			&& !t.FilePath.EndsWith("GenerateAggregateEventAttribute.g.cs", StringComparison.Ordinal)
			&& !t.FilePath.EndsWith("AggregateValidationAttribute.g.cs", StringComparison.Ordinal)
		);

		//Console.WriteLine(
		//    string.Join(
		//        Environment.NewLine,
		//        result.GeneratedTrees.Select(t => t.FilePath).ToArray()
		//    )
		//);

		return aggregateTree?.GetText().ToString() ?? string.Empty;
	}

	static Diagnostic[] GetGeneratorDiagnostics(GeneratorDriverRunResult result) =>
		[.. result.Results.SelectMany(static generatorResult => generatorResult.Diagnostics).OrderBy(static d => d.Id)];

	[Test]
	public async Task Generate_GivenEmptySource_GeneratesAttributesOnly(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	public class Empty { }
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCount);
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratesExpectedCode(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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

		// Assert — 3 attribute files + 1 generated aggregate file
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCountPlusGen);
	}

	[Test]
	public async Task Generate_GivenAggregateWithNoEvents_GeneratesEmptyRegisterEvents(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCountPlusGen);
	}

	[Test]
	public async Task Generate_GivenAggregateWithParameterlessEvent_GeneratesCorrectly(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCountPlusGen);
	}

	[Test]
	public async Task Generate_GivenNonPartialClass_DoesNotGenerate(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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

		// Assert — only attribute files, no generated aggregate
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCount);
		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE001");
	}

	[Test]
	public async Task Generate_GivenMultipleParameters_GeneratesAllProperties(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCountPlusGen);
	}

	[Test]
	public async Task Generate_ProducesNoDiagnosticErrors(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();

		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsEventClass(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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

		// Assert — event class uses the default namespace pattern
		await Assert.That(generatedSource).Contains("namespace Testing.Order");
		await Assert.That(generatedSource).Contains("public sealed class CreateOrderEvent");
		await Assert.That(generatedSource).Contains(": global::Purview.EventSourcing.Aggregates.Events.EventBase");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsJsonConverterSupport(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
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

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("JsonConverter(typeof(OrderAggregateJsonConverter))");
		await Assert
			.That(generatedSource)
			.Contains("internal static OrderAggregate CreateFromJsonModel(OrderAggregateJsonModel jsonModel)");
		await Assert.That(generatedSource).Contains("sealed class OrderAggregateJsonConverter");
		await Assert.That(generatedSource).Contains("sealed class OrderAggregateJsonModel");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsEventProperties(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsBuildEventHash(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert
			.That(generatedSource)
			.Contains("protected override void BuildEventHash(ref global::System.HashCode hash)");
		await Assert.That(generatedSource).Contains("hash.Add(Name);");
		await Assert.That(generatedSource).Contains("hash.Add(Count);");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsRegisterEvents(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.CreateOrderEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.UpdateOrderEvent>(Apply);");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsApplyMethods(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert.That(generatedSource).Contains("void Apply(global::Testing.OrderEvents.CreateOrderEvent @event)");
		await Assert.That(generatedSource).Contains("CustomerId = @event.CustomerId;");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsCommandMethod(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert
			.That(generatedSource)
			.Contains("public partial void CreateOrder(string customerId, decimal total)");
		await Assert.That(generatedSource).Contains("RecordAndApply(new global::Testing.OrderEvents.CreateOrderEvent");
		await Assert.That(generatedSource).Contains("CustomerId = customerId,");
		await Assert.That(generatedSource).Contains("Total = total,");
	}

	[Test]
	public async Task Generate_GivenStringMappedEvent_GeneratedSourceContainsOrdinalGuard(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ProfileAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Rename(string name);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("public partial void Rename(string name)");
		await Assert
			.That(generatedSource)
			.Contains("if (global::System.String.Equals(Name, name, global::System.StringComparison.Ordinal))");
	}

	[Test]
	public async Task Generate_GivenMultiPropertyEvent_GeneratedSourceContainsCompoundEqualityGuard(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ProductAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; } = default!;
		public int Quantity { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Update(string name, int quantity);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(generatedSource)
			.Contains(
				"global::System.String.Equals(Name, name, global::System.StringComparison.Ordinal) && global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(Quantity, quantity)"
			);
		await Assert.That(generatedSource).Contains("return;");
	}

	[Test]
	public async Task Generate_GivenAggregateReturningEventMethod_GeneratedSourceSupportsFluentChaining(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ProfileAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial ProfileAggregate Rename(string name);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("public partial ProfileAggregate Rename(string name)");
		await Assert
			.That(generatedSource)
			.Contains("if (global::System.String.Equals(Name, name, global::System.StringComparison.Ordinal))");
		await Assert.That(generatedSource).Contains("return this;");
	}

	[Test]
	public async Task Generate_GivenBoolReturningEventMethod_GeneratedSourceReturnsFalseWhenUnchanged(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ProfileAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial bool Rename(string name);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("public partial bool Rename(string name)");
		await Assert
			.That(generatedSource)
			.Contains("if (global::System.String.Equals(Name, name, global::System.StringComparison.Ordinal))");
		await Assert.That(generatedSource).Contains("return false;");
		await Assert.That(generatedSource).Contains("return true;");
	}

	[Test]
	public async Task Generate_GivenParameterlessEvent_GeneratedSourceContainsEmptyEventAndRecordAndApplyWithNew(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);
		var warnings = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Warning)
			.ToArray();

		// Assert — parameterless event uses () constructor
		await Assert.That(generatedSource).Contains("public partial void Increment()");
		await Assert.That(generatedSource).Contains("void Apply(global::Testing.CounterEvents.IncrementEvent _)");
		await Assert
			.That(generatedSource)
			.Contains("protected override void BuildEventHash(ref global::System.HashCode _)");
		await Assert
			.That(generatedSource)
			.Contains("RecordAndApply(new global::Testing.CounterEvents.IncrementEvent());");
		await Assert.That(warnings).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenImplicitlyConvertibleParameterType_GeneratedSourceUsesPropertyTypeForEqualityGuard(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	public readonly record struct Name
	{
		public string Value { get; }

		private Name(string value) => Value = value;

		public static implicit operator Name(string value) => new(value);
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class CustomerAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public Name Name { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void ChangeName(string name);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);
		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();

		await Assert
			.That(generatedSource)
			.Contains(
				"global::System.Collections.Generic.EqualityComparer<global::Testing.Name>.Default.Equals(Name, (global::Testing.Name)name)"
			);
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenPrivatePartialEventMethod_GeneratesPrivateMethodAndPublicEvent(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ToggleAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public bool IsActive { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		private partial ToggleAggregate ChangeIsActive(bool isActive);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);
		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();

		await Assert.That(generatedSource).Contains("private partial ToggleAggregate ChangeIsActive(bool isActive)");
		await Assert.That(generatedSource).Contains("public sealed class ChangeIsActiveEvent");
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenAggregateWithNoEvents_GeneratedSourceContainsEmptyRegisterEvents(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert.That(generatedSource).DoesNotContain("namespace Testing.EmptyEvents");
	}

	[Test]
	public async Task Generate_GivenMultipleEvents_GeneratedSourceContainsAllEventClasses(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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

	[Test]
	public async Task Generate_GivenClassNotInheritingAggregateBase_DoesNotGenerate(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCount);
		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE002");
	}

	[Test]
	public async Task Generate_GivenNonPartialMethod_MethodIsSkipped(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE007");
	}

	[Test]
	public async Task Generate_GivenInternalAggregate_GeneratesInternalAccessModifier(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
	public async Task Generate_GivenAttributeFiles_ContainsGenerateAggregateAttribute(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	public class Empty { }
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert — attribute files are generated
		var attributeSources = result
			.GeneratedTrees.Where(t =>
				t.FilePath.EndsWith("EmbeddedAttribute.cs", StringComparison.Ordinal)
				|| t.FilePath.EndsWith("GenerateAggregateAttribute.g.cs", StringComparison.Ordinal)
				|| t.FilePath.EndsWith("GenerateAggregateEventAttribute.g.cs", StringComparison.Ordinal)
			)
			.Select(t => t.GetText().ToString())
			.ToList();

		await Assert.That(attributeSources).Count().IsEqualTo(ExpectedFileCount);

		var allAttributeSource = string.Join("\n", attributeSources);
		await Assert.That(allAttributeSource).Contains("class EmbeddedAttribute");
		await Assert.That(allAttributeSource).Contains("class GenerateAggregateAttribute");
		await Assert.That(allAttributeSource).Contains("class GenerateAggregateEventAttribute");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_OutputCompilationHasNoErrors(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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
		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();

		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenGeneratedFile_HasAutoGeneratedHeader(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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

	[Test]
	public async Task Generate_GivenEventWithDefaultVersion_GeneratesSchemaVersionOverrideOfOne(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
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

		// Assert — default version is 1
		await Assert.That(generatedSource).Contains("public override int SchemaVersion => 1;");
	}

	[Test]
	public async Task Generate_GivenEventWithExplicitVersion_GeneratesCorrectSchemaVersionOverride(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(Version = 3)]
		public partial void CreateOrder(string customerId);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — explicit version 3
		await Assert.That(generatedSource).Contains("public override int SchemaVersion => 3;");
	}

	[Test]
	public async Task Generate_GivenMultipleEventsWithDifferentVersions_GeneratesCorrectSchemaVersionForEach(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; }
		public decimal Total { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateOrder(string customerId);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(Version = 2)]
		public partial void UpdateTotal(decimal total);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — both SchemaVersion overrides appear
		var v1Index = generatedSource.IndexOf("public override int SchemaVersion => 1;", StringComparison.Ordinal);
		var v2Index = generatedSource.IndexOf("public override int SchemaVersion => 2;", StringComparison.Ordinal);

		await Assert.That(v1Index).IsGreaterThanOrEqualTo(0);
		await Assert.That(v2Index).IsGreaterThanOrEqualTo(0);
		// They should appear in event-declaration order (CreateOrderEvent before UpdateTotalEvent)
		await Assert.That(v1Index).IsLessThan(v2Index);
	}

	[Test]
	public async Task Generate_GivenVersionedEvent_GeneratedAttributeTemplateContainsVersionProperty(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
namespace Testing
{
	public class Empty { }
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert — the generated attribute file exposes a Version property
		var attributeTree = result.GeneratedTrees.First(t =>
			t.FilePath.EndsWith("GenerateAggregateEventAttribute.g.cs", StringComparison.Ordinal)
		);
		var attributeSource = (await attributeTree.GetTextAsync(cancellationToken)).ToString();

		await Assert.That(attributeSource).Contains("int Version");
		await Assert.That(attributeSource).Contains("string? EventName");
		await Assert.That(attributeSource).Contains("string? EventNamespace");

		var aggregateAttributeTree = result.GeneratedTrees.First(t =>
			t.FilePath.EndsWith("GenerateAggregateAttribute.g.cs", StringComparison.Ordinal)
		);
		var aggregateAttributeSource = (await aggregateAttributeTree.GetTextAsync(cancellationToken)).ToString();
		await Assert.That(aggregateAttributeSource).Contains("string? EventNamespace");
	}

	[Test]
	public async Task Generate_GivenAggregateEventNamespaceOverride_UsesConfiguredNamespace(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate(EventNamespace = ""Testing.Custom.Events"")]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; } = string.Empty;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateOrder(string customerId);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("namespace Testing.Custom.Events");
		await Assert.That(generatedSource).Contains("Register<global::Testing.Custom.Events.CreateOrderEvent>(Apply);");
	}

	[Test]
	public async Task Generate_GivenEventNameAndNamespaceOverride_UsesMethodOverrides(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate(EventNamespace = ""Testing.Custom.Events"")]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; } = string.Empty;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""OrderCreated"", EventNamespace = ""Testing.Domain.Ordering"")]
		public partial void CreateOrder(string customerId);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("namespace Testing.Domain.Ordering");
		await Assert.That(generatedSource).Contains("public sealed class OrderCreated");
		await Assert.That(generatedSource).Contains("Register<global::Testing.Domain.Ordering.OrderCreated>(Apply);");
		await Assert.That(generatedSource).Contains("void Apply(global::Testing.Domain.Ordering.OrderCreated @event)");
	}

	[Test]
	public async Task Generate_GivenFalsePositiveAggregateBaseName_ReportsInheritanceDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public abstract class AggregateBase { }

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class NotARealAggregate : AggregateBase
	{
		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Rename(string name);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE002");
	}

	[Test]
	public async Task Generate_GivenNestedAggregate_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	public static class AggregateContainer
	{
		[Purview.EventSourcing.Aggregates.GenerateAggregate]
		public partial class NestedAggregate : Purview.EventSourcing.Aggregates.AggregateBase
		{
			public string Value { get; private set; } = default!;

			[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
			public partial void SetValue(string value);
		}
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE003");
	}

	[Test]
	public async Task Generate_GivenGenericAggregate_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class GenericAggregate<TValue> : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public TValue Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetValue(TValue value);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE004");
	}

	[Test]
	public async Task Generate_GivenManualRegisterEvents_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ManualRegistrationAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		protected override void RegisterEvents() { }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetValue(string value);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE005");
	}

	[Test]
	public async Task Generate_GivenEventMethodOutsideAggregate_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		const string source =
			@"
namespace Testing
{
	public partial class UtilityType
	{
		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void DoWork(string value);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE006");
	}

	[Test]
	public async Task Generate_GivenNonAggregateReturnTypeEventMethod_ReportsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class InvalidSignatureAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial string SetValue(string value);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE008");
		await Assert.That(generatedSource).Contains("public partial string SetValue(string value)");
		await Assert
			.That(
				outputCompilation
					.GetDiagnostics(cancellationToken)
					.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
					.Select(static diagnostic => diagnostic.Id)
			)
			.DoesNotContain("CS8795");
	}

	[Test]
	public async Task Generate_GivenUnsupportedEventMethodSignature_ReportsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class InvalidSignatureAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public static partial string SetValue(string value);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE008");
		await Assert.That(generatedSource).Contains("public static partial string SetValue(string value)");
		await Assert
			.That(generatedSource)
			.Contains(
				"The generated aggregate event method 'public static partial string SetValue(string value)' is unavailable because [GenerateAggregateEvent] validation failed. Review the suppressed generator diagnostics for this method (EVENTSTORE008)."
			);
		await Assert
			.That(
				outputCompilation
					.GetDiagnostics(cancellationToken)
					.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
					.Select(static diagnostic => diagnostic.Id)
			)
			.DoesNotContain("CS8795");
	}

	[Test]
	public async Task Generate_GivenOverloadedEventMethods_ReportsDuplicateEventNameDiagnostic(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class DuplicateEventAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;
		public int Count { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Update(string value);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Update(int count);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE009");
		await Assert.That(generatedSource).Contains("public partial void Update(string value)");
		await Assert.That(generatedSource).Contains("public partial void Update(int count)");
		await Assert.That(generatedSource).Contains("EVENTSTORE009");
		await Assert
			.That(
				outputCompilation
					.GetDiagnostics(cancellationToken)
					.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
					.Select(static diagnostic => diagnostic.Id)
			)
			.DoesNotContain("CS8795");
	}

	[Test]
	public async Task Generate_GivenMissingPropertyMapping_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class MappingAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Rename(string customerId);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE010");
		await Assert.That(generatedSource).Contains("public partial void Rename(string customerId)");
		await Assert
			.That(generatedSource)
			.Contains(
				"The generated aggregate event method 'public partial void Rename(string customerId)' is unavailable because [GenerateAggregateEvent] validation failed. Review the suppressed generator diagnostics for this method (EVENTSTORE010)."
			);
		await Assert
			.That(
				outputCompilation
					.GetDiagnostics(cancellationToken)
					.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
					.Select(static diagnostic => diagnostic.Id)
			)
			.DoesNotContain("CS8795");
	}

	[Test]
	public async Task Generate_GivenInitOnlyMappedProperty_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class InitOnlyAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; init; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetValue(string value);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE010");
	}

	[Test]
	public async Task Generate_GivenSameAggregateNameInDifferentNamespaces_UsesUniqueHintNames(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace First
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetValue(string value);
	}
}

namespace Second
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetValue(string value);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var aggregateTrees = ExcludeGenAttribs(result).Select(static tree => tree.FilePath).ToArray();
		var aggregateFileNames = aggregateTrees.Select(static path => Path.GetFileName(path)).ToArray();

		await Assert.That(aggregateTrees).Count().IsEqualTo(2);
		await Assert.That(aggregateTrees.Distinct(StringComparer.Ordinal)).Count().IsEqualTo(2);
		await Assert
			.That(
				aggregateFileNames.All(static fileName =>
					fileName.StartsWith("OrderAggregate_", StringComparison.Ordinal)
				)
			)
			.IsTrue();
		await Assert
			.That(
				aggregateFileNames.All(fileName =>
					fileName.Length
					== "OrderAggregate_".Length + HintNameHashHexLength + GeneratedSourceFileSuffix.Length
				)
			)
			.IsTrue();
	}

	[Test]
	public async Task Generate_GivenAggregateWithManyEvents_GeneratesAllEventsAndRegistrations(
		CancellationToken cancellationToken
	)
	{
		// Arrange — aggregate with 5 events covering full lifecycle
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; }
		public decimal Total { get; private set; }
		public string Status { get; private set; }
		public string ShippingAddress { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateOrder(string customerId);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void UpdateTotal(decimal total);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetShippingAddress(string shippingAddress);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void ConfirmOrder();

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CancelOrder();
	}
}
";

		// Act
		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — all 5 event classes
		await Assert.That(generatedSource).Contains("public sealed class CreateOrderEvent");
		await Assert.That(generatedSource).Contains("public sealed class UpdateTotalEvent");
		await Assert.That(generatedSource).Contains("public sealed class SetShippingAddressEvent");
		await Assert.That(generatedSource).Contains("public sealed class ConfirmOrderEvent");
		await Assert.That(generatedSource).Contains("public sealed class CancelOrderEvent");

		// Assert — all 5 Register calls
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.CreateOrderEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.UpdateTotalEvent>(Apply);");
		await Assert
			.That(generatedSource)
			.Contains("Register<global::Testing.OrderEvents.SetShippingAddressEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.ConfirmOrderEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.CancelOrderEvent>(Apply);");

		// Assert — compiles without errors
		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();

		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenEventWithMultipleParameterTypes_GeneratesCorrectProperties(
		CancellationToken cancellationToken
	)
	{
		// Arrange — event with int, decimal, string, bool parameters
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ProductAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; }
		public decimal Price { get; private set; }
		public int Quantity { get; private set; }
		public bool IsAvailable { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void UpdateProduct(string name, decimal price, int quantity, bool isAvailable);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — all parameter types are represented as properties
		await Assert.That(generatedSource).Contains("public string Name { get; set; }");
		await Assert.That(generatedSource).Contains("public decimal Price { get; set; }");
		await Assert.That(generatedSource).Contains("public int Quantity { get; set; }");
		await Assert.That(generatedSource).Contains("public bool IsAvailable { get; set; }");

		// Assert — Apply method sets all properties
		await Assert.That(generatedSource).Contains("Name = @event.Name;");
		await Assert.That(generatedSource).Contains("Price = @event.Price;");
		await Assert.That(generatedSource).Contains("Quantity = @event.Quantity;");
		await Assert.That(generatedSource).Contains("IsAvailable = @event.IsAvailable;");

		// Assert — BuildEventHash includes all properties
		await Assert.That(generatedSource).Contains("hash.Add(Name);");
		await Assert.That(generatedSource).Contains("hash.Add(Price);");
		await Assert.That(generatedSource).Contains("hash.Add(Quantity);");
		await Assert.That(generatedSource).Contains("hash.Add(IsAvailable);");
	}

	[Test]
	public async Task Generate_GivenAggregateWithTransitiveInheritance_GeneratesCode(
		CancellationToken cancellationToken
	)
	{
		// Arrange — aggregate inherits through an intermediate base class
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	public abstract class DomainAggregateBase : Purview.EventSourcing.Aggregates.AggregateBase
	{
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class AccountAggregate : DomainAggregateBase
	{
		public string AccountName { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateAccount(string accountName);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);

		// Assert — 3 trees: 2 attributes + 1 generated aggregate
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCountPlusGen);

		var generatedSource = GetAggregateGeneratedSource(result);
		await Assert.That(generatedSource).Contains("public sealed class CreateAccountEvent");
		await Assert
			.That(generatedSource)
			.Contains("Register<global::Testing.AccountEvents.CreateAccountEvent>(Apply);");
	}

	[Test]
	public async Task Generate_GivenNestedNamespace_GeneratesCorrectEventsNamespace(CancellationToken cancellationToken)
	{
		// Arrange — deeply nested namespace
		var source =
			AggregateBaseStub
			+ @"
namespace Company.Domain.Orders
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

		// Assert — events namespace follows the pattern
		await Assert.That(generatedSource).Contains("namespace Company.Domain.Orders.OrderEvents");
		await Assert
			.That(generatedSource)
			.Contains("Register<global::Company.Domain.Orders.OrderEvents.CreateOrderEvent>(Apply);");
		await Assert.That(generatedSource).Contains("namespace Company.Domain.Orders");
	}

	[Test]
	public async Task Generate_GivenParameterlessAndParameterizedEvents_GeneratesBothCorrectly(
		CancellationToken cancellationToken
	)
	{
		// Arrange — mix of parameterless and parameterized events
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class CounterAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public int Count { get; private set; }
		public string Label { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Increment();

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Decrement();

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetLabel(string label);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Reset();
	}
}
";

		// Act
		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — parameterless use () constructor, parameterized use { } initializer
		await Assert
			.That(generatedSource)
			.Contains("RecordAndApply(new global::Testing.CounterEvents.IncrementEvent());");
		await Assert
			.That(generatedSource)
			.Contains("RecordAndApply(new global::Testing.CounterEvents.DecrementEvent());");
		await Assert.That(generatedSource).Contains("RecordAndApply(new global::Testing.CounterEvents.ResetEvent());");
		await Assert.That(generatedSource).Contains("Label = label,");

		// Assert — all 4 Register calls
		await Assert.That(generatedSource).Contains("Register<global::Testing.CounterEvents.IncrementEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.CounterEvents.DecrementEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.CounterEvents.SetLabelEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.CounterEvents.ResetEvent>(Apply);");

		// Assert — compiles without errors
		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenNullableParameter_GeneratesNullableProperty(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ProfileAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string? Bio { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void UpdateBio(string? bio);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — nullable parameter generates property and Apply method
		await Assert.That(generatedSource).Contains("Bio");
		await Assert.That(generatedSource).Contains("Bio = @event.Bio;");
		await Assert.That(generatedSource).Contains("public sealed class UpdateBioEvent");
		await Assert.That(generatedSource).Contains("public string? Bio { get; set; } = default!;");
		await Assert.That(generatedSource).Contains("public partial void UpdateBio(string? bio)");
	}

	[Test]
	public async Task Generate_GivenPublicAccessibility_GeneratesPublicPartialClass(CancellationToken cancellationToken)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class PublicAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void DoAction();
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert
		await Assert.That(generatedSource).Contains("public partial class PublicAggregate");
	}

	[Test]
	public async Task Generate_GivenEventWithSingleParameter_CommandMethodHasCorrectSignature(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class NoteAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Content { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetContent(string content);
	}
}
";

		// Act
		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — command method matches partial declaration
		await Assert.That(generatedSource).Contains("public partial void SetContent(string content)");
		// Assert — RecordAndApply creates event with property
		await Assert.That(generatedSource).Contains("Content = content,");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_CanRoundTripPrivateSetterStateWithSystemTextJson(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; } = string.Empty;
		public decimal Total { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateOrder(string customerId, decimal total);
	}
}
";

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var aggregateType = assembly.GetType("Testing.OrderAggregate")!;
		var instance = Activator.CreateInstance(aggregateType)!;
		aggregateType.GetMethod("CreateOrder")!.Invoke(instance, ["customer-1", 12.5m]);

		var detailsProperty = aggregateType.GetProperty("Details")!;
		var detailsType = detailsProperty.PropertyType;
		var details = Activator.CreateInstance(detailsType)!;
		detailsType.GetProperty("Id")!.SetValue(details, "aggregate-1");
		detailsProperty.SetValue(instance, details);

		var json = JsonSerializer.Serialize(instance, aggregateType);
		var roundTripped = JsonSerializer.Deserialize(json, aggregateType)!;

		await Assert.That(aggregateType.GetProperty("CustomerId")!.GetValue(roundTripped)).IsEqualTo("customer-1");
		await Assert.That(aggregateType.GetProperty("Total")!.GetValue(roundTripped)).IsEqualTo(12.5m);
		var roundTrippedDetails = detailsProperty.GetValue(roundTripped)!;
		await Assert.That(detailsType.GetProperty("Id")!.GetValue(roundTrippedDetails)).IsEqualTo("aggregate-1");
	}

	[Test]
	public async Task Generate_GivenGeneratedEvent_CanRoundTripEventDetailsWithSystemTextJson(
		CancellationToken cancellationToken
	)
	{
		var source =
			AggregateBaseStub
			+ @"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class OrderAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; } = string.Empty;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void CreateOrder(string customerId);
	}
}
";

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var eventType = assembly.GetType("Testing.OrderEvents.CreateOrderEvent")!;
		var instance = Activator.CreateInstance(eventType)!;
		eventType.GetProperty("CustomerId")!.SetValue(instance, "customer-2");

		var detailsProperty = eventType.GetProperty("Details", BindingFlags.Public | BindingFlags.Instance)!;
		var detailsType = detailsProperty.PropertyType;
		var details = Activator.CreateInstance(detailsType)!;
		detailsType.GetProperty("CorrelationId")!.SetValue(details, "corr-1");
		detailsProperty.SetValue(instance, details);

		var json = JsonSerializer.Serialize(instance, eventType);
		var roundTripped = JsonSerializer.Deserialize(json, eventType)!;

		await Assert.That(eventType.GetProperty("CustomerId")!.GetValue(roundTripped)).IsEqualTo("customer-2");
		var roundTrippedDetails = detailsProperty.GetValue(roundTripped)!;
		await Assert.That(detailsType.GetProperty("CorrelationId")!.GetValue(roundTrippedDetails)).IsEqualTo("corr-1");
	}

	static IEnumerable<SyntaxTree> ExcludeGenAttribs(GeneratorDriverRunResult result)
	{
		return result.GeneratedTrees.Where(tree =>
			!tree.FilePath.EndsWith("EmbeddedAttribute.cs", StringComparison.Ordinal)
			&& !tree.FilePath.EndsWith("GenerateAggregateAttribute.g.cs", StringComparison.Ordinal)
			&& !tree.FilePath.EndsWith("GenerateAggregateEventAttribute.g.cs", StringComparison.Ordinal)
			&& !tree.FilePath.EndsWith("AggregateValidationAttribute.g.cs", StringComparison.Ordinal)
		);
	}
}
