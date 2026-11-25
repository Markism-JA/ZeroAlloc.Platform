using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Platform.Benchmarking.Harness.Telemetry;

/// <summary>
/// Cache-aligned, 0-allocation unmanaged latency histogram.
/// Tracks latencies from 0 to <see cref="MaxTrackedNs"/> with 1-nanosecond resolution.
/// </summary>
public sealed unsafe class UnmanagedLinearHistogram : IDisposable
{
    private readonly int _maxTrackedNs;
    private readonly nuint _allocationSizeBytes;
    private readonly long* _buckets;

    private long _totalCount;
    private long _sumNs;
    private long _minNs = long.MaxValue;
    private long _maxNs;
    private int _isDisposed;

    /// <summary>
    /// Initializes an unmanaged linear histogram.
    /// </summary>
    /// <param name="maxTrackedNs">Maximum tracked nanoseconds (default 20,000,000 ns = 20 ms).</param>
    public UnmanagedLinearHistogram(int maxTrackedNs = 20_000_000)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTrackedNs);

        _maxTrackedNs = maxTrackedNs;
        _allocationSizeBytes = (nuint)((maxTrackedNs + 1) * sizeof(long));

        // 64-byte alignment matches standard CPU L1/L2 cache-line boundaries
        _buckets = (long*)NativeMemory.AlignedAlloc(_allocationSizeBytes, 64);
        Reset();
    }

    public int MaxTrackedNs => _maxTrackedNs;
    public long TotalCount => Volatile.Read(ref _totalCount);
    public long MinNs => Volatile.Read(ref _minNs) == long.MaxValue ? 0 : Volatile.Read(ref _minNs);
    public long MaxNs => Volatile.Read(ref _maxNs);
    public double MeanNs
    {
        get
        {
            long count = Volatile.Read(ref _totalCount);
            return count == 0 ? 0 : (double)Volatile.Read(ref _sumNs) / count;
        }
    }

    /// <summary>
    /// Records a latency measurement in nanoseconds. Single-threaded hot path.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Record(long elapsedNs)
    {
        if (elapsedNs < 0)
            elapsedNs = 0;
        if (elapsedNs > _maxTrackedNs)
            elapsedNs = _maxTrackedNs;

        _buckets[elapsedNs]++;
        _totalCount++;
        _sumNs += elapsedNs;

        if (elapsedNs < _minNs)
            _minNs = elapsedNs;
        if (elapsedNs > _maxNs)
            _maxNs = elapsedNs;
    }

    /// <summary>
    /// Thread-safe multi-producer recording.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordAtomic(long elapsedNs)
    {
        if (elapsedNs < 0)
            elapsedNs = 0;
        if (elapsedNs > _maxTrackedNs)
            elapsedNs = _maxTrackedNs;

        Interlocked.Increment(ref _buckets[elapsedNs]);
        Interlocked.Increment(ref _totalCount);
        Interlocked.Add(ref _sumNs, elapsedNs);

        // Atomic Min/Max CAS loops
        long currentMin;
        while (elapsedNs < (currentMin = Volatile.Read(ref _minNs)))
        {
            if (Interlocked.CompareExchange(ref _minNs, elapsedNs, currentMin) == currentMin)
                break;
        }

        long currentMax;
        while (elapsedNs > (currentMax = Volatile.Read(ref _maxNs)))
        {
            if (Interlocked.CompareExchange(ref _maxNs, elapsedNs, currentMax) == currentMax)
                break;
        }
    }

    /// <summary>
    /// Computes the latency at a specific percentile (0.0 to 100.0).
    /// </summary>
    public long GetPercentile(double percentile)
    {
        long total = Volatile.Read(ref _totalCount);
        if (total == 0)
            return 0;

        long target = (long)Math.Ceiling((percentile / 100.0) * total);
        long accumulated = 0;

        for (int i = 0; i <= _maxTrackedNs; i++)
        {
            accumulated += _buckets[i];
            if (accumulated >= target)
                return i;
        }

        return _maxTrackedNs;
    }

    /// <summary>
    /// Captures an immutable snapshot of current percentiles without allocations.
    /// </summary>
    public HistogramSnapshot Snapshot()
    {
        return new HistogramSnapshot(
            TotalCount: TotalCount,
            MinNs: MinNs,
            MeanNs: MeanNs,
            P50Ns: GetPercentile(50.0),
            P90Ns: GetPercentile(90.0),
            P99Ns: GetPercentile(99.0),
            P999Ns: GetPercentile(99.9),
            MaxNs: MaxNs
        );
    }

    public void Reset()
    {
        NativeMemory.Clear(_buckets, _allocationSizeBytes);
        _totalCount = 0;
        _sumNs = 0;
        _minNs = long.MaxValue;
        _maxNs = 0;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        if (_buckets != null)
        {
            NativeMemory.AlignedFree(_buckets);
        }
    }
}

public readonly record struct HistogramSnapshot(
    long TotalCount,
    long MinNs,
    double MeanNs,
    long P50Ns,
    long P90Ns,
    long P99Ns,
    long P999Ns,
    long MaxNs
);
