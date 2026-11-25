using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Benchmarking.Harness.Reporting;

/// <summary>
/// Standardized console and structured export formatter for benchmark results.
/// </summary>
public static class ConsoleReportFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Prints a formatted ASCII report to the console.
    /// </summary>
    public static void Print(in ProbeReport report)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 90));
        Console.WriteLine($" BENCHMARK PROBE: {report.ProbeName.ToUpperInvariant()}");
        Console.WriteLine($" Scenario       : {report.Scenario}");
        Console.WriteLine($" Host Topology  : {report.TopologySummary}");
        Console.WriteLine(
            $" Process Target : {Process.GetCurrentProcess().PriorityClass} | OS: {RuntimeInformation.OSDescription}"
        );
        Console.WriteLine(new string('=', 90));

        Console.WriteLine($" Total Operations   : {report.TotalOperations, 15:N0} ops");
        Console.WriteLine($" Elapsed Wall Time  : {report.Duration.TotalMilliseconds, 15:F2} ms");
        Console.WriteLine(
            $" Throughput Line Rate: {report.AchievedMpps, 14:F3} Mpps ({report.ThroughputOpsSec, 15:N0} ops/sec)"
        );

        if (report.BandwidthMBSec > 0.0)
        {
            Console.WriteLine($" Network Bandwidth  : {report.BandwidthMBSec, 14:F2} MB/s");
        }

        Console.WriteLine($" Caller GC Alloc    : {report.GcAllocatedBytes, 15:N0} bytes");
        Console.WriteLine($" Invariant Drops    : {report.DroppedOperations, 15:N0} ops");
        Console.WriteLine(new string('-', 90));

        Console.WriteLine(" LATENCY PERCENTILES (1 ns Resolution)");
        Console.WriteLine(new string('-', 90));
        Console.WriteLine(
            $"   Min              : {report.Latency.MinNs, 12:N0} ns ({report.Latency.MinNs / 1_000.0, 9:F2} µs)"
        );
        Console.WriteLine(
            $"   Mean             : {report.Latency.MeanNs, 12:F1} ns ({report.Latency.MeanNs / 1_000.0, 9:F2} µs)"
        );
        Console.WriteLine(
            $"   p50              : {report.Latency.P50Ns, 12:N0} ns ({report.Latency.P50Ns / 1_000.0, 9:F2} µs)"
        );
        Console.WriteLine(
            $"   p90              : {report.Latency.P90Ns, 12:N0} ns ({report.Latency.P90Ns / 1_000.0, 9:F2} µs)"
        );
        Console.WriteLine(
            $"   p99              : {report.Latency.P99Ns, 12:N0} ns ({report.Latency.P99Ns / 1_000.0, 9:F2} µs)"
        );
        Console.WriteLine(
            $"   p99.9            : {report.Latency.P999Ns, 12:N0} ns ({report.Latency.P999Ns / 1_000.0, 9:F2} µs)"
        );
        Console.WriteLine(
            $"   Max              : {report.Latency.MaxNs, 12:N0} ns ({report.Latency.MaxNs / 1_000.0, 9:F2} µs)"
        );
        Console.WriteLine(new string('=', 90));
        Console.WriteLine();
    }

    /// <summary>
    /// Writes the machine-readable JSON artifact of the report to the specified file path.
    /// </summary>
    public static void ExportJson(in ProbeReport report, string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(filePath, json);
    }
}
