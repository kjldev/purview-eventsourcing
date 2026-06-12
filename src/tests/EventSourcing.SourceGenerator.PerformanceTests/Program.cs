namespace Purview.EventSourcing.SourceGenerator;

public static class Program
{
	public static int Main(string[] args)
	{
		var runBenchmark = Array.Exists(
			args,
			static arg => string.Equals(arg, "--benchmark", StringComparison.OrdinalIgnoreCase)
		);
		var runner = new SourceGeneratorPerformanceRunner();
		var store = new PerformanceHistoryStore();

		var previousRun = store.TryLoadLatest();
		var run = runBenchmark ? runner.RunBenchmark() : runner.RunQuick();
		var savedPath = store.Save(run);

		Console.WriteLine($"Saved {run.Mode} results to {savedPath}");
		Console.WriteLine();

		foreach (var line in run.FormatSummary(previousRun))
			Console.WriteLine(line);

		return 0;
	}
}
