using System.Runtime.InteropServices;

namespace Platform.Memory.Shared;

/// <summary>
/// 4096-byte (1 full OS virtual memory page) fixed header placed at Offset 0
/// of any partitioned shared memory segment or persistent disk flight recorder.
/// Allocates 126 dynamic partition descriptor slots while guaranteeing that Page 0 (metadata)
/// remains isolated from data partition memory writes.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = SuperblockSize)]
public unsafe struct SharedMemorySuperblock
{
    public const int SuperblockSize = 4096;
    public const int HeaderSize = 64;
    public const int DescriptorEntrySize = 32;

    /// <summary>
    /// Maximum number of sub-partitions supported in Page 0:
    /// (4096 total bytes - 64-byte control header) / 32 bytes per descriptor entry = 126.
    /// </summary>
    public const int MaxRegions = (SuperblockSize - HeaderSize) / DescriptorEntrySize;

    // =========================================================================
    // CACHE LINE 0: Identity, Control Plane & Handshake (Offsets 0..63)
    // =========================================================================
    [FieldOffset(0)]
    public uint Magic;

    [FieldOffset(4)]
    public uint AbiVersion;

    [FieldOffset(8)]
    public uint State;

    [FieldOffset(12)]
    public ushort NodeId;

    [FieldOffset(14)]
    public ushort RegionCount;

    [FieldOffset(16)]
    public nuint TotalByteLength;

    [FieldOffset(24)]
    public long CreatedTimestampTicks;

    [FieldOffset(32)]
    public long HeartbeatTicks;

    // Padding completes Cache Line 0
    [FieldOffset(40)]
    private fixed byte _padLine0[24];

    // =========================================================================
    // CACHE LINES 1–3: Partition Descriptors (Offsets 64..255, 6 * 32B = 192B)
    // =========================================================================
    [FieldOffset(64)]
    public fixed byte RegionsRaw[MaxRegions * 32];

    public SharedMemoryRegionEntry* GetRegionEntries()
    {
        fixed (byte* p = RegionsRaw)
        {
            return (SharedMemoryRegionEntry*)p;
        }
    }
}
