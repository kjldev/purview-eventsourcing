namespace Purview.EventSourcing.Serialization;

/// <summary>
/// Specifies assembly-level defaults for value object code generation.
/// These defaults can be overridden on individual [ValueObject] attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class GenerateValueObjectDefaultsAttribute : Attribute
{
	/// <summary>
	/// Gets or sets whether parameterless constructors should be generated for value objects.
	/// When true, generates a private parameterless constructor for EF Core compatibility.
	/// Individual [ValueObject] attributes can override this setting.
	/// Default: true
	/// </summary>
	public bool GenerateConstructor { get; init; } = true;

	public GenerateValueObjectDefaultsAttribute() { }
}
