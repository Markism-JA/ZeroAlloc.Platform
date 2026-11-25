using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Memory.Shared;

namespace Platform.Memory.Scanner;

/// <summary>
/// Stack-friendly snapshot of an active shared memory segment discovered by <see cref="SharedMemoryScanner"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct DiscoveredSegment
{
    /// <summary>Snapshot of the discovered segment's Page 0 superblock.</summary>
    public SharedMemorySuperblock Superblock;

    /// <summary>Valid byte length of the UTF-8 segment name stored in <see cref="NameUtf8"/>.</summary>
    public int NameLength;

    /// <summary>Inline UTF-8 buffer holding up to 64 bytes of the discovered segment name.</summary>
    public fixed byte NameUtf8[64];

    /// <summary>Returns a read-only UTF-8 span over the discovered segment name.</summary>
    public readonly ReadOnlySpan<byte> NameSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            fixed (byte* p = NameUtf8)
                return new ReadOnlySpan<byte>(p, NameLength);
        }
    }

    /// <summary>
    /// Returns true if the segment's <see cref="SharedMemorySuperblock.HeartbeatTicks"/> was updated within <paramref name="timeout"/>.
    /// </summary>
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
