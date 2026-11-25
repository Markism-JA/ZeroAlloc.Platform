namespace Platform.Memory;

[Flags]
public enum MemoryMapOptions : byte
{
    None = 0,

    /// <summary>
    /// Pre-faults all physical pages into RAM immediately upon mapping
    /// (Linux: MAP_POPULATE / madvise MADV_WILLNEED; Windows: VirtualLock).
    /// Prevents page-fault jitter on hot data paths.
    /// </summary>
    PreFault = 1,

    /// <summary>
    /// Uses POSIX tmpfs (/dev/shm) for named shared memory instead of disk files.
    /// </summary>
    AnonymousShared = 2,

    /// <summary>
    /// Advise kernel that sequential streaming access will dominate.
    /// </summary>
    Sequential = 4,

    /// <summary>
    /// Advise kernel that random memory access will dominate.
    /// </summary>
    RandomAccess = 8,
}
