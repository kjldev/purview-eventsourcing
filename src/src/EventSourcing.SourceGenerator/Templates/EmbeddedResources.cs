using System.Reflection;
using System.Text;
using Purview.EventSourcing.SourceGenerator.Helpers;

namespace Purview.EventSourcing.SourceGenerator.Templates;

static class EmbeddedResources
{
	static readonly Assembly OwnerAssembly = typeof(EmbeddedResources).Assembly;

	public static string LoadTemplate(string name)
	{
		var resourceName = $"{AssemblyInfo.RootNamespace}.Templates.Sources.{name}.cs";

		var resourceStream = OwnerAssembly.GetManifestResourceStream(resourceName);
		if (resourceStream is null)
		{
			var existingResources = OwnerAssembly.GetManifestResourceNames();
			throw new ArgumentException(
				$"Could not find embedded resource {resourceName}. Available: {string.Join(", ", existingResources)}"
			);
		}

		using StreamReader reader = new(resourceStream, Encoding.UTF8);
		var template = reader.ReadToEnd();

		template = template.Replace(CodeGenHelpers.CodeGenReplacementToken, CodeGenHelpers.GetGeneratedCodeAttribute());

		return template;
	}
}
