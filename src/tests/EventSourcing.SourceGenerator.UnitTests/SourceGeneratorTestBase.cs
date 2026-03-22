using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Purview.EventSourcing.SourceGenerator;

public abstract class SourceGeneratorTestBase<TGenerator>
	where TGenerator : class, IIncrementalGenerator, new()
{
	protected static async Task<(GeneratorDriverRunResult Result, Compilation OutputCompilation)> GenerateAsync(
		string source,
		CancellationToken cancellationToken = default)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);

		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
			MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
		};

		// Add netstandard reference
		var netstandard = System.Reflection.Assembly.Load("netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
		references.Add(MetadataReference.CreateFromFile(netstandard.Location));

		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[syntaxTree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

		var generator = new TGenerator();

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out var outputCompilation,
			out var diagnostics,
			cancellationToken);

		var result = driver.GetRunResult();

		// No generator exceptions
		foreach (var genResult in result.Results)
		{
			if (genResult.Exception is not null)
				throw genResult.Exception;
		}

		return (result, outputCompilation);
	}
}
