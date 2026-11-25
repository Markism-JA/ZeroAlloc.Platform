using System.Runtime.InteropServices;
using Platform.Native.Posix.Enums;
using Platform.Native.Posix.Structs;

namespace Platform.Native.Posix;

internal static partial class Libc
{
    /// <summary>
    /// Opens or creates a POSIX shared memory object.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/shm_open.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(
        LibName,
        EntryPoint = "shm_open",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8
    )]
    public static partial int Shm_open(string name, PosixOpenFlags oflag, int mode);
#else
    [DllImport(
        LibName,
        EntryPoint = "shm_open",
        SetLastError = true,
        CharSet = CharSet.Ansi
    )]
    public static extern int Shm_open(string name, PosixOpenFlags oflag, int mode);
#endif

    /// <summary>
    /// Overload of <see cref="Shm_open(string, PosixOpenFlags, int)"/> that takes a null-terminated byte array.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/shm_open.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "shm_open", SetLastError = true)]
    public static unsafe partial int Shm_open(byte* name, PosixOpenFlags oflag, int mode);
#else
    [DllImport(LibName, EntryPoint = "shm_open", SetLastError = true)]
    public static unsafe extern int Shm_open(byte* name, PosixOpenFlags oflag, int mode);
#endif

    /// <summary>
    /// Removes a POSIX shared memory object name previously created by shm_open.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/shm_unlink.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(
        LibName,
        EntryPoint = "shm_unlink",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8
    )]
    public static partial int Shm_unlink(string name);
#else
    [DllImport(
        LibName,
        EntryPoint = "shm_unlink",
        SetLastError = true,
        CharSet = CharSet.Ansi
    )]
    public static extern int Shm_unlink(string name);
#endif

    /// <summary>
    /// Opens a file at the specified filesystem path.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/open.2.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(
        LibName,
        EntryPoint = "open",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8
    )]
    public static partial int Open(string pathname, PosixOpenFlags flags, int mode);
#else
    [DllImport(
        LibName,
        EntryPoint = "open",
        SetLastError = true,
        CharSet = CharSet.Ansi
    )]
    public static extern int Open(string pathname, PosixOpenFlags flags, int mode);
#endif

    /// <summary>
    /// Closes an open file descriptor.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/close.2.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "close", SetLastError = true)]
    public static partial int Close(int fd);
#else
    [DllImport(LibName, EntryPoint = "close", SetLastError = true)]
    public static extern int Close(int fd);
#endif

    /// <summary>
    /// Truncates an open file to a specified length.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/ftruncate.2.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "ftruncate", SetLastError = true)]
    public static partial int Ftruncate(int fd, nint length);
#else
    [DllImport(LibName, EntryPoint = "ftruncate", SetLastError = true)]
    public static extern int Ftruncate(int fd, nint length);
#endif

    /// <summary>
    /// Allocates disk space for a file descriptor up front to prevent subsequent SIGBUS errors.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/posix_fallocate.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "posix_fallocate")]
    public static partial int Posix_Fallocate(int fd, nint offset, nint len);
#else
    [DllImport(LibName, EntryPoint = "posix_fallocate")]
    public static extern int Posix_Fallocate(int fd, nint offset, nint len);
#endif

    /// <summary>
    /// Retrieves file status by descriptor.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/fstat.2.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "fstat", SetLastError = true)]
    public static partial int Fstat(int fd, out Stat buf);
#else
    [DllImport(LibName, EntryPoint = "fstat", SetLastError = true)]
    public static extern int Fstat(int fd, out Stat buf);
#endif

#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "fstat", SetLastError = true)]
    public static partial int FstatDarwinArm64(int fd, out DarwinStat buf);
#else
    [DllImport(LibName, EntryPoint = "fstat", SetLastError = true)]
    public static extern int FstatDarwinArm64(int fd, out DarwinStat buf);
#endif

#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "fstat$INODE64", SetLastError = true)]
    public static partial int FstatDarwinX64(int fd, out DarwinStat buf);
#else
    [DllImport(LibName, EntryPoint = "fstat$INODE64", SetLastError = true)]
    public static extern int FstatDarwinX64(int fd, out DarwinStat buf);
#endif

#if NET7_0_OR_GREATER
    [LibraryImport(
        LibName,
        EntryPoint = "unlink",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8
    )]
    public static partial int Unlink(string pathname);
#else
    [DllImport(LibName, EntryPoint = "unlink", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern int Unlink(string pathname);
#endif

    /// <summary>
    /// Synchronizes a file's in-core state with storage device (data and metadata).
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/fsync.2.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "fsync", SetLastError = true)]
    public static partial int Fsync(int fd);
#else
    [DllImport(LibName, EntryPoint = "fsync", SetLastError = true)]
    public static extern int Fsync(int fd);
#endif

    /// <summary>
    /// Synchronizes a file's in-core data state with storage device (excludes non-essential metadata).
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/fdatasync.2.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "fdatasync", SetLastError = true)]
    public static partial int Fdatasync(int fd);
#else
    [DllImport(LibName, EntryPoint = "fdatasync", SetLastError = true)]
    public static extern int Fdatasync(int fd);
#endif

    /// <summary>
    /// Sends a signal to a process or process group.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/kill.2.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "kill", SetLastError = true)]
    public static partial int Kill(int pid, int sig);
#else
    [DllImport(LibName, EntryPoint = "kill", SetLastError = true)]
    public static extern int Kill(int pid, int sig);
#endif
}
