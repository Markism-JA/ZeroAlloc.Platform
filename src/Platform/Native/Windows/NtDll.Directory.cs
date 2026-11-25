using System.Runtime.InteropServices;
using Platform.Native.Windows.Enums;
using Platform.Native.Windows.Structs;

namespace Platform.Native.Windows;

/// <summary>
/// Low-level native routines from ntdll.dll for NT Object Manager directory queries.
/// </summary>
internal static unsafe partial class NtDll
{
    private const string LibName = "ntdll.dll";

    /// <summary>
    /// Opens an existing directory object in the NT object manager namespace.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/devnotes/ntopendirectoryobject"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "NtOpenDirectoryObject")]
    public static partial int NtOpenDirectoryObject(
        out nint directoryHandle,
        DirectoryAccessRights desiredAccess,
        in ObjectAttributes objectAttributes
    );
#else
    [DllImport(LibName, EntryPoint = "NtOpenDirectoryObject")]
    public static extern int NtOpenDirectoryObject(
        out nint directoryHandle,
        DirectoryAccessRights desiredAccess,
        in ObjectAttributes objectAttributes
    );
#endif

    /// <summary>
    /// Queries an open NT directory object for entries (such as Section mappings).
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/devnotes/ntquerydirectoryobject"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "NtQueryDirectoryObject")]
    public static partial int NtQueryDirectoryObject(
        nint directoryHandle,
        byte* buffer,
        int length,
        [MarshalAs(UnmanagedType.Bool)] bool returnSingleEntry,
        [MarshalAs(UnmanagedType.Bool)] bool restartScan,
        ref uint context,
        out uint returnLength
    );
#else
    [DllImport(LibName, EntryPoint = "NtQueryDirectoryObject")]
    public static extern int NtQueryDirectoryObject(
        nint directoryHandle,
        byte* buffer,
        int length,
        [MarshalAs(UnmanagedType.Bool)] bool returnSingleEntry,
        [MarshalAs(UnmanagedType.Bool)] bool restartScan,
        ref uint context,
        out uint returnLength
    );
#endif

    /// <summary>
    /// Closes an open NT object handle.
    /// </summary>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "NtClose")]
    public static partial int NtClose(nint handle);
#else
    [DllImport(LibName, EntryPoint = "NtClose")]
    public static extern int NtClose(nint handle);
#endif
}
