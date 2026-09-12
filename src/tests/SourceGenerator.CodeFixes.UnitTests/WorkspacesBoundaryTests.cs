using Purview.EventSourcing.SourceGenerator.Generators;

namespace Purview.EventSourcing.SourceGenerator.CodeFixes;

/// <summary>
/// Proves the hard assembly boundary: the source-generator/analyzer assembly must not reference
/// Workspaces, while the code-fix assembly is the only home for Workspaces-dependent providers.
/// </summary>
public sealed class WorkspacesBoundaryTests
{
	[Test]
	public async Task GeneratorAssembly_DoesNotReferenceWorkspaces()
	{
		var references = typeof(AggregateSourceGenerator)
			.Assembly.GetReferencedAssemblies()
			.Select(static reference => reference.Name)
			.ToArray();

		await Assert
			.That(
				references.Any(static name =>
					name?.StartsWith("Microsoft.CodeAnalysis.Workspaces", StringComparison.Ordinal) == true
				)
			)
			.IsFalse();
	}

	[Test]
	public async Task CodeFixAssembly_ReferencesWorkspaces()
	{
		var references = typeof(AddPartialModifierCodeFixProvider)
			.Assembly.GetReferencedAssemblies()
			.Select(static reference => reference.Name)
			.ToArray();

		await Assert
			.That(
				references.Any(static name =>
					name?.StartsWith("Microsoft.CodeAnalysis.Workspaces", StringComparison.Ordinal) == true
				)
			)
			.IsTrue();
	}

	[Test]
	public async Task GeneratorProjectFile_DeclaresNoWorkspacesReference()
	{
		var projectPath = ResolveGeneratorProjectPath();
		var content = await File.ReadAllTextAsync(projectPath);

		await Assert.That(content).DoesNotContain("Microsoft.CodeAnalysis.CSharp.Workspaces");
		await Assert.That(content).DoesNotContain("Microsoft.CodeAnalysis.Workspaces.MSBuild");
	}

	static string ResolveGeneratorProjectPath()
	{
		DirectoryInfo? current = new(AppContext.BaseDirectory);
		while (current is not null)
		{
			if (File.Exists(Path.Combine(current.FullName, "nuget.config")))
			{
				return Path.Combine(current.FullName, "src", "src", "SourceGenerator", "SourceGenerator.csproj");
			}

			current = current.Parent;
		}

		throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
	}
}
