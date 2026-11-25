namespace Platform.Native.Windows.Enums;

/// <summary>
/// Sharing mode of an open file or device handle.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew"/></remarks>
[Flags]
internal enum FileShareMode : uint
{
    /// <summary>Prevents other processes from opening a file if they request delete, read, or write access.</summary>
    None = 0x00000000,

    /// <summary>Enables subsequent open operations on a file to request read access.</summary>
    Read = 0x00000001,

    /// <summary>Enables subsequent open operations on a file to request write access.</summary>
    Write = 0x00000002,

    /// <summary>Enables subsequent open operations on a file to request delete access.</summary>
    Delete = 0x00000004,
}
