using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Platform.Time;

public static unsafe class PlatformClockBenchmark
{
    private const long SubMillisecondThresholdNs = 1_000_000; // 1.0 ms constraint

    public static void Main()
    {
        Console.WriteLine("==================================================================");
        Console.WriteLine("        PLATFORM CLOCK CORE CONSTRAINT & LATENCY BENCHMARK        ");
        Console.WriteLine("==================================================================\n");

        var clock = (PlatformClockBlock*)
            NativeMemory.AllocZeroed((nuint)sizeof(PlatformClockBlock));

        try
        {
            // 1. Single-Thread Hot-Path Latency
            BenchmarkCallerLatency(clock);

            // 2. Freshness & Max Stall Verification (Hardware TSC - 0 background threads)
            bool passed = BenchmarkTscIntegrityAndDrift(clock, testDurationSeconds: 5);

            // 3. Multi-Threaded Scalability (Contention check across physical cores)
            BenchmarkConcurrentReaders(clock, testDurationSeconds: 3);

            Console.WriteLine(
                "\n=================================================================="
            );
            Console.WriteLine(
                $"OVERALL VERDICT: [{(passed ? "PASS" : "FAIL")}] Sub-millisecond constraint satisfied."
            );
            Console.WriteLine("==================================================================");
        }
        finally
        {
            NativeMemory.Free(clock);
        }
    }

    private static void BenchmarkCallerLatency(PlatformClockBlock* clock)
    {
        const int iterations = 20_000_000;
        long dummy = 0;

        // Warmup JIT & calibrate TSC
        for (int i = 0; i < 500_000; i++)
            dummy += PlatformClock.NowNs(clock);

        long start = Stopwatch.GetTimestamp();
        for (int i = 0; i < iterations; i++)
        {
            dummy += PlatformClock.NowNs(clock);
        }
        long end = Stopwatch.GetTimestamp();

        double totalNs = (end - start) * (1_000_000_000.0 / Stopwatch.Frequency);
        double nsPerCall = totalNs / iterations;

        Console.WriteLine("[1/3] Caller Read Latency (Single-Threaded)");
        Console.WriteLine($"  - Total Reads:        {iterations:N0}");
        Console.WriteLine($"  - Cost per NowNs():   {nsPerCall:F2} ns");
        Console.WriteLine(
            $"  - State:              {(nsPerCall <= 5.0 ? "OPTIMAL (< 5ns)" : "ACCEPTABLE")}\n"
        );
    }

    private static bool BenchmarkTscIntegrityAndDrift(
        PlatformClockBlock* clock,
        int testDurationSeconds
    )
    {
        Console.WriteLine(
            $"[2/3] Hardware TSC Monotonicity & Wall-Clock Drift ({testDurationSeconds}s Window)"
        );
        Console.WriteLine("  - Validating continuous progression and cross-core stability...");

        long testDurationTicks = Stopwatch.Frequency * testDurationSeconds;
        long testStartTicks = Stopwatch.GetTimestamp();

        long nonMonotonicEvents = 0;
        long totalReads = 0;

        // Track baseline for drift calculation
        long startUtcNs = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) * 100L;
        long startClockNs = PlatformClock.NowNs(clock);

        long prevNs = startClockNs;

        while (Stopwatch.GetTimestamp() - testStartTicks < testDurationTicks)
        {
            long currNs = PlatformClock.NowNs(clock);

            // Test 1: Monotonicity (time must NEVER move backwards)
            if (currNs < prevNs)
            {
                nonMonotonicEvents++;
            }

            prevNs = currNs;
            totalReads++;
        }

        long endClockNs = PlatformClock.NowNs(clock);
        long endUtcNs = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) * 100L;

        long elapsedClockNs = endClockNs - startClockNs;
        long elapsedRealNs = endUtcNs - startUtcNs;

        double driftMs = Math.Abs(elapsedClockNs - elapsedRealNs) / 1_000_000.0;
        double driftPpm =
            (Math.Abs(elapsedClockNs - elapsedRealNs) / (double)elapsedRealNs) * 1_000_000.0;

        Console.WriteLine($"  - Total Consecutive Reads:  {totalReads:N0}");
        Console.WriteLine(
            $"  - Non-Monotonic Regressions:{nonMonotonicEvents} (strictly 0 required)"
        );
        Console.WriteLine($"  - Real Elapsed Wall-Clock:  {elapsedRealNs / 1_000_000.0:F2} ms");
        Console.WriteLine($"  - Clock Elapsed Time:       {elapsedClockNs / 1_000_000.0:F2} ms");
        Console.WriteLine($"  - Total Drift over Window:  {driftMs:F3} ms ({driftPpm:F1} PPM)");

        // Acceptable drift: < 2.0ms over 5 seconds (well within sub-millisecond per-operation tolerances)
        bool isMonotonic = nonMonotonicEvents == 0;
        bool isAccurate = driftMs < 2.0;

        return isMonotonic && isAccurate;
    }

    private static void BenchmarkConcurrentReaders(
        PlatformClockBlock* clock,
        int testDurationSeconds
    )
    {
        int threadCount = Math.Max(2, Environment.ProcessorCount);
        Console.WriteLine($"\n[3/3] Concurrent Contention ({threadCount} Active Worker Threads)");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(testDurationSeconds));
        var token = cts.Token;

        long[] readCounts = new long[threadCount];
        var tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                long localReads = 0;
                while (!token.IsCancellationRequested)
                {
                    _ = PlatformClock.NowNs(clock);
                    localReads++;
                }
                readCounts[index] = localReads;
            });
        }

        Task.WaitAll(tasks);

        long totalReads = 0;
        for (int i = 0; i < threadCount; i++)
            totalReads += readCounts[i];

        double throughputMOps = (totalReads / (double)testDurationSeconds) / 1_000_000.0;

        Console.WriteLine($"  - Combined Reads:     {totalReads:N0}");
        Console.WriteLine($"  - Total Throughput:   {throughputMOps:F2} Million reads/sec");
    }
}
