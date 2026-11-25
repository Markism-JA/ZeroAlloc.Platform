using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Memory.Allocators;
using Platform.Internal;
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

    // Layout in unified 64-byte cache-line-aligned block:
    // [0..31]  : OS primitive (sem_t 32 bytes on Linux, nint 8 bytes on Windows)
    // [32..35] : int isSleepingState
    private const int BlockAlignment = 64;
    private const int UnifiedBlockSize = 64;

    private readonly void* _block;
    private readonly void* _handle;
    private readonly int* _isSleepingState;

    // Managed fallback for macOS / iOS / Android / WebGL where unnamed POSIX sem_init is unsupported
    private readonly AutoResetEvent? _managedEvent;
    private GCHandle _managedGcHandle;

    private int _isDisposed;
    private int _activeWaiters;

    public NativeThreadParkerHandle NativeHandle =>
        _managedEvent != null
            ? new NativeThreadParkerHandle(
                (void*)GCHandle.ToIntPtr(_managedGcHandle),
                _isSleepingState,
                &OnManagedUnparkTrampoline
            )
            : new NativeThreadParkerHandle(_handle, _isSleepingState);

    public int* SleepingStatePointer => _isSleepingState;

    public bool IsSleeping
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref *_isSleepingState) == StateParked;
    }

    public ThreadParker()
        : this(forceManagedFallback: false) { }

    internal ThreadParker(bool forceManagedFallback)
    {
        _block = HeapAllocator.Allocate(UnifiedBlockSize, BlockAlignment);
        Unsafe.InitBlockUnaligned(_block, 0, UnifiedBlockSize);

        _handle = _block;
        _isSleepingState = (int*)((byte*)_block + 32);

        if (!forceManagedFallback && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint hEvent = Kernel32.CreateEventW(null, false, false, null);
            if (hEvent == 0)
            {
                HeapAllocator.Free(_block);
                int error = PlatformCompat.GetLastError();
                throw new Win32Exception(error, "Failed to create anonymous Win32 event.");
            }
            *(nint*)_handle = hEvent;
        }
        else if (!forceManagedFallback && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (Libc.SemInit(_handle, pshared: 0, value: 0) != 0)
            {
                int error = PlatformCompat.GetLastError();
                HeapAllocator.Free(_block);
                throw new IOException($"Failed to initialize POSIX semaphore ({error}).");
            }
        }
        else
        {
            // Fallback for macOS / mobile / sandboxed runtimes
            _managedEvent = new AutoResetEvent(false);
            _managedGcHandle = GCHandle.Alloc(_managedEvent, GCHandleType.Normal);
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
        if (_managedEvent != null)
        {
            _managedEvent.Reset();
            return;
        }

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

            if (_managedEvent != null)
            {
                if (
                    Interlocked.CompareExchange(ref *_isSleepingState, StateRunning, StateSignaled)
                    == StateSignaled
                )
                {
                    _managedEvent.Reset();
                    return true;
                }

                Interlocked.CompareExchange(ref *_isSleepingState, StateParked, StateRunning);
                try
                {
                    return _managedEvent.WaitOne(timeoutMs);
                }
                finally
                {
                    Volatile.Write(ref *_isSleepingState, StateRunning);
                    _managedEvent.Reset();
                }
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (
                    Interlocked.CompareExchange(ref *_isSleepingState, StateRunning, StateSignaled)
                    == StateSignaled
                )
                {
                    nint hEv = *(nint*)_handle;
                    if (hEv != 0)
                        Kernel32.WaitForSingleObject(hEv, 0);
                    return true;
                }

                Interlocked.CompareExchange(ref *_isSleepingState, StateParked, StateRunning);
                nint hEvent = *(nint*)_handle;
                uint winTimeout = timeoutMs < 0 ? 0xFFFFFFFFu : (uint)timeoutMs;
                try
                {
                    uint result = Kernel32.WaitForSingleObject(hEvent, winTimeout);
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
                        while (Volatile.Read(ref _isDisposed) == 0)
                        {
                            if (Libc.SemWait(_handle) == 0)
                            {
                                acquired = true;
                                break;
                            }

                            if (PlatformCompat.GetLastError() != EINTR)
                                break;
                        }
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

                            int err = PlatformCompat.GetLastError();
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

    private static void OnManagedUnparkTrampoline(void* context)
    {
        if (context == null)
            return;

        var handle = GCHandle.FromIntPtr((IntPtr)context);
        if (handle.IsAllocated && handle.Target is AutoResetEvent ev)
        {
            ev.Set();
        }
    }

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

        if (_managedEvent != null)
        {
            _managedEvent.Dispose();
            if (_managedGcHandle.IsAllocated)
                _managedGcHandle.Free();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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

        HeapAllocator.Free(_block);
    }
}
