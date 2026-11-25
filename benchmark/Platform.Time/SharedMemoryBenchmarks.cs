using BenchmarkDotNet.Attributes;
using Platform.Memory.Shared;

namespace Platform.Time;

[MemoryDiagnoser]
public class SharedMemoryBenchmarks
{
    private SharedMemorySession _session = null!;
    private SharedMemoryRegion _cachedRegion;
    private uint _tag;

    [GlobalSetup]
    public void Setup()
    {
        _tag = FourCC.Make("DATA");
        var plan = new SharedMemoryPlan()
            .AddRegion(FourCC.Make("META"), 128, 64)
            .AddRegion(_tag, 4096, 64);

        _session = SharedMemorySession.CreateShared(
            $"bench_shm_{Guid.NewGuid():N}",
            FourCC.Make("BNCH"),
            1,
            1,
            plan
        );
        _cachedRegion = _session.GetRegion(_tag);
        _cachedRegion.Write(0, 42L);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _session.Dispose();
    }

    [Benchmark]
    public SharedMemoryRegion Session_GetRegion() => _session.GetRegion(_tag);

    [Benchmark]
    public long CachedRegion_ReadInt64() => _cachedRegion.Read<long>(0);

    [Benchmark]
    public long CachedRegion_AsRefInt64() => _cachedRegion.AsRef<long>(0);
}
