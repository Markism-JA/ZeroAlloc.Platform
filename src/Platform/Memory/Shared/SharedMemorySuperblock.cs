using System.Runtime.InteropServices;

namespace Platform.Memory.Shared;

/// <summary>
/// 4096-byte fixed header placed at Offset 0 of any partitioned shared memory segment or persistent disk flight recorder.
/// Holds up to 126 dynamic partition descriptors while isolating Page 0 metadata from user data writes.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = SuperblockSize)]
public unsafe struct SharedMemorySuperblock
{
    /// <summary>Fixed byte size of the superblock structure (4096 bytes).</summary>
    public const int SuperblockSize = 4096;

    /// <summary>Byte size of the Cache Line 0 control header (64 bytes).</summary>
    public const int HeaderSize = 64;

    /// <summary>Byte size of each <see cref="SharedMemoryRegionEntry"/> descriptor (32 bytes).</summary>
    public const int DescriptorEntrySize = 32;

    /// <summary>
    /// Maximum number of sub-partitions supported in Page 0:
    /// (4096 total bytes - 64-byte control header) / 32 bytes per descriptor entry = 126.
    /// </summary>
    public const int MaxRegions = (SuperblockSize - HeaderSize) / DescriptorEntrySize;

    // =========================================================================
    // CACHE LINE 0: Identity, Control Plane & Handshake (Offsets 0..63)
    // =========================================================================

    /// <summary>FourCC or application-defined magic number used to validate segment identity.</summary>
    [FieldOffset(0)]
    public uint Magic;

    /// <summary>Schema/ABI version number verified on peer attachment.</summary>
    [FieldOffset(4)]
    public uint AbiVersion;

    /// <summary>Raw <see cref="SharedMemoryState"/> lifecycle state updated atomically.</summary>
    [FieldOffset(8)]
    public uint State;

    /// <summary>Logical node or process identifier published by the owner.</summary>
    [FieldOffset(12)]
    public ushort NodeId;

    /// <summary>Number of active <see cref="SharedMemoryRegionEntry"/> descriptors in <see cref="RegionsRaw"/>.</summary>
    [FieldOffset(14)]
    public ushort RegionCount;

    /// <summary>Total byte length of the mapped segment including Page 0 and all partitions.</summary>
    [FieldOffset(16)]
    public nuint TotalByteLength;

    /// <summary>Stopwatch timestamp captured when the owner formatted the superblock.</summary>
    [FieldOffset(24)]
    public long CreatedTimestampTicks;

    /// <summary>Latest stopwatch heartbeat timestamp published by the owner for liveness monitoring.</summary>
    [FieldOffset(32)]
    public long HeartbeatTicks;

    // Padding completes Cache Line 0
    [FieldOffset(40)]
    private fixed byte _padLine0[24];

    // =========================================================================
    // PARTITION DESCRIPTORS (Offsets 64..4095)
    // =========================================================================

    /// <summary>Raw backing bytes for up to <see cref="MaxRegions"/> contiguous <see cref="SharedMemoryRegionEntry"/> records.</summary>
    [FieldOffset(64)]
    public fixed byte RegionsRaw[MaxRegions * 32];

    /// <summary>Returns a typed pointer to the first <see cref="SharedMemoryRegionEntry"/> descriptor.</summary>
    public SharedMemoryRegionEntry* GetRegionEntries()
    {
        fixed (byte* p = RegionsRaw)
        {
            return (SharedMemoryRegionEntry*)p;
        }
    }
}
