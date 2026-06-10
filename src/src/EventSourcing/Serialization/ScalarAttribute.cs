namespace Purview.EventSourcing.Serialization;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ScalarAttribute(string propertyName = "Value") : Attribute
{
	public string PropertyName { get; } = propertyName;
}
