using System.Collections.Immutable;

namespace Purview.EventSourcing.SourceGenerator.Manifest;

/// <summary>
/// Verifies the shipped <c>Purview.EventSourcing.targets</c> exposes the event-contract manifest
/// properties and the manifest-materialization target, so the packaged build assets match the
/// generator contract.
/// </summary>
public sealed class EventSourcingPackageContentTests
{
	static string TargetsPath { get; } = ResolveTargetsPath();

	static string ResolveTargetsPath()
	{
		DirectoryInfo? current = new(AppContext.BaseDirectory);
		while (current is not null)
		{
			if (File.Exists(Path.Combine(current.FullName, "nuget.config")))
			{
				return Path.Combine(
					current.FullName,
					"src",
					"src",
					"EventSourcing",
					"Sdk",
					"buildTransitive",
					"Purview.EventSourcing.targets"
				);
			}

			current = current.Parent;
		}

		throw new InvalidOperationException("Could not locate the repository root from the test output directory.");
	}

	[Test]
	public async Task Targets_ExposeManifestProperties()
	{
		var targets = await File.ReadAllTextAsync(TargetsPath);

		await Assert.That(targets).Contains("PurviewEventContractManifestEnabled");
		await Assert.That(targets).Contains("PurviewEventContractBaselineFileName");
		await Assert.That(targets).Contains("PurviewEventContractManifestWrite");
		await Assert.That(targets).Contains("ExtractEventContractManifest");
		await Assert.That(targets).Contains("CompilerVisibleProperty");
	}

	[Test]
	public async Task Targets_DoNotReferenceWorkspacesAssemblies()
	{
		var targets = await File.ReadAllTextAsync(TargetsPath);

		await Assert.That(targets).DoesNotContain("Microsoft.CodeAnalysis.Workspaces");
	}
}
