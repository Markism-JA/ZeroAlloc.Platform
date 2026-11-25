using System.Runtime.InteropServices;

namespace Platform.Time;

[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct PlatformClockBlock
{
    // Epoch timestamp in nanoseconds (fits standard wire envelopes)
    [FieldOffset(0)]
    public long UtcEpochNs;

    // Epoch timestamp in milliseconds for coarse sweeps and TTLs
    [FieldOffset(8)]
    public long UtcEpochMs;

    // Monotonic counter (increments every time the clock advances)
    [FieldOffset(16)]
    public long MonotonicTicks;

    // 0 = Normal/Live (updated by ticker), 1 = Simulated/Manual (driven externally)
    [FieldOffset(24)]
    public int IsManual;
}
