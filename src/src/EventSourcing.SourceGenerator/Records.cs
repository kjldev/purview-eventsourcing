using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Purview.EventSourcing.SourceGenerator;

sealed class AggregateInfo(
	string? namespaceName,
	string className,
	Accessibility accessibility,
	bool shouldDeclareAggregateBase,
	List<AggregateStatePropertyInfo> properties,
	List<AggregateEventMethodInfo> methods,
	List<InvalidAggregateEventMethodInfo> invalidMethods,
	string hintName
)
{
	public string? Namespace { get; } = namespaceName;

	public string ClassName { get; } = className;

	public Accessibility Accessibility { get; } = accessibility;

	public bool ShouldDeclareAggregateBase { get; } = shouldDeclareAggregateBase;

	public List<AggregateStatePropertyInfo> Properties { get; } = properties;

	public List<AggregateEventMethodInfo> Methods { get; } = methods;

	public List<InvalidAggregateEventMethodInfo> InvalidMethods { get; } = invalidMethods;

	public string HintName { get; } = hintName;
}

sealed class AggregateStatePropertyInfo(string propertyName, string typeName)
{
	public string PropertyName { get; } = propertyName;

	public string TypeName { get; } = typeName;
}

sealed class AggregateEventMethodInfo(
	string methodName,
	string eventName,
	string eventNamespace,
	List<EventPropertyInfo> parameters,
	string returnTypeName,
	EventMethodReturnKind returnKind,
	Accessibility methodAccessibility,
	int version = 1,
	bool manualApply = false,
	CollectionEventInfo? collectionEvent = null
)
{
	public string MethodName { get; } = methodName;

	public string EventName { get; } = eventName;

	public string EventNamespace { get; } = eventNamespace;

	public List<EventPropertyInfo> Parameters { get; } = parameters;

	public string ReturnTypeName { get; } = returnTypeName;

	public EventMethodReturnKind ReturnKind { get; } = returnKind;

	public Accessibility MethodAccessibility { get; } = methodAccessibility;

	/// <summary>
	/// The schema version declared via <c>[GenerateAggregateEvent(Version = N)]</c>.
	/// Defaults to 1.
	/// </summary>
	public int Version { get; } = version;

	/// <summary>
	/// Indicates whether Apply(...) implementation is user-supplied and should not be auto-generated.
	/// </summary>
	public bool ManualApply { get; } = manualApply;

	/// <summary>
	/// Collection-event metadata when the method is decorated with [GenerateAggregateCollectionEvent].
	/// </summary>
	public CollectionEventInfo? CollectionEvent { get; } = collectionEvent;

	public bool IsCollectionEvent => CollectionEvent is not null;
}

sealed class CollectionEventInfo(
	string propertyName,
	string elementTypeName,
	string propertyTypeName,
	bool isSet,
	CollectionMutationOperation operation,
	CollectionParameterShape parameterShape,
	string normalizeValidateHookSuffix
)
{
	public string PropertyName { get; } = propertyName;

	public string ElementTypeName { get; } = elementTypeName;

	public string PropertyTypeName { get; } = propertyTypeName;

	public bool IsSet { get; } = isSet;

	public CollectionMutationOperation Operation { get; } = operation;

	public CollectionParameterShape ParameterShape { get; } = parameterShape;

	public string NormalizeValidateHookSuffix { get; } = normalizeValidateHookSuffix;
}

enum CollectionParameterShape
{
	Single = 0,
	Enumerable = 1,
	Array = 2,
}

enum CollectionMutationOperation
{
	Add = 0,
	Remove = 1,
}

sealed class InvalidAggregateEventMethodInfo(string signature, string[] diagnosticIds)
{
	public string Signature { get; } = signature;

	public string[] DiagnosticIds { get; } = diagnosticIds;
}

sealed class EventPropertyInfo(
	string parameterName,
	string parameterTypeName,
	string propertyTypeName,
	string aggregatePropertyName,
	bool hasAggregateProperty,
	bool includeInEvent,
	string equalityComparerTypeName,
	bool useStringOrdinalComparison,
	EventParameterConversionKind parameterConversionKind,
	bool isComputed = false,
	bool isParams = false
)
{
	public string ParameterName { get; } = parameterName;

	public string PropertyName { get; } = ToPropertyName(parameterName);

	public string ParameterTypeName { get; } = parameterTypeName;

	public string PropertyTypeName { get; } = propertyTypeName;

	public string AggregatePropertyName { get; } = aggregatePropertyName;

	public bool HasAggregateProperty { get; } = hasAggregateProperty;

	public bool IncludeInEvent { get; } = includeInEvent;

	public string EqualityComparerTypeName { get; } = equalityComparerTypeName;

	public bool UseStringOrdinalComparison { get; } = useStringOrdinalComparison;

	public EventParameterConversionKind ParameterConversionKind { get; } = parameterConversionKind;

	/// <summary>
	/// Indicates this property is marked with [Computed] attribute.
	/// Computed properties are not passed by the caller; instead, they are
	/// computed via OnComputingXxxEvent hook before event creation.
	/// </summary>
	public bool IsComputed { get; } = isComputed;

	public bool IsParams { get; } = isParams;

	public bool RequiresParameterToPropertyTypeConversion { get; } =
		parameterConversionKind is not EventParameterConversionKind.None;

	public static string ToPropertyName(string parameterName) =>
		string.IsNullOrEmpty(parameterName)
			? parameterName
			: char.ToUpperInvariant(parameterName[0]) + parameterName.Substring(1);
}

enum EventParameterConversionKind
{
	None = 0,
	Implicit = 1,
	Create = 2,
	ContextualCreate = 3,
}

sealed class AggregateGenerationResult(AggregateInfo? info, ImmutableArray<Diagnostic> diagnostics)
{
	public AggregateInfo? Info { get; } = info;

	public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
}

sealed class EventMethodValidationResult(ImmutableArray<Diagnostic> diagnostics)
{
	public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
}

sealed class EventTypeValidationResult(ImmutableArray<Diagnostic> diagnostics)
{
	public ImmutableArray<Diagnostic> Diagnostics { get; } = diagnostics;
}

readonly record struct AttributeStringValue(string? Value, bool IsPresent);

enum EventMethodReturnKind
{
	Void = 0,
	Aggregate = 1,
	Bool = 2,
}
