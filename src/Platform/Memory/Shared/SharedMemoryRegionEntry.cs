using System.Runtime.InteropServices;

namespace Platform.Memory.Shared;

/// <summary>
/// 32-byte cache-aligned metadata descriptor for a single sub-partition inside a <see cref="SharedMemorySuperblock"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 32)]
public struct SharedMemoryRegionEntry
{
    /// <summary>Unique 32-bit partition identifier (typically a <see cref="FourCC"/> or enum value).</summary>
    public uint Tag;

    /// <summary>Required power-of-two byte alignment for this partition.</summary>
    public uint Alignment;

    /// <summary>Byte offset from the start of the mapped segment.</summary>
    public nuint Offset;

    /// <summary>Total byte length of the partition.</summary>
    public nuint Length;

    /// <summary>Reserved padding for future metadata extensions.</summary>
    public ulong Reserved;
}
