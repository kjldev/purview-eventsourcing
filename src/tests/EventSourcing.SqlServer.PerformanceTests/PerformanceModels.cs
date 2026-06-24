namespace Purview.EventSourcing.SqlServer;

sealed class SqlServerStorePerformanceRun
{
	public string Mode { get; set; } = string.Empty;

	public DateTimeOffset TimestampUtc { get; set; }

	public string MachineName { get; set; } = string.Empty;

	public string FrameworkDescription { get; set; } = string.Empty;

	public SqlServerPerformanceWorkload Workload { get; set; } = new();

	public List<SqlServerStorePerformanceScenarioRun> Scenarios { get; set; } = [];

	public bool Passed => Scenarios.All(static scenario => scenario.Passed);

	public IEnumerable<string> FormatSummary(SqlServerStorePerformanceRun? previousRun)
	{
		yield return $"Mode: {Mode}";
		yield return $"Timestamp (UTC): {TimestampUtc:O}";
		yield return $"Framework: {FrameworkDescription}";
		yield return $"Machine: {MachineName}";
		yield return $"Workload: aggregates={Workload.AggregateCount}, eventsPerAggregate={Workload.EventsPerAggregate}, queryIterations={Workload.QueryIterations}";
		yield return string.Empty;

		var previousScenarios =
			previousRun?.Scenarios.ToDictionary(static scenario => scenario.Name, StringComparer.Ordinal)
			?? [with(StringComparer.Ordinal)];

		foreach (var scenario in Scenarios)
		{
			yield return scenario.FormatCurrent();

			if (previousScenarios.TryGetValue(scenario.Name, out var previousScenario))
				yield return scenario.FormatComparison(previousScenario);
		}

		yield return string.Empty;
		yield return Passed ? "Result: PASS" : "Result: FAIL";
	}
}

sealed class SqlServerPerformanceWorkload
{
	public int AggregateCount { get; set; }

	public int EventsPerAggregate { get; set; }

	public int QueryIterations { get; set; }
}

sealed class SqlServerStorePerformanceScenarioRun
{
	public string Name { get; set; } = string.Empty;

	public int OperationCount { get; set; }

	public double TotalMilliseconds { get; set; }

	public double AverageMilliseconds { get; set; }

	public double OperationsPerSecond { get; set; }

	public double MaxAllowedAverageMilliseconds { get; set; }

	public bool Passed { get; set; }

	public string FormatCurrent()
	{
		var status = Passed ? "PASS" : "FAIL";
		return $"{Name} {status} total={TotalMilliseconds:F2}ms ops={OperationCount} avg={AverageMilliseconds:F2}ms ops/s={OperationsPerSecond:F2} threshold(avg)<={MaxAllowedAverageMilliseconds:F2}ms";
	}

	public string FormatComparison(SqlServerStorePerformanceScenarioRun previous) =>
		$"  vs previous: avg={FormatDelta(AverageMilliseconds - previous.AverageMilliseconds, previous.AverageMilliseconds)} ops/s={FormatDelta(OperationsPerSecond - previous.OperationsPerSecond, previous.OperationsPerSecond)}";

	static string FormatDelta(double delta, double previous)
	{
		var percent = previous <= 0 ? 0 : (delta / previous) * 100;
		return $"{delta:+0.00;-0.00;0.00} ({percent:+0.0;-0.0;0.0}%)";
	}
}
