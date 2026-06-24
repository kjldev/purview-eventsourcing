namespace Purview.EventSourcing.SqlServer;

public static class Program
{
	public static async Task<int> Main(string[] args)
	{
		var runBenchmark = Array.Exists(
			args,
			static arg => string.Equals(arg, "--benchmark", StringComparison.OrdinalIgnoreCase)
		);
		var runner = new SqlServerStorePerformanceRunner();
		var store = new SqlServerStorePerformanceHistoryStore();

		var previousRun = store.TryLoadLatest();
		var run = await (runBenchmark ? runner.RunBenchmarkAsync() : runner.RunQuickAsync());
		var savedPath = store.Save(run);

		await Console.Out.WriteLineAsync($"Saved {run.Mode} results to {savedPath}");
		await Console.Out.WriteLineAsync();

		foreach (var line in run.FormatSummary(previousRun))
			await Console.Out.WriteLineAsync(line);

		if (run.Passed)
			return 0;

		await Console.Out.WriteLineAsync();
		await Console.Error.WriteLineAsync("Performance thresholds were not met.");
		return 1;
	}
}
