namespace Purview.EventSourcing.Serialization;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ScalarAttribute(string propertyName = "Value") : Attribute
{
	public string PropertyName { get; } = propertyName;

	public bool GenerateJsonConverter { get; init; } = true;

	public bool GenerateComparable { get; init; } = true;

	public bool GenerateComparisonOperators { get; init; } = true;

	public bool GenerateEnumProperties { get; init; } = true;

	public bool GenerateImplicitFromPrimitive { get; init; } = true;

	public bool GenerateImplicitToPrimitive { get; init; } = true;

	public bool GenerateEmpty { get; init; } = true;

	public ValueObjectDeserializationMode DeserializationMode { get; init; } = ValueObjectDeserializationMode.Hydrate;
}
