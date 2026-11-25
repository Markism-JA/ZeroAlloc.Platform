using System.Diagnostics;
using System.Runtime.CompilerServices;
using Platform.Time;

namespace Platform.Benchmarking.Harness.Pacing;

/// <summary>
/// High-resolution open-loop event pacer.
/// Eliminates Coordinated Omission by maintaining an absolute scheduled schedule,
/// measuring true queue wait times when the consumer pipeline experiences backpressure.
/// </summary>
public struct OpenLoopPacer
{
    private readonly long _intervalTicks;
    private long _nextTick;

    /// <summary>
    /// Initializes a pacer targeting a fixed operations-per-second rate.
    /// </summary>
    /// <param name="targetOpsPerSec">Target throughput rate.</param>
    public OpenLoopPacer(long targetOpsPerSec)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetOpsPerSec);

        _intervalTicks = (long)(Stopwatch.Frequency / (double)targetOpsPerSec);
        _nextTick = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Spins until the scheduled dispatch time arrives, advances schedule by interval,
    /// and returns the absolute scheduled UTC timestamp in nanoseconds for payload latency tagging.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long WaitNext()
    {
        long target = _nextTick;
        while (Stopwatch.GetTimestamp() < target)
        {
            Thread.SpinWait(2);
        }

        _nextTick = target + _intervalTicks;
        return NativeTsc.NowNs();
    }

    /// <summary>
    /// Non-blocking check: returns true if the current scheduled tick has elapsed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdvance(out long scheduledNs)
    {
        long now = Stopwatch.GetTimestamp();
        if (now >= _nextTick)
        {
            _nextTick += _intervalTicks;
            scheduledNs = NativeTsc.NowNs();
            return true;
        }

        scheduledNs = 0;
        return false;
    }

    /// <summary>
    /// Resets the internal schedule to current timestamp.
    /// </summary>
    public void Reset()
    {
        _nextTick = Stopwatch.GetTimestamp();
    }
}
