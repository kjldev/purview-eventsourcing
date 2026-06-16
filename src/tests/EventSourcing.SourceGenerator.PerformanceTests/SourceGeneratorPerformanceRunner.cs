using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Purview.EventSourcing.SourceGenerator;

sealed class SourceGeneratorPerformanceRunner
{
	static readonly MetadataReference[] References =
	[
		MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
		MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
		MetadataReference.CreateFromFile(System.Reflection.Assembly.Load("System.Runtime").Location),
		MetadataReference.CreateFromFile(typeof(System.Text.Json.JsonSerializer).Assembly.Location),
		MetadataReference.CreateFromFile(
			System
				.Reflection.Assembly.Load(
					"netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"
				)
				.Location
		),
	];

	static readonly CSharpCompilationOptions CompilationOptions = new(OutputKind.DynamicallyLinkedLibrary);

	public PerformanceRun RunQuick() => Run("Quick", warmupIterations: 1, measurementIterations: 3);

	public PerformanceRun RunBenchmark() => Run("Benchmark", warmupIterations: 3, measurementIterations: 12);

	PerformanceRun Run(string mode, int warmupIterations, int measurementIterations)
	{
		var scenarios = SourceGeneratorPerformanceScenarios.All;
		var results = new List<PerformanceScenarioRun>(scenarios.Count);

		foreach (var scenario in scenarios)
		{
			var baseline = Measure(
				scenario.Source,
				warmupIterations,
				measurementIterations,
				compileWithGenerator: false,
				scenario.CreateGenerator
			);
			var generator = Measure(
				scenario.Source,
				warmupIterations,
				measurementIterations,
				compileWithGenerator: true,
				scenario.CreateGenerator
			);

			results.Add(
				new PerformanceScenarioRun
				{
					Name = scenario.Name,
					GeneratorName = scenario.GeneratorName,
					WarmupIterations = warmupIterations,
					MeasurementIterations = measurementIterations,
					BaselineAverageMilliseconds = baseline.AverageMilliseconds,
					GeneratorAverageMilliseconds = generator.AverageMilliseconds,
				}
			);
		}

		return new PerformanceRun
		{
			Mode = mode,
			TimestampUtc = DateTimeOffset.UtcNow,
			MachineName = Environment.MachineName,
			FrameworkDescription = RuntimeInformation.FrameworkDescription,
			Scenarios = results,
		};
	}

	static Measurement Measure(
		string source,
		int warmupIterations,
		int measurementIterations,
		bool compileWithGenerator,
		Func<IIncrementalGenerator> generatorFactory
	)
	{
		for (var i = 0; i < warmupIterations; i++)
			RunOnce(source, compileWithGenerator, generatorFactory);

		var stopwatch = Stopwatch.StartNew();
		for (var i = 0; i < measurementIterations; i++)
			RunOnce(source, compileWithGenerator, generatorFactory);
		stopwatch.Stop();

		return new Measurement(stopwatch.Elapsed.TotalMilliseconds / measurementIterations);
	}

	static void RunOnce(string source, bool compileWithGenerator, Func<IIncrementalGenerator> generatorFactory)
	{
		var syntaxTree = CSharpSyntaxTree.ParseText(source);
		var compilation = CSharpCompilation.Create(
			"SourceGeneratorPerformance",
			[syntaxTree],
			References,
			CompilationOptions
		);

		if (compileWithGenerator)
		{
			GeneratorDriver driver = CSharpGeneratorDriver.Create(generatorFactory().AsSourceGenerator());
			driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var _, out var diagnostics);

			foreach (var diagnostic in diagnostics)
			{
				if (diagnostic.Severity == DiagnosticSeverity.Error)
					throw new InvalidOperationException(diagnostic.ToString());
			}

			foreach (var generatorResult in driver.GetRunResult().Results)
			{
				if (generatorResult.Exception is not null)
					throw generatorResult.Exception;
			}

			return;
		}
	}

	sealed record Measurement(double AverageMilliseconds);
}
