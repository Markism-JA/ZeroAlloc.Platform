namespace Platform.Memory.Shared;

/// <summary>
/// Lifecycle state published in Page 0 of a <see cref="SharedMemorySuperblock"/>.
/// </summary>
public enum SharedMemoryState : uint
{
    /// <summary>Memory has been zero-allocated but not yet formatted.</summary>
    Uninitialized = 0,

    /// <summary>Owner is currently formatting the superblock and partition table.</summary>
    Initializing = 1,

    /// <summary>Superblock and all partitions are formatted and safe for peers to attach.</summary>
    Ready = 2,

    /// <summary>Owner encountered an unrecoverable fault; peers should abort attachment.</summary>
    Faulted = 3,

    /// <summary>Owner has cleanly disposed the session and is tearing down the segment.</summary>
    TornDown = 4,
}
