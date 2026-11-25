namespace Platform.Memory;

/// <summary>
/// Advisory and paging options for <see cref="NativeMemoryMap"/> creation and attachment.
/// </summary>
[Flags]
public enum MemoryMapOptions : byte
{
    /// <summary>Use default OS demand paging and access heuristics.</summary>
    None = 0,

    /// <summary>
    /// Pre-faults physical pages into RAM immediately upon mapping
    /// (Linux: <c>MAP_POPULATE</c>; macOS: <c>MADV_WILLNEED</c>; Windows: <c>PrefetchVirtualMemory</c>).
    /// Prevents page-fault jitter on hot data paths.
    /// </summary>
    PreFault = 1,

    /// <summary>
    /// Uses POSIX tmpfs (<c>/dev/shm</c>) for named shared memory instead of disk files.
    /// </summary>
    AnonymousShared = 2,

    /// <summary>
    /// Advises the kernel that sequential streaming access will dominate.
    /// </summary>
    Sequential = 4,

    /// <summary>
    /// Advises the kernel that random memory access will dominate.
    /// </summary>
    RandomAccess = 8,
}
