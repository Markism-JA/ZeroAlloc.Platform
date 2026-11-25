using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Platform.Client.Time;

/// <summary>
/// High-resolution, zero-allocation monotonic clock for client runtimes.
/// Safe across all Unity platforms (Mono, IL2CPP, iOS, Android, WebGL, Consoles).
/// </summary>
public static class ClientClock
{
    private static readonly double TicksToNs = 1_000_000_000.0 / Stopwatch.Frequency;
    private static readonly double TicksToMs = 1_000.0 / Stopwatch.Frequency;

    private static readonly long UnixEpochAnchorNs;
    private static readonly long StopwatchAnchorTicks;

    // Optional manual simulation offset for deterministic testing/replays
    private static long _manualTimeOffsetNs;
    private static int _isManualMode;

    static ClientClock()
    {
        StopwatchAnchorTicks = Stopwatch.GetTimestamp();
        UnixEpochAnchorNs = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) * 100L;
    }

    /// <summary>
    /// Gets the current timestamp in monotonic nanoseconds, anchored to the UTC Unix Epoch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowNs()
    {
        if (Volatile.Read(ref _isManualMode) != 0)
        {
            return Volatile.Read(ref _manualTimeOffsetNs);
        }

        long elapsedTicks = Stopwatch.GetTimestamp() - StopwatchAnchorTicks;
        return UnixEpochAnchorNs + (long)(elapsedTicks * TicksToNs);
    }

    /// <summary>
    /// Gets the current timestamp in monotonic milliseconds.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowMs() => NowNs() / 1_000_000L;

    /// <summary>
    /// Gets raw stopwatch monotonic nanoseconds (independent of UTC wall-clock).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long MonotonicNs()
    {
        if (Volatile.Read(ref _isManualMode) != 0)
        {
            return Volatile.Read(ref _manualTimeOffsetNs);
        }

        return (long)(Stopwatch.GetTimestamp() * TicksToNs);
    }

    #region Deterministic Simulation & Testing Controls

    /// <summary>
    /// Freezes the clock at a specific timestamp for deterministic tick testing.
    /// </summary>
    public static void SetManualTime(long utcEpochNs)
    {
        Volatile.Write(ref _manualTimeOffsetNs, utcEpochNs);
        Volatile.Write(ref _isManualMode, 1);
    }

    /// <summary>
    /// Steps the frozen clock forward by a discrete duration.
    /// </summary>
    public static void AdvanceTime(TimeSpan delta)
    {
        if (Volatile.Read(ref _isManualMode) == 0)
            return;

        long current = Volatile.Read(ref _manualTimeOffsetNs);
        Volatile.Write(ref _manualTimeOffsetNs, current + (delta.Ticks * 100L));
    }

    /// <summary>
    /// Restores the clock to live hardware sampling mode.
    /// </summary>
    public static void ResetToLive()
    {
        Volatile.Write(ref _isManualMode, 0);
    }

    #endregion
}
