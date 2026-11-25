using BenchmarkDotNet.Attributes;
using Platform.Memory.Shared;

namespace Platform.Benchmark.Memory;

[MemoryDiagnoser]
public class SharedMemoryBenchmark
{
    private const int BatchSize = 256;

    private SharedMemorySession _session = null!;
    private SharedMemoryRegion _cachedRegion;
    private uint _tag;

    [GlobalSetup]
    public void Setup()
    {
        _tag = FourCC.Make("DATA");
        var plan = new SharedMemoryPlan()
            .AddRegion(FourCC.Make("META"), 128, 64)
            .AddRegion(_tag, BatchSize * sizeof(long), 64);

        _session = SharedMemorySession.CreateShared(
            $"bench_shm_{Guid.NewGuid():N}",
            FourCC.Make("BNCH"),
            1,
            1,
            plan
        );
        _cachedRegion = _session.GetRegion(_tag);

        var span = _cachedRegion.AsSpan<long>();
        for (int i = 0; i < span.Length; i++)
            span[i] = i + 1;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _session.Dispose();
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public nuint Session_GetRegion()
    {
        nuint acc = 0;
        for (int i = 0; i < BatchSize; i++)
            acc += _session.GetRegion(_tag).Length;
        return acc;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public long CachedRegion_ReadInt64()
    {
        long sum = 0;
        for (int i = 0; i < BatchSize; i++)
            sum += _cachedRegion.Read<long>(i * sizeof(long));
        return sum;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public long CachedRegion_AsRefInt64()
    {
        long sum = 0;
        for (int i = 0; i < BatchSize; i++)
            sum += _cachedRegion.AsRef<long>(i * sizeof(long));
        return sum;
    }
}
