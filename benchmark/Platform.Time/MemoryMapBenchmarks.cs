using BenchmarkDotNet.Attributes;
using Platform.Memory;

namespace Platform.Time;

[MemoryDiagnoser]
public class MemoryMapBenchmarks
{
    private const nuint MapSize = 64 * 1024; // 64 KB (16 pages on 4KB / 4 pages on 16KB)
    private NativeMemoryMap _persistentMap = null!;

    [GlobalSetup]
    public void Setup()
    {
        _persistentMap = NativeMemoryMap.CreateAnonymous(
            MapSize,
            MemoryMapProtection.ReadWrite,
            MemoryMapOptions.PreFault
        );
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _persistentMap.Dispose();
    }

    [Benchmark]
    public ulong AnonymousMap_DemandPaged_TouchFirstAndLastPage()
    {
        using var map = NativeMemoryMap.CreateAnonymous(MapSize);
        var span = map.AsSpan();
        span[0] = 1;
        span[(int)MapSize - 1] = 2;
        return (ulong)(span[0] + span[(int)MapSize - 1]);
    }

    [Benchmark]
    public ulong AnonymousMap_PreFault_TouchFirstAndLastPage()
    {
        using var map = NativeMemoryMap.CreateAnonymous(
            MapSize,
            MemoryMapProtection.ReadWrite,
            MemoryMapOptions.PreFault
        );
        var span = map.AsSpan();
        span[0] = 1;
        span[(int)MapSize - 1] = 2;
        return (ulong)(span[0] + span[(int)MapSize - 1]);
    }

    [Benchmark]
    public long TypedSpan_Sum64KB()
    {
        var longs = _persistentMap.AsSpan<long>(0, (int)(MapSize / sizeof(long)));
        long sum = 0;
        for (int i = 0; i < longs.Length; i++)
            sum += longs[i];
        return sum;
    }
}
