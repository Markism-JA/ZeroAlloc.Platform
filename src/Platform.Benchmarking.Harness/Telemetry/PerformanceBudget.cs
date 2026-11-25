namespace Platform.Benchmarking.Harness.Telemetry;

public sealed class PerformanceBudgetViolationException(string message) : Exception(message);

/// <summary>
/// Hard performance contract budget for automated regression gating.
/// </summary>
public readonly record struct PerformanceBudget(
    long MaxP50Ns = long.MaxValue,
    long MaxP99Ns = long.MaxValue,
    long MaxP999Ns = long.MaxValue,
    long MaxAllowedDrops = 0,
    long MaxAllowedGcBytes = 0,
    double MinThroughputMpps = 0.0
)
{
    /// <summary>
    /// Evaluates execution metrics against the budget, throwing a detailed
    /// <see cref="PerformanceBudgetViolationException"/> if any threshold is violated.
    /// </summary>
    public void Evaluate(
        in HistogramSnapshot latency,
        long drops,
        long gcAllocatedBytes,
        double achievedMpps
    )
    {
        if (gcAllocatedBytes > MaxAllowedGcBytes)
        {
            throw new PerformanceBudgetViolationException(
                $"[BUDGET VIOLATION] GC Allocations exceeded: {gcAllocatedBytes:N0} B > budget {MaxAllowedGcBytes:N0} B"
            );
        }

        if (drops > MaxAllowedDrops)
        {
            throw new PerformanceBudgetViolationException(
                $"[BUDGET VIOLATION] Dropped frames exceeded: {drops:N0} > budget {MaxAllowedDrops:N0}"
            );
        }

        if (latency.P50Ns > MaxP50Ns)
        {
            throw new PerformanceBudgetViolationException(
                $"[BUDGET VIOLATION] P50 Latency exceeded: {latency.P50Ns:N0} ns > budget {MaxP50Ns:N0} ns"
            );
        }

        if (latency.P99Ns > MaxP99Ns)
        {
            throw new PerformanceBudgetViolationException(
                $"[BUDGET VIOLATION] P99 Latency exceeded: {latency.P99Ns:N0} ns > budget {MaxP99Ns:N0} ns"
            );
        }

        if (latency.P999Ns > MaxP999Ns)
        {
            throw new PerformanceBudgetViolationException(
                $"[BUDGET VIOLATION] P99.9 Latency exceeded: {latency.P999Ns:N0} ns > budget {MaxP999Ns:N0} ns"
            );
        }

        if (achievedMpps < MinThroughputMpps)
        {
            throw new PerformanceBudgetViolationException(
                $"[BUDGET VIOLATION] Throughput line rate below floor: {achievedMpps:F2} Mpps < budget {MinThroughputMpps:F2} Mpps"
            );
        }
    }
}
