using System.Runtime.InteropServices;

namespace Platform.Time;

/// <summary>
/// 64-byte cache-line-aligned clock control block supporting live hardware sampling
/// or deterministic manual time overrides in shared or unmanaged memory.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct PlatformClockBlock
{
    /// <summary>Manual UTC Unix epoch timestamp in nanoseconds.</summary>
    [FieldOffset(0)]
    public long UtcEpochNs;

    /// <summary>Manual UTC Unix epoch timestamp in milliseconds.</summary>
    [FieldOffset(8)]
    public long UtcEpochMs;

    /// <summary>Monotonic counter incremented each time manual time is updated.</summary>
    [FieldOffset(16)]
    public long MonotonicTicks;

    /// <summary>Mode flag: <c>0</c> for live hardware clock, non-zero for deterministic manual mode.</summary>
    [FieldOffset(24)]
    public int IsManual;
}
