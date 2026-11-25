namespace Platform.Native.Posix.Enums;

/// <summary>
/// File access and creation flags for open and shm_open.
/// </summary>
/// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/open.2.html"/></remarks>
[Flags]
internal enum PosixOpenFlags : int
{
    /// <summary>Open for reading only.</summary>
    ReadOnly = 0,

    /// <summary>Open for writing only.</summary>
    WriteOnly = 1,

    /// <summary>Open for reading and writing.</summary>
    ReadWrite = 2,

    /// <summary>Create file if it does not exist.</summary>
    Create = 0x40,

    /// <summary>Fail if Create is set and file exists.</summary>
    Exclusive = 0x80,

    /// <summary>Truncate file length to 0 upon opening.</summary>
    Truncate = 0x200,

    /// <summary>Write operations complete according to synchronized I/O data integrity.</summary>
    DSync = 0x1000,

    /// <summary>Write operations complete according to synchronized I/O file integrity.</summary>
    Sync = 0x101000,
}
