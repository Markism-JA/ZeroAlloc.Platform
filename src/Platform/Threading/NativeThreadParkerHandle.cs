using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Native.Posix;
using Platform.Native.Windows;

namespace Platform.Threading;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly unsafe struct NativeThreadParkerHandle(void* osHandle, int* isSleepingFlag)
{
    private const int StateRunning = 0;
    private const int StateParked = 1;
    private const int StateSignaled = 2;

    public void* RawHandle => osHandle;
    public int* SleepingFlag => isSleepingFlag;
    public bool IsAllocated => osHandle != null;

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
        if (osHandle == null)
            return;

        if (isSleepingFlag != null)
        {
            // Atomically latch StateSignaled.
            // If previous state was StateParked (1), the thread is armed or sleeping in sem_timedwait -> wake kernel.
            // If previous state was StateRunning (0), TryArm() will fail on the consumer side -> skip syscall.
            // If previous state was StateSignaled (2), already signaled -> skip syscall.
            int prevState = Interlocked.Exchange(ref *isSleepingFlag, StateSignaled);
            if (prevState != StateParked)
            {
                return;
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Libc.SemPost(osHandle);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Kernel32.SetEvent((nint)osHandle);
        }
    }
}
