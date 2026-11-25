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
#if NET7_0_OR_GREATER
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
#else
    [DllImport(
        LibName,
        EntryPoint = "CreateFileMappingW",
        SetLastError = true,
        CharSet = CharSet.Unicode
    )]
    public static extern nint CreateFileMappingW(
        nint hFile,
        void* lpFileMappingAttributes,
        uint flProtect,
        uint dwMaximumSizeHigh,
        uint dwMaximumSizeLow,
        string? lpName
    );
#endif

    /// <summary>
    /// Opens a named file mapping object.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-openfilemappingw"/></remarks>
#if NET7_0_OR_GREATER
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
#else
    [DllImport(
        LibName,
        EntryPoint = "OpenFileMappingW",
        SetLastError = true,
        CharSet = CharSet.Unicode
    )]
    public static extern nint OpenFileMappingW(
        FileMapAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        string lpName
    );
#endif

    /// <summary>
    /// Maps a view of a file mapping into the address space of a calling process.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-mapviewoffile"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "MapViewOfFile", SetLastError = true)]
    public static partial void* MapViewOfFile(
        nint hFileMappingObject,
        FileMapAccess dwDesiredAccess,
        uint dwFileOffsetHigh,
        uint dwFileOffsetLow,
        nuint dwNumberOfBytesToMap
    );
#else
    [DllImport(LibName, EntryPoint = "MapViewOfFile", SetLastError = true)]
    public static extern void* MapViewOfFile(
        nint hFileMappingObject,
        FileMapAccess dwDesiredAccess,
        uint dwFileOffsetHigh,
        uint dwFileOffsetLow,
        nuint dwNumberOfBytesToMap
    );
#endif

    /// <summary>
    /// Unmaps a mapped view of a file from the calling process's address space.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-unmapviewoffile"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "UnmapViewOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnmapViewOfFile(void* lpBaseAddress);
#else
    [DllImport(LibName, EntryPoint = "UnmapViewOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnmapViewOfFile(void* lpBaseAddress);
#endif

    /// <summary>
    /// Closes an open object handle.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-closehandle"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint hObject);
#else
    [DllImport(LibName, EntryPoint = "CloseHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(nint hObject);
#endif

    /// <summary>
    /// Writes to the disk a byte range within a mapped view of a file.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-flushviewoffile"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "FlushViewOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool FlushViewOfFile(void* lpBaseAddress, nuint dwNumberOfBytesToFlush);
#else
    [DllImport(LibName, EntryPoint = "FlushViewOfFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool FlushViewOfFile(void* lpBaseAddress, nuint dwNumberOfBytesToFlush);
#endif

    /// <summary>
    /// Locks the specified region of the process's virtual address space into physical memory.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-virtuallock"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "VirtualLock", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VirtualLock(void* lpAddress, nuint dwSize);
#else
    [DllImport(LibName, EntryPoint = "VirtualLock", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VirtualLock(void* lpAddress, nuint dwSize);
#endif

    /// <summary>
    /// Prefetches virtual memory pages into physical memory.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-prefetchvirtualmemory"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "PrefetchVirtualMemory", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool PrefetchVirtualMemory(
        nint hProcess,
        nuint numberOfEntries,
        Win32MemoryRangeEntry* virtualAddresses,
        uint flags
    );
#else
    [DllImport(LibName, EntryPoint = "PrefetchVirtualMemory", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrefetchVirtualMemory(
        nint hProcess,
        nuint numberOfEntries,
        Win32MemoryRangeEntry* virtualAddresses,
        uint flags
    );
#endif

#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "VirtualAlloc", SetLastError = true)]
    public static partial void* VirtualAlloc(
        void* lpAddress,
        nuint dwSize,
        uint flAllocationType,
        uint flProtect
    );
#else
    [DllImport(LibName, EntryPoint = "VirtualAlloc", SetLastError = true)]
    public static extern void* VirtualAlloc(
        void* lpAddress,
        nuint dwSize,
        uint flAllocationType,
        uint flProtect
    );
#endif

#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "VirtualFree", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VirtualFree(void* lpAddress, nuint dwSize, uint dwFreeType);
#else
    [DllImport(LibName, EntryPoint = "VirtualFree", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VirtualFree(void* lpAddress, nuint dwSize, uint dwFreeType);
#endif
}
