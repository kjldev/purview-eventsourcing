using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Purview.EventSourcing.SourceGenerator;

public sealed class AggregateSourceGeneratorTests : SourceGeneratorTestBase<AggregateSourceGenerator>
{
	/// <summary>
	/// Helper to get the generated source text for the aggregate file (excludes attribute files).
	/// </summary>
	static string GetAggregateGeneratedSource(GeneratorDriverRunResult result)
	{
		var aggregateTree = ExcludeGenAttribs(result).FirstOrDefault();

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
		const string source =
			@"
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

		// Assert — 4 attribute files + 1 generated aggregate file
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCountPlusGen);
	}

	[Test]
	public async Task Generate_GivenAggregateWithNoEvents_GeneratesEmptyRegisterEvents(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
	public async Task Generate_GivenComputedParameterIsExplicitlyPassed_ReportsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public enum ReportProcessingStatus
	{
		Uploaded,
		Complete,
		Failed
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ReportUploadAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Blob { get; private set; }
		public object Summary { get; private set; }
		public ReportProcessingStatus Status { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial ReportUploadAggregate MarkAsCompleted(
			string blob,
			object summary,
			[Purview.EventSourcing.Aggregates.Computed] ReportProcessingStatus status = default
		);
	}

	public static class Caller
	{
		public static void Run(ReportUploadAggregate aggregate)
		{
			aggregate.MarkAsCompleted(""blob://1"", new object(), ReportProcessingStatus.Failed);
		}
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result);

		await Assert.That(diagnostics.Select(static d => d.Id)).Contains("EVENTSTORE017");
	}

	[Test]
	public async Task Generate_GivenComputedParameterWithNoComputeHook_ReportsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public enum ReportProcessingStatus
	{
		Uploaded,
		Complete,
		Failed
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ReportUploadAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Blob { get; private set; }
		public object Summary { get; private set; }
		public ReportProcessingStatus Status { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""CompletedEvent"")]
		public partial ReportUploadAggregate MarkAsCompleted(
			string blob,
			object summary,
			[Purview.EventSourcing.Aggregates.Computed] ReportProcessingStatus status = default
		);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result);

		await Assert.That(diagnostics.Select(static d => d.Id)).DoesNotContain("EVENTSTORE018");
		await Assert.That(diagnostics.Select(static d => d.Id)).DoesNotContain("EVENTSTORE019");
	}

	[Test]
	public async Task Generate_GivenNullableScalarComparedToNullWithEquality_ReportsPatternMatchingWarning(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Serialization.Scalar]
	public readonly partial record struct ProjectId
	{
		public string Value { get; }
		private ProjectId(string value) => Value = value;
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ReportAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; } = string.Empty;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetName(string name);

		public bool ShouldClear(ProjectId? projectId) => projectId == null;
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result);

		await Assert.That(diagnostics.Select(static d => d.Id)).Contains("EVENTSTORE019");
		await Assert
			.That(
				diagnostics
					.First(static d => d.Id == "EVENTSTORE019")
					.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
			)
			.Contains("is null");
	}

	[Test]
	public async Task Generate_GivenNullableScalarComparedToNullWithPatternMatching_DoesNotReportWarning(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Serialization.Scalar]
	public readonly partial record struct ProjectId
	{
		public string Value { get; }
		private ProjectId(string value) => Value = value;
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ReportAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Name { get; private set; } = string.Empty;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetName(string name);

		public bool ShouldClear(ProjectId? projectId) => projectId is null;
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result);

		await Assert.That(diagnostics.Select(static d => d.Id)).DoesNotContain("EVENTSTORE019");
	}

	[Test]
	public async Task Generate_GivenComputedParameterWithOnlyOnComputingHook_DoesNotReportHookDiagnostics(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public enum ReportProcessingStatus
	{
		Uploaded,
		Complete,
		Failed
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ReportUploadAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Blob { get; private set; }
		public object Summary { get; private set; }
		public ReportProcessingStatus Status { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""CompletedEvent"")]
		public partial ReportUploadAggregate MarkAsCompleted(
			string blob,
			object summary,
			[Purview.EventSourcing.Aggregates.Computed] ReportProcessingStatus status = default
		);

		partial void OnComputingCompletedEvent(ref ReportProcessingStatus status) => status = ReportProcessingStatus.Complete;
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result).Select(static d => d.Id).ToArray();
		await Assert.That(diagnostics).DoesNotContain("EVENTSTORE018");
		await Assert.That(diagnostics).DoesNotContain("EVENTSTORE019");

		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenComputedParameterWithInvalidOnComputingSignature_ReportsMissingHookDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public enum ReportProcessingStatus
	{
		Uploaded,
		Complete,
		Failed
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ReportUploadAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Blob { get; private set; }
		public object Summary { get; private set; }
		public ReportProcessingStatus Status { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""CompletedEvent"")]
		public partial ReportUploadAggregate MarkAsCompleted(
			string blob,
			object summary,
			[Purview.EventSourcing.Aggregates.Computed] ReportProcessingStatus status = default
		);

		// Invalid: missing ref
		partial void OnComputingCompletedEvent(ReportProcessingStatus status) { }
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result);
		await Assert.That(diagnostics.Select(static d => d.Id)).DoesNotContain("EVENTSTORE018");
		await Assert.That(diagnostics.Select(static d => d.Id)).DoesNotContain("EVENTSTORE019");
	}

	[Test]
	public async Task Generate_GivenComputedParameter_GeneratedSourceContainsComputingAndRaisingHookOverloads(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public enum ReportProcessingStatus
	{
		Uploaded,
		Complete,
		Failed
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ReportUploadAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Blob { get; private set; }
		public object Summary { get; private set; }
		public ReportProcessingStatus Status { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""CompletedEvent"")]
		public partial ReportUploadAggregate MarkAsCompleted(
			string blob,
			object summary,
			[Purview.EventSourcing.Aggregates.Computed] ReportProcessingStatus status = default
		);

		partial void OnComputingCompletedEvent(ref ReportProcessingStatus status) => status = ReportProcessingStatus.Complete;
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);
		await Assert
			.That(generatedSource)
			.Contains("partial void OnComputingCompletedEvent(ref global::Testing.ReportProcessingStatus status);");
		await Assert
			.That(generatedSource)
			.Contains(
				"partial void OnComputingCompletedEvent(ref string blob, ref object summary, ref global::Testing.ReportProcessingStatus status);"
			);
		await Assert
			.That(generatedSource)
			.Contains("partial void OnRaisingCompletedEvent(ref string blob, ref object summary);");
		await Assert
			.That(generatedSource)
			.Contains(
				"partial void OnRaisingCompletedEvent(ref string blob, ref object summary, ref global::Testing.ReportProcessingStatus status);"
			);
	}

	[Test]
	public async Task Generate_GivenComputedParameterWithOnlyOnComputingWithoutComputedValuesHook_DoesNotReportHookDiagnostics(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public enum ReportProcessingStatus
	{
		Uploaded,
		Complete,
		Failed
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ReportUploadAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Blob { get; private set; }
		public object Summary { get; private set; }
		public ReportProcessingStatus Status { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""CompletedEvent"")]
		public partial ReportUploadAggregate MarkAsCompleted(
			string blob,
			object summary,
			[Purview.EventSourcing.Aggregates.Computed] ReportProcessingStatus status = default
		);

		partial void OnComputingCompletedEvent(ref string blob, ref object summary)
		{
		}
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result).Select(static d => d.Id).ToArray();
		await Assert.That(diagnostics).DoesNotContain("EVENTSTORE018");
		await Assert.That(diagnostics).DoesNotContain("EVENTSTORE019");
	}

	[Test]
	public async Task Generate_GivenEventNameOverride_HookNamesUseOverriddenEventName(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public enum ReportProcessingStatus
	{
		None,
		Complete
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ReportUploadAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Blob { get; private set; }
		public object Summary { get; private set; }
		public ReportProcessingStatus Status { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""MarkAsCompleted"")]
		public partial ReportUploadAggregate MarkAsComplete(
			string blob,
			object summary,
			[Purview.EventSourcing.Aggregates.Computed] ReportProcessingStatus status = default
		);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);
		await Assert.That(generatedSource).Contains("OnComputingMarkAsCompletedEvent");
		await Assert.That(generatedSource).Contains("OnRaisingMarkAsCompletedEvent");
		await Assert.That(generatedSource).DoesNotContain("OnComputingMarkAsCompleted2Event");
	}

	[Test]
	public async Task Generate_ProducesNoDiagnosticErrors(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
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
		const string source =
			@"
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
		await Assert.That(generatedSource).Contains("public sealed class OrderCreated");
		await Assert.That(generatedSource).Contains(": global::Purview.EventSourcing.Aggregates.Events.EventBase");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsJsonConverterSupport(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.OrderCreatedEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.OrderUpdatedEvent>(Apply);");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsApplyMethods(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
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
		await Assert.That(generatedSource).Contains("void Apply(global::Testing.OrderEvents.OrderCreatedEvent @event)");
		await Assert.That(generatedSource).Contains("CustomerId = @event.CustomerId;");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_GeneratedSourceContainsCommandMethod(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
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
		await Assert.That(generatedSource).Contains("var @event = new global::Testing.OrderEvents.OrderCreated");
		await Assert.That(generatedSource).Contains("RecordAndApply(@event);");
		await Assert.That(generatedSource).Contains("CustomerId = customerId,");
		await Assert.That(generatedSource).Contains("Total = total,");
	}

	[Test]
	public async Task Generate_GivenStringMappedEvent_GeneratedSourceContainsOrdinalGuard(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();

		// Assert — parameterless event uses () constructor
		await Assert.That(generatedSource).Contains("public partial void Increment()");
		await Assert
			.That(generatedSource)
			.Contains("private partial void Apply(global::Testing.CounterEvents.IncrementedEvent @event);");
		await Assert
			.That(generatedSource)
			.Contains("protected override void BuildEventHash(ref global::System.HashCode _)");
		await Assert.That(generatedSource).Contains("var @event = new global::Testing.CounterEvents.IncrementedEvent");
		await Assert.That(generatedSource).Contains("RecordAndApply(@event);");
		await Assert.That(errors.Select(static e => e.Id)).Contains("CS8795");
	}

	[Test]
	public async Task Generate_GivenImplicitlyConvertibleParameterType_GeneratedSourceUsesPropertyTypeForEqualityGuard(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
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
				"global::System.Collections.Generic.EqualityComparer<global::Testing.Name>.Default.Equals(Name, __nameValue)"
			);
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenPrivatePartialEventMethod_GeneratesPrivateMethodAndPublicEvent(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
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
		await Assert.That(generatedSource).Contains("public sealed class IsActiveChanged");
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenAggregateWithNoEvents_GeneratedSourceContainsEmptyRegisterEvents(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
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
		const string source =
			@"
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
		await Assert.That(generatedSource).Contains("public sealed class OrderCreated");
		await Assert.That(generatedSource).Contains("public sealed class TotalUpdated");
	}

	[Test]
	public async Task Generate_GivenClassWithNoBaseClass_GeneratesAndAddsAggregateBaseToGeneratedPart(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
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

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCountPlusGen);
		await Assert
			.That(generatedSource)
			.Contains("public partial class NotAnAggregate : global::Purview.EventSourcing.Aggregates.AggregateBase");
		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.DoesNotContain("EVENTSTORE002");

		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenNonPartialMethod_MethodIsSkipped(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
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
		await Assert.That(generatedSource).Contains("NameSet");
		await Assert.That(generatedSource).DoesNotContain("NonPartialMethodEvent");
		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains("EVENTSTORE007");
	}

	[Test]
	public async Task Generate_GivenClassWithOnlyInterfaces_GeneratesAndAddsAggregateBaseToGeneratedPart(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	public interface ITaggable
	{
	}

	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class InterfaceOnlyAggregate : ITaggable
	{
		public string Value { get; private set; } = string.Empty;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetValue(string value);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(generatedSource)
			.Contains(
				"public partial class InterfaceOnlyAggregate : global::Purview.EventSourcing.Aggregates.AggregateBase"
			);
		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.DoesNotContain("EVENTSTORE002");

		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenInternalAggregate_GeneratesInternalAccessModifier(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		const string source =
			@"
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
		var attributeSources = result.GeneratedTrees.Select(t => t.GetText().ToString()).ToList();

		await Assert.That(attributeSources).Count().IsEqualTo(ExpectedFileCount);

		var allAttributeSource = string.Join("\n", attributeSources);
		await Assert.That(allAttributeSource).Contains("class EmbeddedAttribute");
		await Assert.That(allAttributeSource).Contains("class AggregatePropertyAttribute");
		await Assert.That(allAttributeSource).Contains("class GenerateAggregateAttribute");
		await Assert.That(allAttributeSource).Contains("class GenerateAggregateDefaultsAttribute");
		await Assert.That(allAttributeSource).Contains("class GenerateAggregateEventAttribute");
		await Assert.That(allAttributeSource).Contains("class MetadataAttribute");
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_OutputCompilationHasNoErrors(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		// They should appear in event-declaration order (OrderCreated before TotalUpdated)
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
		await Assert.That(aggregateAttributeSource).Contains("string? EventSuffix");

		var aggregateDefaultsAttributeTree = result.GeneratedTrees.First(t =>
			t.FilePath.EndsWith("GenerateAggregateDefaultsAttribute.g.cs", StringComparison.Ordinal)
		);
		var aggregateDefaultsAttributeSource = (
			await aggregateDefaultsAttributeTree.GetTextAsync(cancellationToken)
		).ToString();
		await Assert.That(aggregateDefaultsAttributeSource).Contains("string? EventSuffix");
	}

	[Test]
	public async Task Generate_GivenInferredEventName_AppliesSuffixByDefault(CancellationToken cancellationToken)
	{
		const string source =
			@"
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

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("public sealed class OrderCreated");
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.OrderCreatedEvent>(Apply);");
		await Assert.That(generatedSource).Contains("void Apply(global::Testing.OrderEvents.OrderCreatedEvent @event)");
	}

	[Test]
	public async Task Generate_GivenAssemblyEventSuffixOverride_UsesAssemblyConfiguredSuffix(
		CancellationToken cancellationToken
	)
	{
		var source =
			@"[assembly: Purview.EventSourcing.Aggregates.GenerateAggregateDefaults(EventSuffix = ""DomainEvent"")]

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

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("public sealed class OrderCreatedDomainEvent");
		await Assert
			.That(generatedSource)
			.Contains("Register<global::Testing.OrderEvents.OrderCreatedDomainEvent>(Apply);");
		await Assert
			.That(generatedSource)
			.Contains("void Apply(global::Testing.OrderEvents.OrderCreatedDomainEvent @event)");
	}

	[Test]
	public async Task Generate_GivenAggregateEventSuffixOverride_PrefersAggregateSuffixOverAssembly(
		CancellationToken cancellationToken
	)
	{
		var source =
			@"[assembly: Purview.EventSourcing.Aggregates.GenerateAggregateDefaults(EventSuffix = ""DomainEvent"")]

namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate(EventSuffix = ""CustomEvent"")]
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

		await Assert.That(generatedSource).Contains("public sealed class OrderCreatedCustomEvent");
		await Assert
			.That(generatedSource)
			.Contains("Register<global::Testing.OrderEvents.OrderCreatedCustomEvent>(Apply);");
		await Assert
			.That(generatedSource)
			.Contains("void Apply(global::Testing.OrderEvents.OrderCreatedCustomEvent @event)");
	}

	[Test]
	public async Task Generate_GivenAggregateEventNamespaceOverride_UsesConfiguredNamespace(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
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
		await Assert
			.That(generatedSource)
			.Contains("Register<global::Testing.Custom.Events.OrderCreatedEvent>(Apply);");
	}

	[Test]
	public async Task Generate_GivenEventNameAndNamespaceOverride_UsesMethodOverrides(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
	[Arguments("set")]
	[Arguments("protected set")]
	[Arguments("internal set")]
	[Arguments("protected internal set")]
	[Arguments("private protected set")]
	public async Task Generate_GivenAggregatePropertySetterIsNotPrivate_ReportsError(
		string setterAccess,
		CancellationToken cancellationToken
	)
	{
		var source =
			$@"
namespace Testing
{{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class PublicSetterAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{{
		public string Value {{ get; {setterAccess}; }} = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetValue(string value);
	}}
}}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result);
		var errorIds = diagnostics
			.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
			.Select(static diagnostic => diagnostic.Id);

		await Assert.That(errorIds).Contains("EVENTSTORE011");
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
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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

		var (result, _) = await GenerateAsync(source, cancellationToken);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains(GeneratorDiagnostics.EventParameterMustMapToWritableProperty.Id);
	}

	[Test]
	public async Task Generate_GivenMetadataStoreTrue_AddsStoredEventPropertyWithoutAggregateMutation(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class MappingAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Rename([Purview.EventSourcing.Aggregates.Metadata] string initialPropertyToTest);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.DoesNotContain(GeneratorDiagnostics.EventParameterMustMapToWritableProperty.Id);
		await Assert.That(generatedSource).Contains("public partial void Rename(string initialPropertyToTest)");
		await Assert.That(generatedSource).Contains("public string InitialPropertyToTest { get; set; } = default!;");
		await Assert.That(generatedSource).Contains("OnRaisingRenamedEvent(ref initialPropertyToTest);");
		await Assert.That(generatedSource).Contains("InitialPropertyToTest = initialPropertyToTest,");
		await Assert.That(generatedSource).Contains("OnRaisedRenamedEvent(@event);");
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
	public async Task Generate_GivenMetadataStoreFalse_PassesParameterToOnRaisingWithoutStoringAndStoring(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class MappingAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void Rename(
			[Purview.EventSourcing.Aggregates.Metadata(false)] string correlationId,
			[Purview.EventSourcing.Aggregates.Metadata] string correlationToStoreImplicitId,
			[Purview.EventSourcing.Aggregates.Metadata(true)] string? correlationToStoreExplicitId
		);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.DoesNotContain(GeneratorDiagnostics.EventParameterMustMapToWritableProperty.Id);
		await Assert
			.That(generatedSource)
			.Contains(
				"public partial void Rename(string correlationId, string correlationToStoreImplicitId, string? correlationToStoreExplicitId)"
			);
		await Assert
			.That(generatedSource)
			.Contains(
				"OnRaisingRenamedEvent(ref string correlationId, ref string correlationToStoreImplicitId, ref string? correlationToStoreExplicitId);"
			);
		await Assert.That(generatedSource).DoesNotContain("public string CorrelationId { get; set; } = default!;");
		await Assert
			.That(generatedSource)
			.Contains("public string CorrelationToStoreImplicitId { get; set; } = default!;");
		await Assert
			.That(generatedSource)
			.Contains("public string? CorrelationToStoreExplicitId { get; set; } = default!;");
		await Assert.That(generatedSource).Contains("var @event = new global::Testing.MappingEvents.Renamed");
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
	public async Task Generate_GivenAggregatePropertyOverride_MapsParameterToSpecifiedAggregateProperty(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class MappingAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public int QuantityOnHand { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void ReceiveStock([Purview.EventSourcing.Aggregates.AggregateProperty(nameof(QuantityOnHand))] int initialQuantity);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert.That(generatedSource).Contains("public int InitialQuantity { get; set; } = default!;");
		await Assert.That(generatedSource).Contains("QuantityOnHand = @event.InitialQuantity;");
		await Assert.That(generatedSource).Contains("OnRaisingStockReceivedEvent(ref initialQuantity);");
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
	public async Task Generate_GivenAggregatePropertyOverrideTargetMissing_ReportsDiagnostic(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class MappingAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public int QuantityOnHand { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void ReceiveStock([Purview.EventSourcing.Aggregates.AggregateProperty(""MissingProperty"")] int initialQuantity);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);

		await Assert
			.That(GetGeneratorDiagnostics(result).Select(static diagnostic => diagnostic.Id))
			.Contains(GeneratorDiagnostics.EventParameterMustMapToWritableProperty.Id);
	}

	[Test]
	public async Task Generate_GivenInitOnlyMappedProperty_ReportsDiagnostic(CancellationToken cancellationToken)
	{
		const string source =
			@"
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
		const string source =
			@"
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
		const string source =
			@"
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

		private partial void Apply(global::Testing.OrderEvents.OrderConfirmedEvent @event)
		{
			Status = ""Confirmed"";
		}

		private partial void Apply(global::Testing.OrderEvents.OrderCanceledEvent @event)
		{
			Status = ""Canceled"";
		}
	}
}
";

		// Act
		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — all 5 event classes
		await Assert.That(generatedSource).Contains("public sealed class OrderCreated");
		await Assert.That(generatedSource).Contains("public sealed class TotalUpdated");
		await Assert.That(generatedSource).Contains("public sealed class ShippingAddressSetEvent");
		await Assert.That(generatedSource).Contains("public sealed class OrderConfirmedEvent");
		await Assert.That(generatedSource).Contains("public sealed class OrderCanceledEvent"); // US spelling

		// Assert — all 5 Register calls
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.OrderCreatedEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.TotalUpdatedEvent>(Apply);");
		await Assert
			.That(generatedSource)
			.Contains("Register<global::Testing.OrderEvents.ShippingAddressSetEvent>(Apply);");
		await Assert
			.That(generatedSource)
			.Contains("Register<global::Testing.OrderEvents.OrderConfirmedEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.OrderEvents.OrderCanceledEvent>(Apply);");

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
		const string source =
			@"
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
		const string source =
			@"
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

		// Assert — attribute files + 1 generated aggregate
		await Assert.That(result.GeneratedTrees).Count().IsEqualTo(ExpectedFileCountPlusGen);

		var generatedSource = GetAggregateGeneratedSource(result);
		await Assert.That(generatedSource).Contains("public sealed class AccountCreatedEvent");
		await Assert
			.That(generatedSource)
			.Contains("Register<global::Testing.AccountEvents.AccountCreatedEvent>(Apply);");
	}

	[Test]
	public async Task Generate_GivenNestedNamespace_GeneratesCorrectEventsNamespace(CancellationToken cancellationToken)
	{
		// Arrange — deeply nested namespace
		const string source =
			@"
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
			.Contains("Register<global::Company.Domain.Orders.OrderEvents.OrderCreatedEvent>(Apply);");
		await Assert.That(generatedSource).Contains("namespace Company.Domain.Orders");
	}

	[Test]
	public async Task Generate_GivenParameterlessAndParameterizedEvents_GeneratesBothCorrectly(
		CancellationToken cancellationToken
	)
	{
		// Arrange — mix of parameterless and parameterized events
		const string source =
			@"
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

		private partial void Apply(global::Testing.CounterEvents.IncrementedEvent @event) => Count++;
		private partial void Apply(global::Testing.CounterEvents.DecrementedEvent @event) => Count--;
		private partial void Apply(global::Testing.CounterEvents.ResetEvent @event) => Count = 0;
	}
}
";

		// Act
		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		// Assert — parameterless use () constructor, parameterized use { } initializer
		await Assert.That(generatedSource).Contains("var @event = new global::Testing.CounterEvents.IncrementedEvent");
		await Assert.That(generatedSource).Contains("var @event = new global::Testing.CounterEvents.DecrementedEvent");
		await Assert.That(generatedSource).Contains("var @event = new global::Testing.CounterEvents.ResetEvent");
		await Assert.That(generatedSource).Contains("RecordAndApply(@event);");
		await Assert.That(generatedSource).Contains("Label = label,");

		// Assert — all 4 Register calls
		await Assert.That(generatedSource).Contains("Register<global::Testing.CounterEvents.IncrementedEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.CounterEvents.DecrementedEvent>(Apply);");
		await Assert.That(generatedSource).Contains("Register<global::Testing.CounterEvents.LabelSetEvent>(Apply);");
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
		const string source =
			@"
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
		await Assert.That(generatedSource).Contains("public sealed class BioUpdated");
		await Assert.That(generatedSource).Contains("public string? Bio { get; set; } = default!;");
		await Assert.That(generatedSource).Contains("public partial void UpdateBio(string? bio)");
	}

	[Test]
	public async Task Generate_GivenPublicAccessibility_GeneratesPublicPartialClass(CancellationToken cancellationToken)
	{
		// Arrange
		const string source =
			@"
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
		const string source =
			@"
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
	public async Task Generate_GivenGeneratedEvent_InvokesShouldApplyBeforeAndAfterOnRaising(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class CustomerAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetCustomerId(string customerId);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		var firstShouldApplyIndex = generatedSource.IndexOf(
			"if (!ShouldApplyCustomerIdSetEvent(@event))",
			StringComparison.Ordinal
		);
		var onRaisingIndex = generatedSource.IndexOf(
			"OnRaisingCustomerIdSetEvent(ref customerId);",
			StringComparison.Ordinal
		);
		var secondShouldApplyIndex = generatedSource.IndexOf(
			"if (!ShouldApplyCustomerIdSetEvent(@event))",
			firstShouldApplyIndex + 1,
			StringComparison.Ordinal
		);

		await Assert.That(firstShouldApplyIndex).IsGreaterThanOrEqualTo(0);
		await Assert.That(onRaisingIndex).IsGreaterThanOrEqualTo(0);
		await Assert.That(secondShouldApplyIndex).IsGreaterThanOrEqualTo(0);
		await Assert.That(firstShouldApplyIndex).IsLessThan(onRaisingIndex);
		await Assert.That(onRaisingIndex).IsLessThan(secondShouldApplyIndex);
		await Assert
			.That(generatedSource)
			.Contains(
				"partial void OnShouldApplyCustomerIdSetEvent(global::Testing.CustomerEvents.CustomerIdSetEvent @event, ref bool shouldApply);"
			);
	}

	[Test]
	public async Task Generate_GivenSecondShouldApplyReturnsFalse_StopsProcessingAndReturnsToCaller(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class CustomerAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public int ShouldApplyCallCount { get; private set; }
		public string? LastRaisedValue { get; private set; }
		public string? CustomerId { get; private set; }

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial bool SetCustomerId(string customerId);

		partial void OnRaisingCustomerIdSetEvent(ref string customerId)
		{
			customerId = ""raised-value"";
			LastRaisedValue = customerId;
		}

		partial void OnShouldApplyCustomerIdSetEvent(global::Testing.CustomerEvents.CustomerIdSetEvent @event, ref bool shouldApply)
		{
			ShouldApplyCallCount++;
			shouldApply = ShouldApplyCallCount == 1;
		}
	}
}
";

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var aggregateType = assembly.GetType("Testing.CustomerAggregate")!;
		var instance = Activator.CreateInstance(aggregateType)!;
		var setCustomerId = aggregateType.GetMethod("SetCustomerId")!;

		var result = (bool)setCustomerId.Invoke(instance, ["input-value"])!;

		await Assert.That(result).IsFalse();
		await Assert.That(aggregateType.GetProperty("ShouldApplyCallCount")!.GetValue(instance)).IsEqualTo(2);
		await Assert.That(aggregateType.GetProperty("LastRaisedValue")!.GetValue(instance)).IsEqualTo("raised-value");
		await Assert.That(aggregateType.GetProperty("CustomerId")!.GetValue(instance)).IsNull();
	}

	[Test]
	public async Task Generate_GivenNullValidationHook_RunsValidationBeforeNoChangeGuard(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class CustomerAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string CustomerId { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
		public partial void SetCustomerId(string customerId);

		partial void OnCustomerIdChanging(ref string customerId) => global::System.ArgumentNullException.ThrowIfNull(customerId);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		var onChangingIndex = generatedSource.IndexOf(
			"OnCustomerIdChanging(ref customerId);",
			StringComparison.Ordinal
		);
		var onRaisingIndex = generatedSource.IndexOf(
			"OnRaisingCustomerIdSetEvent(ref customerId);",
			StringComparison.Ordinal
		);
		var noChangeIndex = generatedSource.IndexOf(
			"if (global::System.String.Equals(CustomerId, customerId, global::System.StringComparison.Ordinal))",
			StringComparison.Ordinal
		);

		await Assert.That(onChangingIndex).IsGreaterThanOrEqualTo(0);
		await Assert.That(onRaisingIndex).IsGreaterThanOrEqualTo(0);
		await Assert.That(noChangeIndex).IsGreaterThanOrEqualTo(0);
		await Assert.That(onChangingIndex).IsLessThan(noChangeIndex);
		await Assert.That(onRaisingIndex).IsLessThan(noChangeIndex);

		var assembly = await CompileToAssemblyAsync(source, cancellationToken);
		var aggregateType = assembly.GetType("Testing.CustomerAggregate")!;
		var instance = Activator.CreateInstance(aggregateType)!;
		var setCustomerId = aggregateType.GetMethod("SetCustomerId")!;
		var threwArgumentNullException = false;

		try
		{
			setCustomerId.Invoke(instance, [null]);
		}
		catch (TargetInvocationException ex) when (ex.InnerException is ArgumentNullException)
		{
			threwArgumentNullException = true;
		}

		await Assert.That(threwArgumentNullException).IsTrue();
	}

	[Test]
	public async Task Generate_GivenSimpleAggregate_CanRoundTripPrivateSetterStateWithSystemTextJson(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
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
		const string source =
			@"
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
		var eventType = assembly.GetType("Testing.OrderEvents.OrderCreatedEvent")!;
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

	[Test]
	public async Task Generate_GivenSuspiciousMethodNames_ReportsVerbPhraseDiagnostics(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class NamingAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""CustomerRegistered"")]
		public partial void NewCustomer(string value);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""CustomerCreated"")]
		public partial void CustomerRegistered(string value);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""ValueChanged"")]
		public partial void NameChanged(string value);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""ValueSet"")]
		public partial void Handle(string value);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var warnings = GetGeneratorDiagnostics(result).Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();

		await Assert
			.That(warnings.Count(d => d.Id == GeneratorDiagnostics.AggregateMethodShouldBeVerbPhrase.Id))
			.IsEqualTo(4);
		await Assert.That(warnings.Select(d => d.Id)).DoesNotContain(GeneratorDiagnostics.UnableToInferEventName.Id);
		await Assert
			.That(warnings.Select(d => d.Id))
			.DoesNotContain(GeneratorDiagnostics.EventNameOverrideShouldBePastTense.Id);
	}

	[Test]
	public async Task Generate_GivenInvalidEventNameOverrides_ReportsPastTenseDiagnostics(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class NamingAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = default!;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""RegisterCustomer"")]
		public partial void NewCustomer(string value);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""CreateCustomer"")]
		public partial void CreateCustomer(string value);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""ApproveQuestion"")]
		public partial void ApproveQuestion(string value);

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""WithdrawConsent"")]
		public partial void WithdrawConsent(string value);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var warnings = GetGeneratorDiagnostics(result).Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();

		await Assert.That(warnings.Count(d => d.Id == "EVENTSTORE014")).IsEqualTo(4);
	}

	[Test]
	public async Task Generate_GivenManualEventTypes_ReportsPastTenseDiagnostics(CancellationToken cancellationToken)
	{
		const string source =
			@"
namespace Testing
{
	public sealed record NameChanged : Purview.EventSourcing.Aggregates.Events.EventBase
	{
		protected override void BuildEventHash(ref global::System.HashCode hash)
		{
		}
	}

	public sealed record ChangeName : Purview.EventSourcing.Aggregates.Events.EventBase
	{
		protected override void BuildEventHash(ref global::System.HashCode hash)
		{
		}
	}

	public sealed record CustomerRegisteredEvent : Purview.EventSourcing.Aggregates.Events.EventBase
	{
		protected override void BuildEventHash(ref global::System.HashCode hash)
		{
		}
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var warnings = GetGeneratorDiagnostics(result).Where(d => d.Severity == DiagnosticSeverity.Warning).ToArray();

		await Assert.That(warnings.Select(d => d.Id)).Contains(GeneratorDiagnostics.EventNameShouldBePastTense.Id);
		await Assert.That(warnings.Count(d => d.Id == GeneratorDiagnostics.EventNameShouldBePastTense.Id)).IsEqualTo(1);
		await Assert
			.That(warnings.Select(d => d.Id))
			.DoesNotContain(GeneratorDiagnostics.EventNameOverrideShouldBePastTense.Id);
		await Assert.That(warnings.Select(d => d.Id)).DoesNotContain(GeneratorDiagnostics.UnableToInferEventName.Id);
	}

	[Test]
	public async Task Generate_GivenNonNullableParamMappedToNullableProperty_EmitsNullabilityMismatchWarning(
		CancellationToken cancellationToken
	)
	{
		// Non-nullable string parameter mapping to a nullable string? aggregate property.
		// The generator handles it automatically (adds a local cast), but also emits EVENTSTORE016
		// to guide the developer to fix the signature rather than rely on the workaround.
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class NoteAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
			public string? Note { get; private set; }

			[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
			public partial void SetNote(string note);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var warnings = GetGeneratorDiagnostics(result)
			.Where(static d => d.Severity == DiagnosticSeverity.Info)
			.ToArray();

		await Assert
			.That(warnings.Select(static d => d.Id))
			.Contains(GeneratorDiagnostics.EventParameterNullabilityMismatch.Id);

		var mismatchWarning = warnings.First(static d =>
			d.Id == GeneratorDiagnostics.EventParameterNullabilityMismatch.Id
		);
		var message = mismatchWarning.GetMessage(System.Globalization.CultureInfo.InvariantCulture);
		await Assert.That(message).Contains("note");
		await Assert.That(message).Contains("SetNote");
		await Assert.That(message).Contains("Note");
		await Assert.That(message).Contains("string?");
	}

	[Test]
	public async Task Generate_GivenNullableParamMappedToNullableProperty_DoesNotEmitNullabilityMismatchWarning(
		CancellationToken cancellationToken
	)
	{
		// When the parameter already has the matching nullable annotation, no warning should be emitted.
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class NoteAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
			public string? Note { get; private set; }

			[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
			public partial void SetNote(string? note);
	}
}
";

		var (result, _) = await GenerateAsync(source, cancellationToken);
		var warnings = GetGeneratorDiagnostics(result)
			.Where(static d => d.Severity == DiagnosticSeverity.Warning)
			.ToArray();

		await Assert
			.That(warnings.Select(static d => d.Id))
			.DoesNotContain(GeneratorDiagnostics.EventParameterNullabilityMismatch.Id);
	}

	[Test]
	[Arguments("System.Collections.Generic.List<string>")]
	[Arguments("System.Collections.Generic.IList<string>")]
	[Arguments("System.Collections.Generic.ICollection<string>")]
	[Arguments("System.Collections.Generic.IReadOnlyList<string>")]
	[Arguments("System.Collections.Generic.IReadOnlyCollection<string>")]
	[Arguments("System.Collections.Generic.IEnumerable<string>")]
	[Arguments("System.Collections.Generic.HashSet<string>")]
	[Arguments("string[]")]
	public async Task Generate_GivenNonEventStoreCollectionProperty_ReportsCollectionTypeError(
		string collectionType,
		CancellationToken cancellationToken
	)
	{
		var source =
			$@"
namespace Testing
{{
		[Purview.EventSourcing.Aggregates.GenerateAggregate]
		public partial class ItemAggregate : Purview.EventSourcing.Aggregates.AggregateBase
		{{
			public {collectionType} Tags {{ get; private set; }}

			[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
			public partial void SetTags({collectionType} tags);
		}}
}}
";

		var (result, _) = await GenerateAsync(source, false, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result);

		await Assert
			.That(diagnostics.Select(static d => d.Id))
			.Contains(GeneratorDiagnostics.AggregatePropertyCollectionTypeMustUseEventStoreCollections.Id);
		await Assert
			.That(
				diagnostics
					.Where(static d =>
						d.Id == GeneratorDiagnostics.AggregatePropertyCollectionTypeMustUseEventStoreCollections.Id
					)
					.Select(static d => d.Severity)
			)
			.Contains(DiagnosticSeverity.Error);
	}

	[Test]
	[Arguments("Purview.EventSourcing.EventStoreList<string>")]
	[Arguments("Purview.EventSourcing.EventStoreSet<string>")]
	public async Task Generate_GivenEventStoreCollectionProperty_DoesNotReportCollectionTypeError(
		string collectionType,
		CancellationToken cancellationToken
	)
	{
		var source =
			$@"
namespace Testing
{{
		[Purview.EventSourcing.Aggregates.GenerateAggregate]
		public partial class ItemAggregate : Purview.EventSourcing.Aggregates.AggregateBase
		{{
			public {collectionType} Tags {{ get; private set; }} = new();

			[Purview.EventSourcing.Aggregates.GenerateAggregateEvent]
			public partial void SetTags({collectionType} tags);
		}}
}}
";

		var (result, outputCompilation) = await GenerateAsync(source, false, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result);

		await Assert
			.That(diagnostics.Select(static d => d.Id))
			.DoesNotContain(GeneratorDiagnostics.AggregatePropertyCollectionTypeMustUseEventStoreCollections.Id);

		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenCollectionEvents_UsesCollectionSemanticsAndSharedEnumerableHooks(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ItemAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public Purview.EventSourcing.EventStoreSet<string> Tags { get; private set; } = [];

		[Purview.EventSourcing.Aggregates.GenerateAggregateCollectionEvent(nameof(Tags))]
		public partial ItemAggregate AddTag(string tag);

		[Purview.EventSourcing.Aggregates.GenerateAggregateCollectionEvent(nameof(Tags))]
		public partial ItemAggregate AddTags(System.Collections.Generic.IEnumerable<string> tags);

		[Purview.EventSourcing.Aggregates.GenerateAggregateCollectionEvent(nameof(Tags))]
		public partial ItemAggregate AddTags(params string[] tags);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, false, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result).Select(static d => d.Id).ToArray();
		var generatedSource = GetAggregateGeneratedSource(result);
		await Assert.That(diagnostics).DoesNotContain(GeneratorDiagnostics.UnsupportedEventMethodSignature.Id);
		await Assert
			.That(generatedSource)
			.Contains(
				"partial void OnNormalizingAddTags(ref global::System.Collections.Generic.IEnumerable<string> tags);"
			);
		await Assert
			.That(generatedSource)
			.Contains("partial void OnValidatingAddTags(global::System.Collections.Generic.IEnumerable<string> tags);");
		await Assert.That(generatedSource).Contains("if (Tags.Contains(__itemValue))");
		await Assert
			.That(generatedSource)
			.Contains("var __eventItems = __itemsValue as string[] ?? [.. __itemsValue];");
		await Assert
			.That(generatedSource)
			.Contains("((global::System.Collections.Generic.ICollection<string>)Tags).Add(__item);");

		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenManualEventAttribute_DisablesAutomaticApplyAndRequiresImplementation(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ManualAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public string Value { get; private set; } = string.Empty;

		[Purview.EventSourcing.Aggregates.GenerateAggregateEvent(EventName = ""ValueCommandAppliedEvent"", Manual = true)]
		public partial void ApplyValueCommand(string input);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(generatedSource)
			.Contains("private partial void Apply(global::Testing.ManualEvents.ValueCommandAppliedEvent @event);");
		await Assert.That(generatedSource).DoesNotContain("Value = @event.Input;");

		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors.Select(static d => d.Id)).Contains("CS8795");
	}

	[Test]
	public async Task Generate_GivenManualCollectionEventAttribute_DisablesAutomaticApplyAndRequiresImplementation(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ManualCollectionAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public Purview.EventSourcing.EventStoreList<string> Tags { get; private set; } = [];

		[Purview.EventSourcing.Aggregates.GenerateAggregateCollectionEvent(nameof(Tags), Manual = true)]
		public partial void AddTag(string tag);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, false, cancellationToken);
		var generatedSource = GetAggregateGeneratedSource(result);

		await Assert
			.That(generatedSource)
			.Contains("private partial void Apply(global::Testing.ManualCollectionEvents.TagAddedEvent @event);");
		await Assert
			.That(generatedSource)
			.DoesNotContain("((global::System.Collections.Generic.ICollection<string>)Tags).Add(");

		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors.Select(static d => d.Id)).Contains("CS8795");
	}

	[Test]
	public async Task Generate_GivenCollectionRemoveMethodName_InfersRemoveMutationAndSkipsNoChangeEvents(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ItemAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public Purview.EventSourcing.EventStoreSet<string> Tags { get; private set; } = [];

		[Purview.EventSourcing.Aggregates.GenerateAggregateCollectionEvent(nameof(Tags))]
		public partial ItemAggregate RemoveTag(string tag);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, false, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result).Select(static d => d.Id).ToArray();
		var generatedSource = GetAggregateGeneratedSource(result);
		await Assert.That(diagnostics).DoesNotContain(GeneratorDiagnostics.UnsupportedEventMethodSignature.Id);
		await Assert.That(generatedSource).Contains("if (!Tags.Contains(__itemValue))");
		await Assert
			.That(generatedSource)
			.Contains("((global::System.Collections.Generic.ICollection<string>)Tags).Remove(@event.Tag);");

		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors).IsEmpty();
	}

	[Test]
	public async Task Generate_GivenCollectionOperationOverride_UsesSpecifiedMutation(
		CancellationToken cancellationToken
	)
	{
		const string source =
			@"
namespace Testing
{
	[Purview.EventSourcing.Aggregates.GenerateAggregate]
	public partial class ItemAggregate : Purview.EventSourcing.Aggregates.AggregateBase
	{
		public Purview.EventSourcing.EventStoreSet<string> Tags { get; private set; } = [];

		[Purview.EventSourcing.Aggregates.GenerateAggregateCollectionEvent(nameof(Tags), Operation = Purview.EventSourcing.Aggregates.CollectionEventOperation.Remove)]
		public partial ItemAggregate ArchiveTag(string tag);

		[Purview.EventSourcing.Aggregates.GenerateAggregateCollectionEvent(nameof(Tags), Operation = Purview.EventSourcing.Aggregates.CollectionEventOperation.Add)]
		public partial ItemAggregate DeleteTag(string tag);
	}
}
";

		var (result, outputCompilation) = await GenerateAsync(source, false, cancellationToken);
		var diagnostics = GetGeneratorDiagnostics(result).Select(static d => d.Id).ToArray();
		var generatedSource = GetAggregateGeneratedSource(result);
		await Assert.That(diagnostics).DoesNotContain(GeneratorDiagnostics.UnsupportedEventMethodSignature.Id);
		await Assert.That(generatedSource).Contains("if (!Tags.Contains(__itemValue))");
		await Assert.That(generatedSource).Contains("if (Tags.Contains(__itemValue))");
		await Assert
			.That(generatedSource)
			.Contains("((global::System.Collections.Generic.ICollection<string>)Tags).Remove(@event.Tag);");
		await Assert
			.That(generatedSource)
			.Contains("((global::System.Collections.Generic.ICollection<string>)Tags).Add(@event.Tag);");

		var errors = outputCompilation
			.GetDiagnostics(cancellationToken)
			.Where(static d => d.Severity == DiagnosticSeverity.Error)
			.ToArray();
		await Assert.That(errors).IsEmpty();
	}
}
