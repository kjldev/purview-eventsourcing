using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Purview.EventSourcing.SourceGenerator;

public abstract class SourceGeneratorTestBase<TGenerator>
	where TGenerator : class, IIncrementalGenerator, new()
{
	protected static async Task<(GeneratorDriverRunResult Result, Compilation OutputCompilation)> GenerateAsync(
		string source,
		CancellationToken cancellationToken = default
	)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);

		var references = new List<MetadataReference>
		{
			MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
			MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
			MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
			MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
		};

		// Add netstandard reference
		var netstandard = System.Reflection.Assembly.Load(
			"netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"
		);
		references.Add(MetadataReference.CreateFromFile(netstandard.Location));

		var compilation = CSharpCompilation.Create(
			"TestAssembly",
			[syntaxTree],
			references,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
		);

		var generator = new TGenerator();

		GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);
		driver = driver.RunGeneratorsAndUpdateCompilation(
			compilation,
			out var outputCompilation,
			out _,
			cancellationToken
		);

		var result = driver.GetRunResult();

		// No generator exceptions
		foreach (var genResult in result.Results)
		{
			if (genResult.Exception is not null)
				throw genResult.Exception;
		}

		return (result, outputCompilation);
	}

	protected static async Task<global::System.Reflection.Assembly> CompileToAssemblyAsync(
		string source,
		CancellationToken cancellationToken = default
	)
	{
		var (_, compilation) = await GenerateAsync(source, cancellationToken);
		await using var assemblyStream = new MemoryStream();
		var emitResult = compilation.Emit(assemblyStream, cancellationToken: cancellationToken);
		if (!emitResult.Success)
		{
			var diagnostics = string.Join(
				Environment.NewLine,
				emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString())
			);

			throw new InvalidOperationException(diagnostics);
		}

		assemblyStream.Position = 0;
		return global::System.Reflection.Assembly.Load(assemblyStream.ToArray());
	}
}
