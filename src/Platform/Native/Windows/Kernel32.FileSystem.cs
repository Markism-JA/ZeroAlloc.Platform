using System.Runtime.InteropServices;
using Platform.Native.Windows.Enums;

namespace Platform.Native.Windows;

internal static unsafe partial class Kernel32
{
    /// <summary>Sentinel indicating an invalid handle value (-1).</summary>
    public static readonly nint INVALID_HANDLE_VALUE = -1;

    /// <summary>
    /// Creates or opens a file or I/O device.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(
        LibName,
        EntryPoint = "CreateFileW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16
    )]
    public static partial nint CreateFileW(
        string lpFileName,
        FileAccessRights dwDesiredAccess,
        FileShareMode dwShareMode,
        void* lpSecurityAttributes,
        CreationDisposition dwCreationDisposition,
        FileAttributesFlags dwFlagsAndAttributes,
        nint hTemplateFile
    );
#else
    [DllImport(
        LibName,
        EntryPoint = "CreateFileW",
        SetLastError = true,
        CharSet = CharSet.Unicode
    )]
    public static extern nint CreateFileW(
        string lpFileName,
        FileAccessRights dwDesiredAccess,
        FileShareMode dwShareMode,
        void* lpSecurityAttributes,
        CreationDisposition dwCreationDisposition,
        FileAttributesFlags dwFlagsAndAttributes,
        nint hTemplateFile
    );
#endif

    /// <summary>
    /// Flushes the buffers of a specified file to permanent backing storage.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-flushfilebuffers"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "FlushFileBuffers", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FlushFileBuffers(nint hFile);
#else
    [DllImport(LibName, EntryPoint = "FlushFileBuffers", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FlushFileBuffers(nint hFile);
#endif

    /// <summary>
    /// Retrieves the physical size of the specified file.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfilesizeex"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "GetFileSizeEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetFileSizeEx(nint hFile, out long lpFileSize);
#else
    [DllImport(LibName, EntryPoint = "GetFileSizeEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetFileSizeEx(nint hFile, out long lpFileSize);
#endif

    /// <summary>
    /// Opens an existing file mapping object.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-openfilemappingw"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "OpenFileMappingW", SetLastError = true)]
    public static partial nint OpenFileMappingW(
        FileMapAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        char* lpName
    );
#else
    [DllImport(LibName, EntryPoint = "OpenFileMappingW", SetLastError = true)]
    public static extern nint OpenFileMappingW(
        FileMapAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        char* lpName
    );
#endif
    
    /// <summary>
    /// Moves the file pointer of the specified file.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfilepointerex"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "SetFilePointerEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetFilePointerEx(
        nint hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod
    );
#else
    [DllImport(LibName, EntryPoint = "SetFilePointerEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetFilePointerEx(
        nint hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod
    );
#endif

    /// <summary>
    /// Sets the physical file size for the specified file at the current position of the file pointer.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setendoffile"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "SetEndOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetEndOfFile(nint hFile);
#else
    [DllImport(LibName, EntryPoint = "SetEndOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetEndOfFile(nint hFile);
#endif
}
