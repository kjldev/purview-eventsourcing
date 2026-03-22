using System.Reflection;
using System.Text;

namespace Purview.EventSourcing.SourceGenerator.Templates;

sealed class EmbeddedResources
{
	readonly Assembly _ownerAssembly = typeof(EmbeddedResources).Assembly;
	readonly string _namespaceRoot = typeof(EmbeddedResources).Namespace!;

	EmbeddedResources() { }

	public static EmbeddedResources Instance { get; } = new();

	public string LoadTemplate(string name)
	{
		var resourceName = $"{_namespaceRoot}.Sources.{name}.cs";

		var resourceStream = _ownerAssembly.GetManifestResourceStream(resourceName);
		if (resourceStream is null)
		{
			var existingResources = _ownerAssembly.GetManifestResourceNames();
			throw new ArgumentException(
				$"Could not find embedded resource {resourceName}. Available: {string.Join(", ", existingResources)}"
			);
		}

		using var reader = new StreamReader(resourceStream, Encoding.UTF8);
		return reader.ReadToEnd().Trim();
	}
}
