using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Native.Posix;
using Platform.Native.Posix.Structs;
using Platform.Native.Windows;

namespace Platform.Threading;

public sealed unsafe class ThreadParker : IDisposable
{
    public const int StateRunning = 0;
    public const int StateParked = 1;
    public const int StateSignaled = 2;

    private const int EINTR = 4;
    private const int ETIMEDOUT = 110;

    // Layout in unified block:
    // [0..31]  : OS primitive (sem_t 32 bytes on Linux, nint 8 bytes on Windows)
    // [32..35] : int isSleepingState
    private const nuint BlockAlignment = 16;
    private const nuint UnifiedBlockSize = 64; // Plenty of room for 32-byte sem_t + 4-byte state + padding

    private readonly void* _block;
    private readonly void* _handle;
    private readonly int* _isSleepingState;

    private int _isDisposed;
    private int _activeWaiters;

    public NativeThreadParkerHandle NativeHandle => new(_handle, _isSleepingState);
    public int* SleepingStatePointer => _isSleepingState;

    public ThreadParker()
    {
        // 16-byte aligned 64-byte block guarantees valid alignment for sem_t and int
        _block = NativeMemory.AlignedAlloc(UnifiedBlockSize, BlockAlignment);
        NativeMemory.Clear(_block, UnifiedBlockSize);

        _handle = _block;
        _isSleepingState = (int*)((byte*)_block + 32);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint hEvent = Kernel32.CreateEventW(null, false, false, null);
            if (hEvent == 0)
            {
                NativeMemory.AlignedFree(_block);
                int error = Marshal.GetLastPInvokeError();
                throw new Win32Exception(error, "Failed to create anonymous Win32 event.");
            }
            *(nint*)_handle = hEvent;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (Libc.SemInit(_handle, pshared: 0, value: 0) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                NativeMemory.AlignedFree(_block);
                throw new IOException($"Failed to initialize POSIX semaphore ({error}).");
            }
        }
        else
        {
            NativeMemory.AlignedFree(_block);
            throw new PlatformNotSupportedException("Unsupported operating system.");
        }
    }

    /// <summary>
    /// Pre-arms the parker before checking work queues.
    /// If already Running (0), transitions atomically to Parked (1) and returns true.
    /// If Signaled (2), consumes the token back to Running (0), drains any OS post, and returns false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryArm()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            return false;

        while (true)
        {
            int state = Volatile.Read(ref *_isSleepingState);

            switch (state)
            {
                case StateRunning:
                    // Normal path: 0 -> 1 (Armed)
                    if (
                        Interlocked.CompareExchange(
                            ref *_isSleepingState,
                            StateParked,
                            StateRunning
                        ) == StateRunning
                    )
                    {
                        return true;
                    }
                    break;

                case StateSignaled:
                    // Pre-signaled: Consume token 2 -> 0 so we don't get trapped
                    if (
                        Interlocked.CompareExchange(
                            ref *_isSleepingState,
                            StateRunning,
                            StateSignaled
                        ) == StateSignaled
                    )
                    {
                        // Drain any native token in the OS primitive
                        DrainNativeTokens();
                        return false;
                    }
                    break;

                default:
                    // Already parked or invalid
                    return false;
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrainNativeTokens()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            while (Volatile.Read(ref _isDisposed) == 0 && Libc.SemTryWait(_handle) == 0) { }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint hEvent = *(nint*)_handle;
            if (hEvent != 0)
            {
                Kernel32.WaitForSingleObject(hEvent, 0);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Disarm()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            return;

        Interlocked.CompareExchange(ref *_isSleepingState, StateRunning, StateParked);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Park(int timeoutMs)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            return false;

        // Register active entry so Dispose() cannot free memory under this thread
        Interlocked.Increment(ref _activeWaiters);
        try
        {
            if (Volatile.Read(ref _isDisposed) != 0)
                return false;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                nint hEvent = *(nint*)_handle;
                try
                {
                    uint result = Kernel32.WaitForSingleObject(hEvent, (uint)timeoutMs);
                    return result == Kernel32.WaitObject0;
                }
                finally
                {
                    Volatile.Write(ref *_isSleepingState, StateRunning);
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Consume early signal if present
                if (
                    Interlocked.CompareExchange(ref *_isSleepingState, StateRunning, StateSignaled)
                    == StateSignaled
                )
                {
                    while (Libc.SemTryWait(_handle) == 0) { }
                    return true;
                }

                Interlocked.CompareExchange(ref *_isSleepingState, StateParked, StateRunning);

                bool acquired = false;
                try
                {
                    if (timeoutMs < 0)
                    {
                        while (Volatile.Read(ref _isDisposed) == 0 && Libc.SemWait(_handle) != 0)
                        {
                            if (Marshal.GetLastPInvokeError() != EINTR)
                                break;
                        }
                        acquired = true;
                    }
                    else
                    {
                        Timespec ts;
                        Libc.ClockGetTime(Libc.CLOCK_REALTIME, &ts);

                        long sec = (long)ts.TvSec + (timeoutMs / 1000);
                        long nsec = ts.TvNsec + (timeoutMs % 1000 * 1_000_000L);

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
                            {
                                acquired = true;
                                break;
                            }

                            int err = Marshal.GetLastPInvokeError();
                            if (err == EINTR)
                                continue;

                            break;
                        }
                    }
                }
                finally
                {
                    Volatile.Write(ref *_isSleepingState, StateRunning);
                    while (Libc.SemTryWait(_handle) == 0) { }
                }

                return acquired;
            }

            return false;
        }
        finally
        {
            Interlocked.Decrement(ref _activeWaiters);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unpark() => NativeHandle.Unpark();

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        // Wake any waiter currently blocked
        NativeHandle.Unpark();

        // Spin until all threads have completely evacuated Park() and its finally block
        var spinner = new SpinWait();
        while (Volatile.Read(ref _activeWaiters) > 0)
        {
            NativeHandle.Unpark();
            spinner.SpinOnce();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint hEvent = *(nint*)_handle;
            if (hEvent != 0)
            {
                Kernel32.CloseHandle(hEvent);
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Libc.SemDestroy(_handle);
        }

        NativeMemory.AlignedFree(_block);
    }
}
