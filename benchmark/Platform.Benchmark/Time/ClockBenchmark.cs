using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Memory.Allocators;
using Platform.Time;

namespace Platform.Benchmark.Time;

[MemoryDiagnoser]
public unsafe class ClockBenchmark
{
    private const int BatchSize = 64;

    private PlatformClockBlock* _liveBlock;
    private PlatformClockBlock* _manualBlock;

    [GlobalSetup]
    public void Setup()
    {
        _liveBlock = (PlatformClockBlock*)HeapAllocator.Allocate(sizeof(PlatformClockBlock), 64);
        _manualBlock = (PlatformClockBlock*)HeapAllocator.Allocate(sizeof(PlatformClockBlock), 64);
        *_liveBlock = default;
        *_manualBlock = default;
        PlatformClock.SetManualTime(_manualBlock, 1_700_000_000_000_000_000L);
        _ = NativeTsc.NowNs(); // Calibrate static TSC anchor before measurement
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        HeapAllocator.Free(_liveBlock);
        HeapAllocator.Free(_manualBlock);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BatchSize)]
    public long Stopwatch_GetTimestamp()
    {
        long acc = 0;
        for (int i = 0; i < BatchSize; i++)
            acc += Stopwatch.GetTimestamp();
        return acc;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public long DateTime_UtcNow_Ticks()
    {
        long acc = 0;
        for (int i = 0; i < BatchSize; i++)
            acc += DateTime.UtcNow.Ticks;
        return acc;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public long NativeTsc_NowNs()
    {
        long acc = 0;
        for (int i = 0; i < BatchSize; i++)
            acc += NativeTsc.NowNs();
        return acc;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public long PlatformClock_NowNs_Live()
    {
        long acc = 0;
        for (int i = 0; i < BatchSize; i++)
            acc += PlatformClock.NowNs(_liveBlock);
        return acc;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public long PlatformClock_NowNs_Manual()
    {
        long acc = 0;
        for (int i = 0; i < BatchSize; i++)
            acc += PlatformClock.NowNs(_manualBlock);
        return acc;
    }
}
