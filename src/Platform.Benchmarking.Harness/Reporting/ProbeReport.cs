using Platform.Benchmarking.Harness.Telemetry;

namespace Platform.Benchmarking.Harness.Reporting;

/// <summary>
/// Immutable, machine-verifiable report summarizing a benchmark probe execution run.
/// </summary>
public readonly record struct ProbeReport(
    string ProbeName,
    string Scenario,
    TimeSpan Duration,
    long TotalOperations,
    double ThroughputOpsSec,
    double BandwidthMBSec,
    HistogramSnapshot Latency,
    long DroppedOperations,
    long GcAllocatedBytes,
    string TopologySummary
)
{
    public double AchievedMpps => ThroughputOpsSec / 1_000_000.0;

    /// <summary>
    /// Evaluates the probe run against a hard performance budget.
    /// Throws <see cref="PerformanceBudgetViolationException"/> if any threshold fails.
    /// </summary>
    public void AssertBudget(in PerformanceBudget budget)
    {
        budget.Evaluate(
            latency: Latency,
            drops: DroppedOperations,
            gcAllocatedBytes: GcAllocatedBytes,
            achievedMpps: AchievedMpps
        );
    }
}
