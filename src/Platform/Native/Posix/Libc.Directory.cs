using System.Runtime.InteropServices;
using Platform.Native.Posix.Structs;

namespace Platform.Native.Posix;

internal static unsafe partial class Libc
{
    /// <summary>
    /// Opens a directory stream corresponding to the directory path.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/opendir.3.html"/></remarks>
    [LibraryImport(
        LibName,
        EntryPoint = "opendir",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8
    )]
    public static partial nint OpenDir(string name);

    /// <summary>
    /// Returns a pointer to a dirent structure representing the next directory entry.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/readdir.3.html"/></remarks>
    [LibraryImport(LibName, EntryPoint = "readdir", SetLastError = true)]
    public static partial PosixDirent* ReadDir(nint dirp);

    /// <summary>
    /// Closes the directory stream associated with dirp.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/closedir.3.html"/></remarks>
    [LibraryImport(LibName, EntryPoint = "closedir", SetLastError = true)]
    public static partial int CloseDir(nint dirp);
}
