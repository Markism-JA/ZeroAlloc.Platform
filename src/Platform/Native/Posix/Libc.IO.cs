using System.Runtime.InteropServices;

namespace Platform.Native.Posix;

internal static unsafe partial class Libc
{
    /// <summary>
    /// Writes up to <paramref name="count"/> bytes from the buffer pointed to by <paramref name="buf"/>
    /// to the file referred to by the file descriptor <paramref name="fd"/>.
    /// </summary>
    /// <param name="fd">The target file descriptor.</param>
    /// <param name="buf">Pointer to the unmanaged memory buffer to read from.</param>
    /// <param name="count">Maximum number of bytes to write.</param>
    /// <returns>
    /// On success, the number of bytes written is returned. On error, -1 is returned and <c>errno</c> is set appropriately.
    /// </returns>
    /// <remarks>
    /// See: <see href="https://man7.org/linux/man-pages/man2/write.2.html"/>
    /// </remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "write", SetLastError = true)]
    public static partial nint Write(int fd, byte* buf, nuint count);
#else
    [DllImport(LibName, EntryPoint = "write", SetLastError = true)]
    public static extern nint Write(int fd, byte* buf, nuint count);
#endif
}
