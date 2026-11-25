using System.Runtime.InteropServices;

namespace Platform.Native.Posix;

internal static unsafe partial class Libc
{
    /// <summary>
    /// Obtains the identifier of the calling thread.
    /// </summary>
    /// <remarks>
    /// See: <see href="https://man7.org/linux/man-pages/man3/pthread_self.3.html"/>
    /// </remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "pthread_self")]
    public static partial nint PthreadSelf();
#else
    [DllImport(LibName, EntryPoint = "pthread_self")]
    public static extern nint PthreadSelf();
#endif

    /// <summary>
    /// Sets the CPU affinity mask of the target thread.
    /// </summary>
    /// <param name="thread">The thread ID whose affinity is to be modified.</param>
    /// <param name="cpusetsize">Length of the buffer pointed to by <paramref name="cpuset"/> in bytes.</param>
    /// <param name="cpuset">Pointer to the bit mask specifying CPU affinity.</param>
    /// <returns>0 on success; otherwise a non-zero POSIX errno code.</returns>
    /// <remarks>
    /// Notice: <c>pthread_*</c> functions return the error number directly on failure instead of setting <c>errno</c>.
    /// See: <see href="https://man7.org/linux/man-pages/man3/pthread_setaffinity_np.3.html"/>
    /// </remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "pthread_setaffinity_np")]
    public static partial int PthreadSetAffinityNp(nint thread, nuint cpusetsize, ulong* cpuset);
#else
    [DllImport(LibName, EntryPoint = "pthread_setaffinity_np")]
    public static extern int PthreadSetAffinityNp(nint thread, nuint cpusetsize, ulong* cpuset);
#endif
}
