using System.Diagnostics;
using System.Runtime.CompilerServices;
using Platform.Time;

namespace Platform.Benchmarking.Harness.Pacing;

/// <summary>
/// Unified rate regulator supporting both maximum-throughput saturation and open-loop pacing.
/// </summary>
public struct RateLimiter
{
    private readonly RateLimiterMode _mode;
    private readonly long _intervalTicks;
    private long _nextTick;

    public readonly RateLimiterMode Mode => _mode;
    public long TargetOpsPerSec { get; }

    public RateLimiter(RateLimiterMode mode, long targetOpsPerSec = 0)
    {
        _mode = mode;
        TargetOpsPerSec = targetOpsPerSec;

        if (mode == RateLimiterMode.Paced)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetOpsPerSec);
            _intervalTicks = (long)(Stopwatch.Frequency / (double)targetOpsPerSec);
        }
        else
        {
            _intervalTicks = 0;
        }

        _nextTick = Stopwatch.GetTimestamp();
    }

    public static RateLimiter CreateSaturated() => new(RateLimiterMode.Saturated);

    public static RateLimiter CreatePaced(long targetOpsPerSec) =>
        new(RateLimiterMode.Paced, targetOpsPerSec);

    /// <summary>
    /// Acquires permission for the next dispatch.
    /// In Paced mode, spins until the scheduled TSC timestamp arrives.
    /// In Saturated mode, returns immediately.
    /// </summary>
    /// <returns>Current timestamp in nanoseconds for latency measurement.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long AcquireNext()
    {
        if (_mode == RateLimiterMode.Saturated)
        {
            return NativeTsc.NowNs();
        }

        long target = _nextTick;
        while (Stopwatch.GetTimestamp() < target)
        {
            Thread.SpinWait(2);
        }

        _nextTick = target + _intervalTicks;
        return NativeTsc.NowNs();
    }

    /// <summary>
    /// Resets the internal schedule to current timestamp.
    /// </summary>
    public void Reset()
    {
        _nextTick = Stopwatch.GetTimestamp();
    }
}
