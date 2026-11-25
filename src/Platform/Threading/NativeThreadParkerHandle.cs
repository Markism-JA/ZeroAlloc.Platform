using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Native.Posix;
using Platform.Native.Windows;

namespace Platform.Threading;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public readonly unsafe struct NativeThreadParkerHandle
{
    private const int StateRunning = 0;
    private const int StateParked = 1;
    private const int StateSignaled = 2;

    private readonly void* _osHandle;
    private readonly int* _isSleepingFlag;
    private readonly delegate*<void*, void> _managedUnparkCallback;

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

    public void* RawHandle => _osHandle;
    public int* SleepingFlag => _isSleepingFlag;
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
