namespace Platform.Native.Windows.Enums;

/// <summary>
/// Access rights for mapping views of file mapping objects.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-mapviewoffile"/></remarks>
[Flags]
internal enum FileMapAccess : uint
{
    /// <summary>A copy-on-write view of the file is mapped.</summary>
    Copy = 0x0001,

    /// <summary>A read/write view of the file is mapped.</summary>
    Write = 0x0002,

    /// <summary>A read-only view of the file is mapped.</summary>
    Read = 0x0004,

    /// <summary>An executable view of the file is mapped.</summary>
    Execute = 0x0020,

    /// <summary>A read/write view of the file is mapped with all access rights.</summary>
    AllAccess = 0x000F001F,
}
