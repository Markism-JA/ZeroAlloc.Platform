namespace Platform.Native.Windows.Enums;

/// <summary>
/// Action to take on files that exist and do not exist.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew"/></remarks>
internal enum CreationDisposition : uint
{
    /// <summary>Creates a new file, only if it does not already exist.</summary>
    CreateNew = 1,

    /// <summary>Creates a new file, always. Overwrites if existing.</summary>
    CreateAlways = 2,

    /// <summary>Opens a file or device, only if it exists.</summary>
    OpenExisting = 3,

    /// <summary>Opens a file, always. Creates if non-existent.</summary>
    OpenAlways = 4,

    /// <summary>Opens a file and truncates it to zero bytes, only if it exists.</summary>
    TruncateExisting = 5,
}
