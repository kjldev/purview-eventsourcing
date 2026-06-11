using Microsoft.CodeAnalysis;

namespace Purview.EventSourcing.SourceGenerator;

static class GeneratorDiagnostics
{
	const string Category = "Purview.EventSourcing.SourceGenerator";

	public static readonly DiagnosticDescriptor AggregateMustBePartial = new(
		id: "EVENTSTORE001",
		title: "Aggregate must be partial",
		messageFormat: "Aggregate '{0}' must be declared partial to use [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor AggregateMustInheritAggregateBase = new(
		id: "EVENTSTORE002",
		title: "Aggregate must inherit AggregateBase",
		messageFormat: "Aggregate '{0}' must inherit from Purview.EventSourcing.Aggregates.AggregateBase to use [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NestedAggregatesAreNotSupported = new(
		id: "EVENTSTORE003",
		title: "Nested aggregates are not supported",
		messageFormat: "Aggregate '{0}' cannot be nested inside another type when using [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor GenericAggregatesAreNotSupported = new(
		id: "EVENTSTORE004",
		title: "Generic aggregates are not supported",
		messageFormat: "Aggregate '{0}' cannot be generic when using [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ManualRegisterEventsIsNotSupported = new(
		id: "EVENTSTORE005",
		title: "RegisterEvents is generated automatically",
		messageFormat: "Aggregate '{0}' cannot declare RegisterEvents() manually when using [GenerateAggregate]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EventMethodRequiresAggregateAttribute = new(
		id: "EVENTSTORE006",
		title: "GenerateAggregateEvent requires GenerateAggregate",
		messageFormat: "Method '{0}' must be declared on a [GenerateAggregate] aggregate type",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EventMethodMustBePartial = new(
		id: "EVENTSTORE007",
		title: "Generated event method must be partial",
		messageFormat: "Method '{0}' must be declared partial to use [GenerateAggregateEvent]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor UnsupportedEventMethodSignature = new(
		id: "EVENTSTORE008",
		title: "Unsupported generated event method signature",
		messageFormat: "Method '{0}' has an unsupported [GenerateAggregateEvent] signature: {1}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor DuplicateGeneratedEventName = new(
		id: "EVENTSTORE009",
		title: "Generated event names must be unique",
		messageFormat: "Method '{0}' on aggregate '{1}' conflicts with another [GenerateAggregateEvent] method because both would generate the event type '{2}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor EventParameterMustMapToWritableProperty = new(
		id: "EVENTSTORE010",
		title: "Generated event parameters must map to writable aggregate properties",
		messageFormat: "Parameter '{0}' on method '{1}' must map to a writable aggregate property on '{2}': {3}",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor AggregatePropertySetterShouldBePrivate = new(
		id: "EVENTSTORE011",
		title: "Aggregate property setters should be private",
		messageFormat: "Aggregate property '{0}' on '{1}' has a non-private setter ('{2}'). Event-sourced aggregate state should use private setters.",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ValueObjectMustBePartial = new(
		id: "EVENTSTORE101",
		title: "Value object must be partial",
		messageFormat: "Value object '{0}' must be declared partial to use [{1}]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor NestedValueObjectsAreNotSupported = new(
		id: "EVENTSTORE102",
		title: "Nested value objects are not supported",
		messageFormat: "Value object '{0}' cannot be nested when using [{1}]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor GenericValueObjectsAreNotSupported = new(
		id: "EVENTSTORE103",
		title: "Generic value objects are not supported",
		messageFormat: "Value object '{0}' cannot be generic when using [{1}]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ScalarPropertyMissing = new(
		id: "EVENTSTORE104",
		title: "Scalar property is missing",
		messageFormat: "Scalar value object '{0}' must declare readable property '{1}'",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ScalarConstructorMissing = new(
		id: "EVENTSTORE105",
		title: "Scalar constructor is missing",
		messageFormat: "Scalar value object '{0}' must declare a constructor '{0}({1})' to support generated Create/Hydrate",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ComplexHydrateConstructorMissing = new(
		id: "EVENTSTORE106",
		title: "Value object hydration constructor is missing",
		messageFormat: "Value object '{0}' must declare a constructor matching its generated Hydrate(...) parameter list",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor StrictDeserializationRequiresCreate = new(
		id: "EVENTSTORE107",
		title: "Strict mode requires Create",
		messageFormat: "Value object '{0}' uses strict deserialization mode but does not declare a compatible static Create(...) overload",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);

	public static readonly DiagnosticDescriptor ConflictingValueObjectAttributes = new(
		id: "EVENTSTORE108",
		title: "Conflicting value object attributes",
		messageFormat: "Type '{0}' cannot be annotated with both [Scalar] and [ValueObject]",
		category: Category,
		defaultSeverity: DiagnosticSeverity.Error,
		isEnabledByDefault: true
	);
}
