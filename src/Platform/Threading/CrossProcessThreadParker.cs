using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Internal;
using Platform.Native.Posix;
using Platform.Native.Posix.Structs;
using Platform.Native.Windows;

namespace Platform.Threading;

/// <summary>
/// High-performance cross-process auto-reset wake synchronization event.
/// Backed by Win32 Named Events on Windows and POSIX Named Semaphores on Linux.
/// </summary>
public sealed unsafe class CrossProcessThreadParker : IDisposable
{
    private const int PosixCreateFlags = 0x0040; // O_CREAT
    private const int DefaultPosixMode = 0b110_110_000; // 0660 (rw-rw----)
    private const int EINTR = 4;

    private readonly void* _handle;
    private readonly string _name;
    private readonly bool _isOwner;
    private int _isDisposed;

    /// <summary>
    /// Raw unmanaged handle pointer (Win32 HANDLE on Windows, sem_t* on Linux).
    /// </summary>
    public void* Handle => _handle;

    private CrossProcessThreadParker(void* handle, string name, bool isOwner)
    {
        _handle = handle;
        _name = name;
        _isOwner = isOwner;
    }

    /// <summary>
    /// Creates or opens a named cross-process auto-reset wake event.
    /// </summary>
    public static CrossProcessThreadParker OpenOrCreate(string name, bool isOwner = false)
    {
        PlatformCompat.ThrowIfNullOrWhiteSpace(name, nameof(name));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string winName =
                name.StartsWith("Global\\", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Local\\", StringComparison.OrdinalIgnoreCase)
                    ? name
                    : "Global\\" + name;

            nint hEvent = Kernel32.CreateEventW(
                null,
                bManualReset: false,
                bInitialState: false,
                winName
            );
            if (hEvent == 0)
            {
                int error = PlatformCompat.GetLastError();
                throw new Win32Exception(error, $"Failed to create/open Win32 event '{winName}'.");
            }

            return new CrossProcessThreadParker((void*)hEvent, winName, isOwner);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            string posixName = name.StartsWith('/') ? name : "/" + name.Replace('\\', '_');
            void* sem = Libc.SemOpen(posixName, PosixCreateFlags, DefaultPosixMode, 0);

            if (sem == Libc.SEM_FAILED || sem == null)
            {
                int error = PlatformCompat.GetLastError();
                throw new IOException(
                    $"Failed to create/open POSIX semaphore '{posixName}' ({error})."
                );
            }

            return new CrossProcessThreadParker(sem, posixName, isOwner);
        }

        throw new PlatformNotSupportedException("Unsupported operating system.");
    }

    /// <summary>
    /// Signals the wake event, unblocking one quiescent receiver thread.
    /// 0-allocation native syscall.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unpark() => Unpark(_handle);

    /// <summary>
    /// Unparks the target thread, transitioning it from BLOCKED back to RUNNABLE in the OS scheduler.
    /// Pure native syscall (SetEvent on Windows, sem_post on Linux).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Unpark(void* handle)
    {
        if (handle == null)
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Kernel32.SetEvent((nint)handle);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Libc.SemPost(handle);
        }
    }

    /// <summary>
    /// Parks the calling thread in the OS scheduler until unparked or timeout expires.
    /// Drops CPU consumption of the calling thread to 0%.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Park(int timeoutMs)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            return false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            uint winTimeout = timeoutMs < 0 ? 0xFFFFFFFFu : (uint)timeoutMs;
            uint result = Kernel32.WaitForSingleObject((nint)_handle, winTimeout);

#if DEBUG
            if (result == Kernel32.WaitFailed)
            {
                int error = PlatformCompat.GetLastError();
                throw new Win32Exception(
                    error,
                    $"WaitForSingleObject failed on handle 0x{(nint)_handle:X}."
                );
            }
#endif

            return result == Kernel32.WaitObject0;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (timeoutMs < 0)
            {
                while (Volatile.Read(ref _isDisposed) == 0)
                {
                    if (Libc.SemWait(_handle) == 0)
                        return true;

                    if (PlatformCompat.GetLastError() != EINTR)
                        return false;
                }

                return false;
            }

            Timespec ts;
            Libc.ClockGetTime(Libc.CLOCK_REALTIME, &ts);

            long sec = (long)ts.TvSec + (timeoutMs / 1000);
            long nsec = ts.TvNsec + ((timeoutMs % 1000) * 1_000_000L);
            if (nsec >= 1_000_000_000L)
            {
                sec += nsec / 1_000_000_000L;
                nsec %= 1_000_000_000L;
            }

            ts.TvSec = (nint)sec;
            ts.TvNsec = (nint)nsec;

            while (Volatile.Read(ref _isDisposed) == 0)
            {
                if (Libc.SemTimedWait(_handle, &ts) == 0)
                    return true;

                if (PlatformCompat.GetLastError() != EINTR)
                    return false;
            }

            return false;
        }

        return false;
    }

    /// <summary>Closes the OS handle and, if owner on Linux, unlinks the named POSIX semaphore. Safe to call multiple times.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Kernel32.CloseHandle((nint)_handle);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Libc.SemClose(_handle);
            if (_isOwner)
            {
                Libc.SemUnlink(_name);
            }
        }
    }
}
