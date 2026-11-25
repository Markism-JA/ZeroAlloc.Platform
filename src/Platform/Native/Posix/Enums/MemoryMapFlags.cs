namespace Platform.Native.Posix.Enums;

/// <summary>
/// Determines whether updates to the mapping are visible to other processes mapping the same region.
/// </summary>
/// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/mmap.2.html"/></remarks>
[Flags]
internal enum MemoryMapFlags : int
{
    /// <summary>Share this mapping; updates are visible to other processes.</summary>
    Shared = 0x01,

    /// <summary>Create a private copy-on-write mapping.</summary>
    Private = 0x02,

    /// <summary>The mapping is not backed by any file; contents are initialized to zero.</summary>
    Anonymous = 0x20,

    /// <summary>Lock the mapped region into physical memory.</summary>
    Locked = 0x2000,

    /// <summary>Populate (prefault) page tables for a mapping without faulting later.</summary>
    Populate = 0x8000,
}
