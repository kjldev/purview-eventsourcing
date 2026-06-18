using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Purview.EventSourcing.SourceGenerator.Helpers;

namespace Purview.EventSourcing.SourceGenerator;

public abstract class SourceGeneratorTestBase<TGenerator>(bool throwOnLogError = true)
	where TGenerator : class, IIncrementalGenerator, new()
{
	public static readonly string[] GeneratedAttributes =
	[
		"EmbeddedAttribute.cs",
		"AggregatePropertyAttribute.g.cs",
		"GenerateAggregateAttribute.g.cs",
		"GenerateAggregateDefaultsAttribute.g.cs",
		"GenerateAggregateEventAttribute.g.cs",
		"MetadataAttribute.g.cs",
		"ComputedAttribute.g.cs",
	];

	public static readonly int ExpectedFileCount = GeneratedAttributes.Length;
	public static readonly int ExpectedFileCountPlusGen = ExpectedFileCount + 1;

	public const int HintNameHashHexLength = 16;
	public const string GeneratedSourceFileSuffix = ".g.cs";

	protected async Task<(GeneratorDriverRunResult Result, Compilation OutputCompilation)> GenerateAsync(
		string source,
		bool includeNamespaces,
		CancellationToken cancellationToken
	)
	{
		if (includeNamespaces)
		{
			source =
				@"
using System;
using System.Collections.Generic;
using System.Linq;

using Purview.EventSourcing.Aggregates;
using Purview.EventSourcing.Serialization;
using Purview.EventSourcing.ValueObjects;

" + source;
		}

		var syntaxTree = CSharpSyntaxTree.ParseText(source, cancellationToken: cancellationToken);

		var references = new List<MetadataReference>
		{
			// Without this, all of the references to event sourcing types will fail.
			MetadataReference.CreateFromFile(typeof(Aggregates.IAggregate).Assembly.Location),
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

		TGenerator generator = new();

		if (generator is ILogSupport logging && TestContext.Current is not null)
		{
			logging.SetLogOutput(
				(message, outputType) =>
				{
					var prefix = outputType switch
					{
						OutputType.Diagnostic => "DIA",
						OutputType.Debug => "DBG",
						OutputType.Info => "INF",
						OutputType.Warning => "WRN",
						OutputType.Error => "ERR",
						_ => "???",
					};

					TestContext.Current.OutputWriter.WriteLine($"{prefix}: {message}");

					if (throwOnLogError && outputType == OutputType.Error)
						throw new InvalidOperationException($"Generator logged error: {message}");
				}
			);
		}

		var driver = CSharpGeneratorDriver
			.Create(generator)
			.RunGeneratorsAndUpdateCompilation(
				compilation,
				out var outputCompilation,
				out var diagnostics,
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

	protected async Task<(GeneratorDriverRunResult Result, Compilation OutputCompilation)> GenerateAsync(
		string source,
		CancellationToken cancellationToken
	) => await GenerateAsync(source, includeNamespaces: true, cancellationToken);

	protected async Task<Assembly> CompileToAssemblyAsync(string source, CancellationToken cancellationToken)
	{
		var (_, compilation) = await GenerateAsync(source, cancellationToken);
		await using MemoryStream assemblyStream = new();
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
		return System.Reflection.Assembly.Load(assemblyStream.ToArray());
	}

	protected static IEnumerable<SyntaxTree> ExcludeGenAttribs(GeneratorDriverRunResult result)
	{
		return result.GeneratedTrees.Where(tree =>
			!GeneratedAttributes.Any(attr => tree.FilePath.EndsWith(attr, StringComparison.Ordinal))
		);
	}
}
