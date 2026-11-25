namespace Platform.Native.Windows.Enums;

/// <summary>
/// Standard generic access rights used with CreateFileW.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/secauthz/generic-access-rights"/></remarks>
[Flags]
internal enum FileAccessRights : uint
{
    /// <summary>Read access.</summary>
    GenericRead = 0x80000000,

    /// <summary>Write access.</summary>
    GenericWrite = 0x40000000,

    /// <summary>All possible access rights.</summary>
    GenericAll = 0x10000000,
}
