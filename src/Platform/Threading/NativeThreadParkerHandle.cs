using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Native.Posix;
using Platform.Native.Windows;

namespace Platform.Threading;

/// <summary>
/// Blittable value-type handle for unparking a <see cref="ThreadParker"/> from producer threads without touching managed references.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly unsafe struct NativeThreadParkerHandle
{
    private const int StateRunning = 0;
    private const int StateParked = 1;
    private const int StateSignaled = 2;

    private readonly void* _osHandle;
    private readonly int* _isSleepingFlag;
    private readonly delegate*<void*, void> _managedUnparkCallback;

    /// <summary>Creates a native parker handle bound to an OS synchronization primitive and an atomic state flag pointer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativeThreadParkerHandle(void* osHandle, int* isSleepingFlag)
    {
        _osHandle = osHandle;
        _isSleepingFlag = isSleepingFlag;
        _managedUnparkCallback = null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal NativeThreadParkerHandle(
        void* managedContext,
        int* isSleepingFlag,
        delegate*<void*, void> managedUnparkCallback
    )
    {
        _osHandle = managedContext;
        _isSleepingFlag = isSleepingFlag;
        _managedUnparkCallback = managedUnparkCallback;
    }

    /// <summary>Raw pointer to the underlying OS primitive or managed trampoline context.</summary>
    public void* RawHandle => _osHandle;

    /// <summary>Pointer to the 64-byte-aligned atomic sleep state flag (<c>0</c>=Running, <c>1</c>=Parked, <c>2</c>=Signaled).</summary>
    public int* SleepingFlag => _isSleepingFlag;

    /// <summary>True if this handle points to an initialized parker.</summary>
    public bool IsAllocated => _osHandle != null;

    /// <summary>
    /// Unparks the target thread if it is currently parked or armed to park.
    /// Safely elides kernel syscalls if the target thread is already signaled or actively running.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WakeIfSleeping() => Unpark();

    /// <summary>
    /// Atomically transitions the waiter state to Signaled and issues an OS wake event
    /// only if the waiter is currently armed or sleeping in kernel space.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unpark()
    {
        if (_osHandle == null)
            return;

        if (_isSleepingFlag != null)
        {
            // Atomically latch StateSignaled.
            // If previous state was StateParked (1), the thread is armed or sleeping -> wake kernel/event.
            // If previous state was StateRunning (0), TryArm() will fail on the consumer side -> skip syscall.
            // If previous state was StateSignaled (2), already signaled -> skip syscall.
            int prevState = Interlocked.Exchange(ref *_isSleepingFlag, StateSignaled);
            if (prevState != StateParked)
            {
                return;
            }
        }

        if (_managedUnparkCallback != null)
        {
            _managedUnparkCallback(_osHandle);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Libc.SemPost(_osHandle);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            nint hEvent = *(nint*)_osHandle;
            if (hEvent != 0)
            {
                Kernel32.SetEvent(hEvent);
            }
        }
    }
}
