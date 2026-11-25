using System.Runtime.CompilerServices;

namespace Platform.Time;

public static unsafe class PlatformClock
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowNs() => NativeTsc.NowNs();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowMs() => NativeTsc.NowMs();

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
    /// Forces manual time mode for deterministic testing without Thread.Sleep.
    /// </summary>
    public static void SetManualTime(PlatformClockBlock* clock, long utcEpochNs)
    {
        clock->IsManual = 1;
        Volatile.Write(ref clock->UtcEpochNs, utcEpochNs);
        Volatile.Write(ref clock->UtcEpochMs, utcEpochNs / 1_000_000L);
        Interlocked.Increment(ref clock->MonotonicTicks);
    }

    public static void AdvanceTime(PlatformClockBlock* clock, TimeSpan delta)
    {
        long current = Volatile.Read(ref clock->UtcEpochNs);
        SetManualTime(clock, current + (delta.Ticks * 100L));
    }

    /// <summary>
    /// Restores the clock block back to live hardware sampling mode.
    /// </summary>
    public static void ResetToLive(PlatformClockBlock* clock)
    {
        Volatile.Write(ref clock->IsManual, 0);
    }
}
