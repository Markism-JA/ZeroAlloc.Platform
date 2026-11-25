using System.Runtime.InteropServices;
using Platform.Native.Windows.Enums;
using Platform.Native.Windows.Structs;

namespace Platform.Native.Windows;

internal static unsafe partial class Kernel32
{
    private const string LibName = "kernel32.dll";

    /// <summary>
    /// Creates or opens a named or unnamed file mapping object for a specified file.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createfilemappingw"/></remarks>
    [LibraryImport(
        LibName,
        EntryPoint = "CreateFileMappingW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16
    )]
    public static partial nint CreateFileMappingW(
        nint hFile,
        void* lpFileMappingAttributes,
        uint flProtect,
        uint dwMaximumSizeHigh,
        uint dwMaximumSizeLow,
        string? lpName
    );

    /// <summary>
    /// Opens a named file mapping object.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-openfilemappingw"/></remarks>
    [LibraryImport(
        LibName,
        EntryPoint = "OpenFileMappingW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16
    )]
    public static partial nint OpenFileMappingW(
        FileMapAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        string lpName
    );

    /// <summary>
    /// Maps a view of a file mapping into the address space of a calling process.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-mapviewoffile"/></remarks>
    [LibraryImport(LibName, EntryPoint = "MapViewOfFile", SetLastError = true)]
    public static partial void* MapViewOfFile(
        nint hFileMappingObject,
        FileMapAccess dwDesiredAccess,
        uint dwFileOffsetHigh,
        uint dwFileOffsetLow,
        nuint dwNumberOfBytesToMap
    );

    /// <summary>
    /// Unmaps a mapped view of a file from the calling process's address space.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-unmapviewoffile"/></remarks>
    [LibraryImport(LibName, EntryPoint = "UnmapViewOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnmapViewOfFile(void* lpBaseAddress);

    /// <summary>
    /// Closes an open object handle.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-closehandle"/></remarks>
    [LibraryImport(LibName, EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);

    /// <summary>
    /// Writes to the disk a byte range within a mapped view of a file.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-flushviewoffile"/></remarks>
    [LibraryImport(LibName, EntryPoint = "FlushViewOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FlushViewOfFile(void* lpBaseAddress, nuint dwNumberOfBytesToFlush);

    /// <summary>
    /// Locks the specified region of the process's virtual address space into physical memory.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-virtuallock"/></remarks>
    [LibraryImport(LibName, EntryPoint = "VirtualLock", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VirtualLock(void* lpAddress, nuint dwSize);

    /// <summary>
    /// Prefetches virtual memory pages into physical memory.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-prefetchvirtualmemory"/></remarks>
    [LibraryImport(LibName, EntryPoint = "PrefetchVirtualMemory", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PrefetchVirtualMemory(
        nint hProcess,
        nuint numberOfEntries,
        Win32MemoryRangeEntry* virtualAddresses,
        uint flags
    );

    [LibraryImport(LibName, EntryPoint = "VirtualAlloc", SetLastError = true)]
    public static partial void* VirtualAlloc(
        void* lpAddress,
        nuint dwSize,
        uint flAllocationType,
        uint flProtect
    );

    [LibraryImport(LibName, EntryPoint = "VirtualFree", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VirtualFree(void* lpAddress, nuint dwSize, uint dwFreeType);
}
