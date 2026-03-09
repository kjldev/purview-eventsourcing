using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Purview.EventSourcing.SourceGenerator;

sealed class AggregateInfo
{
	public AggregateInfo(
		string? namespaceName,
		string className,
		Accessibility accessibility,
		List<AggregateEventMethodInfo> methods)
	{
		Namespace = namespaceName;
		ClassName = className;
		Accessibility = accessibility;
		Methods = methods;
	}

	public string? Namespace { get; }
	public string ClassName { get; }
	public Accessibility Accessibility { get; }
	public List<AggregateEventMethodInfo> Methods { get; }
}

sealed class AggregateEventMethodInfo
{
	public AggregateEventMethodInfo(string methodName, List<EventPropertyInfo> parameters)
	{
		MethodName = methodName;
		EventName = methodName + "Event";
		Parameters = parameters;
	}

	public string MethodName { get; }
	public string EventName { get; }
	public List<EventPropertyInfo> Parameters { get; }
}

sealed class EventPropertyInfo
{
	public EventPropertyInfo(string parameterName, string typeName)
	{
		ParameterName = parameterName;
		PropertyName = char.ToUpperInvariant(parameterName[0]) + parameterName.Substring(1);
		TypeName = typeName;
	}

	public string ParameterName { get; }
	public string PropertyName { get; }
	public string TypeName { get; }
}
