namespace Purview.EventSourcing.SourceGenerator;

sealed class PerformanceRun
{
	public string Mode { get; set; } = string.Empty;

	public DateTimeOffset TimestampUtc { get; set; }

	public string MachineName { get; set; } = string.Empty;

	public string FrameworkDescription { get; set; } = string.Empty;

	public List<PerformanceScenarioRun> Scenarios { get; set; } = [];

	public IEnumerable<string> FormatSummary(PerformanceRun? previousRun)
	{
		yield return $"Mode: {Mode}";
		yield return $"Timestamp (UTC): {TimestampUtc:O}";
		yield return $"Framework: {FrameworkDescription}";
		yield return $"Machine: {MachineName}";
		yield return string.Empty;

		var previousScenarios =
			previousRun?.Scenarios.ToDictionary(static scenario => scenario.Name, StringComparer.Ordinal)
			?? new Dictionary<string, PerformanceScenarioRun>(StringComparer.Ordinal);

		foreach (var scenario in Scenarios)
		{
			yield return scenario.FormatCurrent();

			if (previousScenarios.TryGetValue(scenario.Name, out var previousScenario))
				yield return scenario.FormatComparison(previousScenario);
		}
	}
}

sealed class PerformanceScenarioRun
{
	public string Name { get; set; } = string.Empty;

	public string GeneratorName { get; set; } = string.Empty;

	public int WarmupIterations { get; set; }

	public int MeasurementIterations { get; set; }

	public double BaselineAverageMilliseconds { get; set; }

	public double GeneratorAverageMilliseconds { get; set; }

	public double GeneratorOverheadMilliseconds => GeneratorAverageMilliseconds - BaselineAverageMilliseconds;

	public double GeneratorOverheadPercent =>
		BaselineAverageMilliseconds <= 0 ? 0 : (GeneratorOverheadMilliseconds / BaselineAverageMilliseconds) * 100;

	public string FormatCurrent() =>
		$"{Name} [{GeneratorName}] baseline={BaselineAverageMilliseconds:F2}ms generator={GeneratorAverageMilliseconds:F2}ms overhead={GeneratorOverheadMilliseconds:F2}ms ({GeneratorOverheadPercent:F1}%)";

	public string FormatComparison(PerformanceScenarioRun previous) =>
		$"  vs previous: generator={FormatDelta(GeneratorAverageMilliseconds - previous.GeneratorAverageMilliseconds, previous.GeneratorAverageMilliseconds)} baseline={FormatDelta(BaselineAverageMilliseconds - previous.BaselineAverageMilliseconds, previous.BaselineAverageMilliseconds)}";

	static string FormatDelta(double delta, double previous)
	{
		var percent = previous <= 0 ? 0 : (delta / previous) * 100;
		return $"{delta:+0.00;-0.00;0.00}ms ({percent:+0.0;-0.0;0.0}%)";
	}
}
