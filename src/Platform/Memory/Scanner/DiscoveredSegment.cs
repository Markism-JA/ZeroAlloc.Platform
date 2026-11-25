using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Memory.Shared;

namespace Platform.Memory.Scanner;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct DiscoveredSegment
{
    public SharedMemorySuperblock Superblock;
    public int NameLength;
    public fixed byte NameUtf8[64];

    public readonly ReadOnlySpan<byte> NameSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            fixed (byte* p = NameUtf8)
                return new ReadOnlySpan<byte>(p, NameLength);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly bool IsHeartbeatAlive(TimeSpan timeout)
    {
        long ticks = Superblock.HeartbeatTicks;
        if (ticks == 0)
            return false;
        long elapsed = Stopwatch.GetTimestamp() - ticks;
        return elapsed >= 0 && elapsed <= (long)(timeout.TotalSeconds * Stopwatch.Frequency);
    }
}
