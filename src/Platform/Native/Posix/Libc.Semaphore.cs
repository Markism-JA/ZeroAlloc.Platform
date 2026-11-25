using System.Runtime.InteropServices;
using Platform.Native.Posix.Structs;

namespace Platform.Native.Posix;

internal static unsafe partial class Libc
{
    public const int CLOCK_REALTIME = 0;
    public const int CLOCK_MONOTONIC = 1;

    /// <summary>Sentinel pointer returned by <see cref="SemOpen"/> on failure.</summary>
    public static readonly void* SEM_FAILED = (void*)-1;

    /// <summary>
    /// Initializes and opens named semaphore.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_open.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(
        LibName,
        EntryPoint = "sem_open",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8
    )]
    public static partial void* SemOpen(string name, int oflag, int mode, uint value);
#else
    [DllImport(
        LibName,
        EntryPoint = "sem_open",
        SetLastError = true,
        CharSet = CharSet.Ansi
    )]
    public static extern void* SemOpen(string name, int oflag, int mode, uint value);
#endif

    /// <summary>
    /// Unlocks (increments) the semaphore, waking any waiting threads.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_post.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "sem_post", SetLastError = true)]
    public static partial int SemPost(void* sem);
#else
    [DllImport(LibName, EntryPoint = "sem_post", SetLastError = true)]
    public static extern int SemPost(void* sem);
#endif

    /// <summary>
    /// Decrements (locks) the semaphore. If the count is 0, waits until available or timeout elapses.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_timedwait.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "sem_timedwait", SetLastError = true)]
    public static partial int SemTimedWait(void* sem, Timespec* absTimeout);
#else
    [DllImport(LibName, EntryPoint = "sem_timedwait", SetLastError = true)]
    public static extern int SemTimedWait(void* sem, Timespec* absTimeout);
#endif

    /// <summary>
    /// Non-blocking try-decrement of the semaphore.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_trywait.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "sem_trywait", SetLastError = true)]
    public static partial int SemTryWait(void* sem);
#else
    [DllImport(LibName, EntryPoint = "sem_trywait", SetLastError = true)]
    public static extern int SemTryWait(void* sem);
#endif

    /// <summary>
    /// Closes the named semaphore.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_close.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "sem_close", SetLastError = true)]
    public static partial int SemClose(void* sem);
#else
    [DllImport(LibName, EntryPoint = "sem_close", SetLastError = true)]
    public static extern int SemClose(void* sem);
#endif

    /// <summary>
    /// Removes the named semaphore from the system.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_unlink.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(
        LibName,
        EntryPoint = "sem_unlink",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf8
    )]
    public static partial int SemUnlink(string name);
#else
    [DllImport(
        LibName,
        EntryPoint = "sem_unlink",
        SetLastError = true,
        CharSet = CharSet.Ansi
    )]
    public static extern int SemUnlink(string name);
#endif

    /// <summary>
    /// Initializes unnamed semaphore at the specified memory location.
    /// </summary>
    /// <param name="sem">Pointer to the unmanaged memory (at least 32 bytes on x86_64) allocated for sem_t.</param>
    /// <param name="pshared">0 for threads of a process; non-zero for shared between processes.</param>
    /// <param name="value">Initial value for the semaphore.</param>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_init.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "sem_init", SetLastError = true)]
    public static partial int SemInit(void* sem, int pshared, uint value);
#else
    [DllImport(LibName, EntryPoint = "sem_init", SetLastError = true)]
    public static extern int SemInit(void* sem, int pshared, uint value);
#endif

    /// <summary>
    /// Destroys unnamed semaphore previously initialized with <see cref="SemInit"/>.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_destroy.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "sem_destroy", SetLastError = true)]
    public static partial int SemDestroy(void* sem);
#else
    [DllImport(LibName, EntryPoint = "sem_destroy", SetLastError = true)]
    public static extern int SemDestroy(void* sem);
#endif

    /// <summary>
    /// Decrements (locks) the semaphore. Blocks indefinitely until the count is greater than 0.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_wait.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "sem_wait", SetLastError = true)]
    public static partial int SemWait(void* sem);
#else
    [DllImport(LibName, EntryPoint = "sem_wait", SetLastError = true)]
    public static extern int SemWait(void* sem);
#endif

    /// <summary>
    /// Retrieves the current value of the semaphore.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/sem_getvalue.3.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "sem_getvalue", SetLastError = true)]
    public static partial int SemGetValue(void* sem, int* sval);
#else
    [DllImport(LibName, EntryPoint = "sem_getvalue", SetLastError = true)]
    public static extern int SemGetValue(void* sem, int* sval);
#endif

    /// <summary>
    /// Retrieves the time of the specified clock.
    /// </summary>
    /// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/clock_gettime.2.html"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "clock_gettime", SetLastError = true)]
    public static partial int ClockGetTime(int clockId, Timespec* tp);
#else
    [DllImport(LibName, EntryPoint = "clock_gettime", SetLastError = true)]
    public static extern int ClockGetTime(int clockId, Timespec* tp);
#endif
}
