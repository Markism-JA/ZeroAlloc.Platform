using BenchmarkDotNet.Attributes;
using Platform.Memory;

namespace Platform.Benchmark.Memory;

[MemoryDiagnoser]
public class NativeMemoryMapBenchmark
{
    private const nuint MapSize = 64 * 1024; // 64 KB
    private const int LongCount = (int)(MapSize / sizeof(long));

    private NativeMemoryMap _mapped = null!;
    private long[] _managedArray = null!;

    [GlobalSetup]
    public void Setup()
    {
        _mapped = NativeMemoryMap.CreateAnonymous(
            MapSize,
            MemoryMapProtection.ReadWrite,
            MemoryMapOptions.PreFault
        );
        _managedArray = new long[LongCount];

        var mappedSpan = _mapped.AsSpan<long>(0, LongCount);
        for (int i = 0; i < LongCount; i++)
        {
            mappedSpan[i] = i;
            _managedArray[i] = i;
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _mapped.Dispose();
    }

    [Benchmark(Baseline = true)]
    public long ManagedArray_SequentialSum_64KB()
    {
        long sum = 0;
        long[] arr = _managedArray;
        for (int i = 0; i < arr.Length; i++)
            sum += arr[i];
        return sum;
    }

    [Benchmark]
    public long NativeMemoryMap_TypedSpan_SequentialSum_64KB()
    {
        var longs = _mapped.AsSpan<long>(0, LongCount);
        long sum = 0;
        for (int i = 0; i < longs.Length; i++)
            sum += longs[i];
        return sum;
    }

    [Benchmark]
    public long AnonymousMap_DemandPaged_CreateAndTouchAllPages_64KB()
    {
        using var map = NativeMemoryMap.CreateAnonymous(MapSize);
        var span = map.AsSpan();
        int step = Environment.SystemPageSize;
        long acc = 0;
        for (int i = 0; i < span.Length; i += step)
        {
            span[i] = 1;
            acc += span[i];
        }
        return acc;
    }

    [Benchmark]
    public long AnonymousMap_PreFault_CreateAndTouchAllPages_64KB()
    {
        using var map = NativeMemoryMap.CreateAnonymous(
            MapSize,
            MemoryMapProtection.ReadWrite,
            MemoryMapOptions.PreFault
        );
        var span = map.AsSpan();
        int step = Environment.SystemPageSize;
        long acc = 0;
        for (int i = 0; i < span.Length; i += step)
        {
            span[i] = 1;
            acc += span[i];
        }
        return acc;
    }
}
