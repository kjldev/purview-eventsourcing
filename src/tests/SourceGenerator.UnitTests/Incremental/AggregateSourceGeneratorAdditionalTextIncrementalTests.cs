using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Purview.EventSourcing.SourceGenerator.Generators;
using StepReason = Microsoft.CodeAnalysis.IncrementalStepRunReason;

namespace Purview.EventSourcing.SourceGenerator.Incremental;

/// <summary>
/// Verifies that changing an unrelated <see cref="AdditionalText"/> does not invalidate the aggregate
/// pipeline. The framework's <c>IncrementalRunInput</c> cannot vary additional texts per run, so this
/// scenario drives a raw <c>GeneratorDriver</c> directly.
/// </summary>
public sealed class AggregateSourceGeneratorAdditionalTextIncrementalTests
{
	const string OrderAggregateSource = """
		using Purview.EventSourcing.Aggregates;

		namespace Testing
		{
			[Aggregate]
			public partial class OrderAggregate : AggregateBase
			{
				public string CustomerId { get; private set; }

				[Event]
				public partial void CreateOrder(string customerId);
			}
		}
		""";

	[Test]
	public async Task Generate_GivenUnrelatedAdditionalFileChange_TargetsUnaffected(CancellationToken cancellationToken)
	{
		InMemoryAdditionalText unrelated = new("notes.txt", "first");
		GeneratorDriver driver = CreateDriver([unrelated]);
		var compilation = CreateCompilation([ParseTree(OrderAggregateSource, "Order.cs")]);

		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cancellationToken);
		var firstGenerated = GeneratedSourceTexts(driver.GetRunResult().Results[0]);

		driver = driver.ReplaceAdditionalText(unrelated, new InMemoryAdditionalText("notes.txt", "second"));
		driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _, cancellationToken);

		var result = driver.GetRunResult().Results[0];
		await Assert.That(StepReasons(result, "GetAggregateTargets")).DoesNotContain(StepReason.Modified);
		await Assert.That(StepReasons(result, "EventContractManifest")).DoesNotContain(StepReason.Modified);

		var secondGenerated = GeneratedSourceTexts(result);
		await Assert.That(secondGenerated).IsEquivalentTo(firstGenerated);
	}

	static ImmutableArray<StepReason> StepReasons(GeneratorRunResult result, string stepName) =>
		[
			.. result.TrackedSteps.TryGetValue(stepName, out var steps)
				? steps.SelectMany(static step => step.Outputs.Select(static output => output.Reason))
				: [],
		];

	static ImmutableArray<string> GeneratedSourceTexts(GeneratorRunResult result) =>
		[
			.. result
				.GeneratedSources.Select(static source => source.SourceText.ToString())
				.OrderBy(static source => source, StringComparer.Ordinal),
		];

	static CSharpGeneratorDriver CreateDriver(ImmutableArray<AdditionalText> additionalTexts) =>
		CSharpGeneratorDriver.Create(
			[new AggregateSourceGenerator().AsSourceGenerator()],
			additionalTexts: additionalTexts,
			parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
			driverOptions: new GeneratorDriverOptions(
				IncrementalGeneratorOutputKind.None,
				trackIncrementalGeneratorSteps: true
			)
		);

	static CSharpCompilation CreateCompilation(IEnumerable<SyntaxTree> trees) =>
		CSharpCompilation.Create(
			"TestAssembly",
			trees,
			ResolveReferences(),
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

	static SyntaxTree ParseTree(string source, string filePath) =>
		CSharpSyntaxTree.ParseText(source, path: filePath, options: new CSharpParseOptions(LanguageVersion.Latest));

	static ImmutableArray<MetadataReference> ResolveReferences()
	{
		var generatorAssemblyPath = typeof(SourceGeneratorFramework.TypeIdentity).Assembly.Location;
		var builder = ImmutableArray.CreateBuilder<MetadataReference>();

		var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty).Split(
			Path.PathSeparator,
			StringSplitOptions.RemoveEmptyEntries
		);
		foreach (var path in trusted)
		{
			if (string.Equals(path, generatorAssemblyPath, StringComparison.OrdinalIgnoreCase))
				continue;

			builder.Add(MetadataReference.CreateFromFile(path));
		}

		builder.Add(
			MetadataReference.CreateFromFile(
				typeof(System.ComponentModel.DataAnnotations.RequiredAttribute).Assembly.Location
			)
		);
		builder.Add(MetadataReference.CreateFromFile(typeof(Aggregates.IAggregate).Assembly.Location));
		builder.Add(MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location));

		return builder.ToImmutable();
	}
}
