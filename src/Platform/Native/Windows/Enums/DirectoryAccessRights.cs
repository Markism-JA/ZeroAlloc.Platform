namespace Platform.Native.Windows.Enums;

/// <summary>
/// Access rights for opening NT Object Manager directory objects.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/devnotes/ntopendirectoryobject"/></remarks>
[Flags]
internal enum DirectoryAccessRights : uint
{
    /// <summary>Permission flag to query directory entries.</summary>
    Query = 0x0001,
}
