using Microsoft.CodeAnalysis;

namespace Purview.EventSourcing.SourceGenerator;

static class GeneratorDiagnostics
{
	const string Category = "Purview.EventSourcing.SourceGenerator";

	public static readonly DiagnosticDescriptor AggregateMustBePartial = new(
		id: "PVEVTGEN001",
		title: "Aggregate must be partial",
		messageFormat: "Aggregate '{0}' must be declared partial to use [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor AggregateMustInheritAggregateBase = new(
		id: "PVEVTGEN002",
		title: "Aggregate must inherit AggregateBase",
		messageFormat: "Aggregate '{0}' must inherit from Purview.EventSourcing.Aggregates.AggregateBase to use [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NestedAggregatesAreNotSupported = new(
		id: "PVEVTGEN003",
		title: "Nested aggregates are not supported",
		messageFormat: "Aggregate '{0}' cannot be nested inside another type when using [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor GenericAggregatesAreNotSupported = new(
		id: "PVEVTGEN004",
		title: "Generic aggregates are not supported",
		messageFormat: "Aggregate '{0}' cannot be generic when using [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ManualRegisterEventsIsNotSupported = new(
		id: "PVEVTGEN005",
		title: "RegisterEvents is generated automatically",
		messageFormat: "Aggregate '{0}' cannot declare RegisterEvents() manually when using [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EventMethodRequiresAggregateAttribute = new(
		id: "PVEVTGEN006",
		title: "GenerateAggregateEvent requires GenerateAggregate",
		messageFormat: "Method '{0}' must be declared on a [GenerateAggregate] aggregate type",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EventMethodMustBePartial = new(
		id: "PVEVTGEN007",
		title: "Generated event method must be partial",
		messageFormat: "Method '{0}' must be declared partial to use [GenerateAggregateEvent]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UnsupportedEventMethodSignature = new(
		id: "PVEVTGEN008",
		title: "Unsupported generated event method signature",
		messageFormat: "Method '{0}' has an unsupported [GenerateAggregateEvent] signature: {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor DuplicateGeneratedEventName = new(
		id: "PVEVTGEN009",
		title: "Generated event names must be unique",
		messageFormat: "Method '{0}' on aggregate '{1}' conflicts with another [GenerateAggregateEvent] method because both would generate the event type '{2}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EventParameterMustMapToWritableProperty = new(
		id: "PVEVTGEN010",
		title: "Generated event parameters must map to writable aggregate properties",
		messageFormat: "Parameter '{0}' on method '{1}' must map to a writable aggregate property on '{2}': {3}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);
}
