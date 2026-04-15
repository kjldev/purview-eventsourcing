using Microsoft.CodeAnalysis;

namespace Purview.EventSourcing.SourceGenerator;

sealed class AggregateInfo(
	string? namespaceName,
	string className,
	Accessibility accessibility,
	List<AggregateStatePropertyInfo> properties,
	List<AggregateEventMethodInfo> methods
	)
{
	public string? Namespace { get; } = namespaceName;
	public string ClassName { get; } = className;
	public Accessibility Accessibility { get; } = accessibility;
	public List<AggregateStatePropertyInfo> Properties { get; } = properties;
	public List<AggregateEventMethodInfo> Methods { get; } = methods;
}

sealed class AggregateStatePropertyInfo(string propertyName, string typeName)
{
	public string PropertyName { get; } = propertyName;
	public string TypeName { get; } = typeName;
}

sealed class AggregateEventMethodInfo(string methodName, List<EventPropertyInfo> parameters, int version = 1)
{
	public string MethodName { get; } = methodName;
	public string EventName { get; } = methodName + "Event";
	public List<EventPropertyInfo> Parameters { get; } = parameters;

	/// <summary>
	/// The schema version declared via <c>[GenerateAggregateEvent(Version = N)]</c>.
	/// Defaults to 1.
	/// </summary>
	public int Version { get; } = version;
}

sealed class EventPropertyInfo(string parameterName, string typeName)
{
	public string ParameterName { get; } = parameterName;
	public string PropertyName { get; } = string.IsNullOrEmpty(parameterName)
			? parameterName
			: char.ToUpperInvariant(parameterName[0]) + parameterName.Substring(1);
	public string TypeName { get; } = typeName;
}
