namespace Platform.Native.Posix.Enums;

/// <summary>
/// Desired memory protection of the mapping.
/// </summary>
/// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/mmap.2.html"/></remarks>
[Flags]
internal enum MemoryMapProt : int
{
    /// <summary>Pages may not be accessed.</summary>
    None = 0,

    /// <summary>Pages may be read.</summary>
    Read = 1,

    /// <summary>Pages may be written.</summary>
    Write = 2,

    /// <summary>Pages may be executed.</summary>
    Execute = 4,
}
