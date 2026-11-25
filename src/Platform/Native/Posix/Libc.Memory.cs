using System.Runtime.InteropServices;
using Platform.Native.Posix.Enums;

namespace Platform.Native.Posix;

internal static unsafe partial class Libc
{
    private const string LibName = "libc";

    /// <summary>Sentinel pointer returned by <see cref="Mmap"/> on failure.</summary>
    public static readonly void* MAP_FAILED = (void*)-1;

    /// <summary>
    /// Creates a new memory mapping in the virtual address space of the calling process.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/mmap.2.html"/></remarks>
    [LibraryImport(LibName, EntryPoint = "mmap", SetLastError = true)]
    public static partial void* Mmap(
        void* addr,
        nuint length,
        MemoryMapProt prot,
        MemoryMapFlags flags,
        int fd,
        nint offset
    );

    /// <summary>
    /// Deletes the mappings for the specified address range.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/munmap.2.html"/></remarks>
    [LibraryImport(LibName, EntryPoint = "munmap", SetLastError = true)]
    public static partial int Munmap(void* addr, nuint length);

    /// <summary>
    /// Gives advice or directions to the kernel about an address range.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/madvise.2.html"/></remarks>
    [LibraryImport(LibName, EntryPoint = "madvise", SetLastError = true)]
    public static partial int Madvise(void* addr, nuint length, MadviseAdvice advice);

    /// <summary>
    /// Flushes changes made to the in-core copy of a mapped file back to storage.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/msync.2.html"/></remarks>
    [LibraryImport(LibName, EntryPoint = "msync", SetLastError = true)]
    public static partial int Msync(void* addr, nuint length, MsyncFlags flags);
}
