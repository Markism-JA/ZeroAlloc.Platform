namespace Platform.Native.Posix.Enums;

/// <summary>
/// Flags for flushing mapped memory to backing storage.
/// </summary>
/// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/msync.2.html"/></remarks>
[Flags]
internal enum MsyncFlags : int
{
    /// <summary>Requests that an update be scheduled, returning immediately.</summary>
    Async = 1,

    /// <summary>Invalidates other mappings of the same file so they can be refreshed.</summary>
    Invalidate = 2,

    /// <summary>Requests an update and blocks until execution completes.</summary>
    Sync = 4,
}
