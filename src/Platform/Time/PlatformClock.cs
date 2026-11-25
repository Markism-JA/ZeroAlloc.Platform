using System.Runtime.CompilerServices;

namespace Platform.Time;

/// <summary>
/// Zero-allocation clock facade reading either the live <see cref="NativeTsc"/> clock
/// or a deterministic manual <see cref="PlatformClockBlock"/>.
/// </summary>
public static unsafe class PlatformClock
{
    /// <summary>Gets the current live UTC Unix epoch timestamp in nanoseconds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowNs() => NativeTsc.NowNs();

    /// <summary>Gets the current live UTC Unix epoch timestamp in milliseconds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowMs() => NativeTsc.NowMs();

    /// <summary>
    /// Gets the current UTC Unix epoch timestamp in nanoseconds from <paramref name="clock"/> if in manual mode,
    /// or from <see cref="NativeTsc.NowNs"/> when <paramref name="clock"/> is null, near-null (&lt; 4096), or live.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowNs(PlatformClockBlock* clock)
    {
        // If clock is null, or points to an offset < 4KB (e.g. &((DiagnosticGate*)null)->Clock)
        if ((nuint)clock < 4096 || clock->IsManual == 0)
        {
            return NativeTsc.NowNs();
        }

        return Volatile.Read(ref clock->UtcEpochNs);
    }

    /// <summary>
    /// Gets the current UTC Unix epoch timestamp in milliseconds from <paramref name="clock"/> if in manual mode,
    /// or from <see cref="NativeTsc.NowMs"/> when <paramref name="clock"/> is null, near-null (&lt; 4096), or live.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowMs(PlatformClockBlock* clock)
    {
        if ((nuint)clock < 4096 || clock->IsManual == 0)
        {
            return NativeTsc.NowMs();
        }

        return Volatile.Read(ref clock->UtcEpochMs);
    }

    /// <summary>
    /// Switches <paramref name="clock"/> into deterministic manual mode and sets its UTC Unix epoch timestamp in nanoseconds.
    /// </summary>
    public static void SetManualTime(PlatformClockBlock* clock, long utcEpochNs)
    {
        clock->IsManual = 1;
        Volatile.Write(ref clock->UtcEpochNs, utcEpochNs);
        Volatile.Write(ref clock->UtcEpochMs, utcEpochNs / 1_000_000L);
        Interlocked.Increment(ref clock->MonotonicTicks);
    }

    /// <summary>
    /// Advances a manual <paramref name="clock"/> forward by <paramref name="delta"/>.
    /// </summary>
    public static void AdvanceTime(PlatformClockBlock* clock, TimeSpan delta)
    {
        long current = Volatile.Read(ref clock->UtcEpochNs);
        SetManualTime(clock, current + (delta.Ticks * 100L));
    }

    /// <summary>
    /// Restores <paramref name="clock"/> back to live hardware sampling mode.
    /// </summary>
    public static void ResetToLive(PlatformClockBlock* clock)
    {
        Volatile.Write(ref clock->IsManual, 0);
    }
}
