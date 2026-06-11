namespace Purview.EventSourcing.Serialization;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public sealed class ValueObjectAttribute : Attribute
{
	public bool GenerateJsonConverter { get; init; } = true;

	public bool GenerateComparable { get; init; } = true;

	public bool GenerateComparisonOperators { get; init; } = true;

	public ValueObjectDeserializationMode DeserializationMode { get; init; } = ValueObjectDeserializationMode.Hydrate;
}
