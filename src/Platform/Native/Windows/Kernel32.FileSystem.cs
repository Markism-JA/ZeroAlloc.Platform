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

    /// <summary>
    /// Flushes the buffers of a specified file to permanent backing storage.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-flushfilebuffers"/></remarks>
    [LibraryImport(LibName, EntryPoint = "FlushFileBuffers", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FlushFileBuffers(nint hFile);

    /// <summary>
    /// Retrieves the physical size of the specified file.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfilesizeex"/></remarks>
    [LibraryImport(LibName, EntryPoint = "GetFileSizeEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetFileSizeEx(nint hFile, out long lpFileSize);

    /// <summary>
    /// Opens an existing file mapping object.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-openfilemappingw"/></remarks>
    [LibraryImport(LibName, EntryPoint = "OpenFileMappingW", SetLastError = true)]
    public static partial nint OpenFileMappingW(
        FileMapAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        char* lpName
    );
    
    /// <summary>
    /// Moves the file pointer of the specified file.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setfilepointerex"/></remarks>
    [LibraryImport(LibName, EntryPoint = "SetFilePointerEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetFilePointerEx(
        nint hFile,
        long liDistanceToMove,
        out long lpNewFilePointer,
        uint dwMoveMethod
    );

    /// <summary>
    /// Sets the physical file size for the specified file at the current position of the file pointer.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-setendoffile"/></remarks>
    [LibraryImport(LibName, EntryPoint = "SetEndOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetEndOfFile(nint hFile);
}
