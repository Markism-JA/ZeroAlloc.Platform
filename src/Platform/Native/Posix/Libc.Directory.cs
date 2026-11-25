using System.Runtime.InteropServices;
using Platform.Native.Posix.Structs;

namespace Platform.Native.Posix;

internal static unsafe partial class Libc
{
    /// <summary>
    /// Opens a directory stream corresponding to the directory path.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/opendir.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(
        LibName,
        EntryPoint = "opendir",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8
    )]
    public static partial nint OpenDir(string name);
#else
    [DllImport(
        LibName,
        EntryPoint = "opendir",
        SetLastError = true,
        CharSet = CharSet.Ansi
    )]
    public static extern nint OpenDir(string name);
#endif

    /// <summary>
    /// Returns a pointer to a dirent structure representing the next directory entry.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/readdir.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "readdir", SetLastError = true)]
    public static partial PosixDirent* ReadDir(nint dirp);
#else
    [DllImport(LibName, EntryPoint = "readdir", SetLastError = true)]
    public static extern PosixDirent* ReadDir(nint dirp);
#endif

    /// <summary>
    /// Closes the directory stream associated with dirp.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/closedir.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "closedir", SetLastError = true)]
    public static partial int CloseDir(nint dirp);
#else
    [DllImport(LibName, EntryPoint = "closedir", SetLastError = true)]
    public static extern int CloseDir(nint dirp);
#endif
}
