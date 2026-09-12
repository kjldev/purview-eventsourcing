namespace Purview.EventSourcing.SourceGenerator.Aggregate.Models;

sealed record class AggregateInfo(
	TypeReference AggregateClass,
	Accessibility Accessibility,
	bool ShouldDeclareAggregateBase,
	EquatableArray<AggregateStatePropertyInfo> Properties,
	EquatableArray<AggregateEventMethodInfo> Methods,
	EquatableArray<InvalidAggregateEventMethodInfo> InvalidMethods,
	string HintName,
	bool IsValid = true,
	bool IsPartial = true,
	bool InheritsAggregateBase = false,
	bool HasManualRegisterEvents = false,
	EquatableArray<AggregateContainingTypeInfo> ContainingTypes = default,
	EquatableArray<GenericTypeParameterOptions> TypeParameters = default
);

sealed record class AggregateContainingTypeInfo(
	string Name,
	Accessibility Accessibility,
	bool IsStatic,
	EquatableArray<GenericTypeParameterOptions> TypeParameters = default
);

sealed record class AggregateStatePropertyInfo(string PropertyName, TypeReference PropertyType);

/// <param name="MethodName">The name of the method.</param>
/// <param name="EventType">The type of the event.</param>
/// <param name="AllParameters">All parameters of the event method.</param>
/// <param name="EventParameters">The parameters that are included in the event, i.e. get stored.</param>
/// <param name="ComputedParameters">The parameters that are computed.</param>
/// <param name="NonComputedParameters">The parameters that are not computed.</param>
/// <param name="AggregateProperties">The properties that have a type of aggregate.</param>
/// <param name="ReturnType">The return type of the method.</param>
/// <param name="ReturnKind">The kind of the return value.</param>
/// <param name="MethodAccessibility">The accessibility of the method.</param>
/// <param name="Version">
/// The schema version declared via <c>[Event(Version = N)]</c>.
/// Defaults to 1.
/// </param>
/// <param name="ManualApply">Indicates whether Apply(...) implementation is user-supplied via the [Manual] attribute and should not be auto-generated.</param>
/// <param name="UserApplyMethodKind">Indicates whether the user has defined a manual Apply method for this event.</param>
/// <param name="UserApplyMethodAccessibility">The accessibility of the user-defined Apply method.</param>
/// <param name="CollectionEvent">Collection-event metadata when the method is decorated with [CollectionEvent].</param>
/// <param name="Signature">Original method signature text, used for invalid-aggregate stubs.</param>
sealed record class AggregateEventMethodInfo(
	string MethodName,
	TypeReference EventType,
	EquatableArray<EventPropertyInfo> AllParameters,
	EquatableArray<EventPropertyInfo> EventParameters,
	EquatableArray<EventPropertyInfo> ComputedParameters,
	EquatableArray<EventPropertyInfo> NonComputedParameters,
	EquatableArray<EventPropertyInfo> AggregateProperties,
	TypeReference ReturnType,
	EventMethodReturnKind ReturnKind,
	Accessibility MethodAccessibility,
	int Version,
	bool ManualApply,
	UserApplyMethodKind UserApplyMethodKind,
	TypeDeclarationAccessibility? UserApplyMethodAccessibility,
	CollectionEventInfo? CollectionEvent,
	string Signature
)
{
	public bool IsCollectionEvent => CollectionEvent is not null;
}

sealed record class CollectionEventInfo(
	string PropertyName,
	TypeReference ElementType,
	TypeReference PropertyType,
	bool IsSet,
	CollectionMutationOperation Operation,
	CollectionParameterShape ParameterShape,
	string NormalizeValidateHookSuffix
);

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

enum UserApplyMethodKind
{
	None = 0,

	PartialImplementation = 1,

	NonPartial = 2,
}

sealed record class InvalidAggregateEventMethodInfo(string Signature, EquatableArray<string> DiagnosticIds);

/// <param name="ParameterName"></param>
/// <param name="ParameterType"></param>
/// <param name="PropertyType"></param>
/// <param name="AggregatePropertyName"></param>
/// <param name="HasAggregateProperty"></param>
/// <param name="IncludeInEvent"></param>
/// <param name="EqualityComparerTypeName"></param>
/// <param name="UseStringOrdinalComparison"></param>
/// <param name="ParameterConversionKind"></param>
/// <param name="IsComputed">
/// Indicates this property is marked with [Computed] attribute.
/// Computed properties are not passed by the caller; instead, they are
/// computed via OnComputingXxxEvent hook before event creation.
/// </param>
/// <param name="IsParams"></param>
/// <param name="IsNotNull"></param>
/// <param name="IsRequired"></param>
/// <param name="IsString"></param>
sealed record class EventPropertyInfo(
	string ParameterName,
	TypeReference ParameterType,
	TypeReference PropertyType,
	string AggregatePropertyName,
	bool HasAggregateProperty,
	bool IncludeInEvent,
	string EqualityComparerTypeName,
	bool UseStringOrdinalComparison,
	EventParameterConversionKind ParameterConversionKind,
	bool IsComputed = false,
	bool IsParams = false,
	bool IsNotNull = false,
	bool IsRequired = false,
	bool IsString = false
)
{
	public string PropertyName => ToPropertyName(ParameterName);

	public bool RequiresParameterToPropertyTypeConversion =>
		ParameterConversionKind is not EventParameterConversionKind.None;

	public bool RequiresLocalCopy =>
		IsComputed
		|| ParameterConversionKind is not EventParameterConversionKind.None
		|| (IsNotNull && PropertyType.IsNullable)
		|| (IsRequired && (PropertyType.IsNullable || IsString));

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

sealed record class EventMethodValidationResult(ImmutableArray<ReportableDiagnostic> Diagnostics);

sealed record class EventTypeValidationResult(ImmutableArray<ReportableDiagnostic> Diagnostics);

enum EventMethodReturnKind
{
	Void = 0,
	Aggregate = 1,
	Bool = 2,
}
