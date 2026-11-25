using System.Runtime.InteropServices;

namespace Platform.Memory.Shared;

/// <summary>
/// Cache-aligned metadata descriptor for a single sub-partition.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 32)]
public struct SharedMemoryRegionEntry
{
    public uint Tag;
    public uint Alignment;
    public nuint Offset;
    public nuint Length;
    public ulong Reserved;
}
