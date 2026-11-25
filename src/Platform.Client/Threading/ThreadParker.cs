using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Memory.Allocators;

namespace Platform.Client.Threading;

/// <summary>
/// Managed parking primitive using standard CLR synchronization.
/// Guaranteed safe across all Unity targets, Mono, and IL2CPP without native platform libraries.
/// Employs an atomic two-phase handshake (TryArm/Disarm) to prevent missed-signal races.
/// </summary>
public sealed unsafe class ThreadParker : IDisposable
{
    private readonly AutoResetEvent _waitEvent = new(false);
    private readonly int* _isSleepingState;
    private GCHandle _gcHandle;

    private int _isDisposed;
    private int _activeWaiters;

    public ThreadParkerHandle Handle { get; }
    public int* SleepingStatePointer => _isSleepingState;

    public bool IsSleeping
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Volatile.Read(ref *_isSleepingState) == ThreadParkerHandle.StateParked;
    }

    public ThreadParker()
    {
        // 64-byte aligned integer prevents false sharing on the sleep state flag
        _isSleepingState = (int*)HeapAllocator.Allocate(sizeof(int), 64);
        *_isSleepingState = ThreadParkerHandle.StateRunning;

        _gcHandle = GCHandle.Alloc(this, GCHandleType.Normal);

        Handle = new ThreadParkerHandle(
            _isSleepingState,
            &OnUnparkTrampoline,
            (void*)GCHandle.ToIntPtr(_gcHandle)
        );
    }

    /// <summary>
    /// Atomically pre-arms the parker before verifying work queues.
    /// If Running (0), transitions atomically to Parked (1) and returns true.
    /// If Signaled (2), consumes the token back to Running (0), resets the CLR wait event, and returns false.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryArm()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return false;
        }

        while (true)
        {
            int state = Volatile.Read(ref *_isSleepingState);

            switch (state)
            {
                case ThreadParkerHandle.StateRunning:
                    // Normal path: 0 -> 1 (Armed)
                    if (
                        Interlocked.CompareExchange(
                            ref *_isSleepingState,
                            ThreadParkerHandle.StateParked,
                            ThreadParkerHandle.StateRunning
                        ) == ThreadParkerHandle.StateRunning
                    )
                    {
                        return true;
                    }
                    break;

                case ThreadParkerHandle.StateSignaled:
                    // Work arrived before arming completed: consume signal (2 -> 0) and abort sleep
                    if (
                        Interlocked.CompareExchange(
                            ref *_isSleepingState,
                            ThreadParkerHandle.StateRunning,
                            ThreadParkerHandle.StateSignaled
                        ) == ThreadParkerHandle.StateSignaled
                    )
                    {
                        // Drain any buffered signal in the AutoResetEvent
                        _waitEvent.Reset();
                        return false;
                    }
                    break;

                default:
                    // Already Parked or invalid state
                    return false;
            }
        }
    }

    /// <summary>
    /// Disarms the parker if work arrived during safety checks (Parked -> Running).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Disarm()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return;
        }

        Interlocked.CompareExchange(
            ref *_isSleepingState,
            ThreadParkerHandle.StateRunning,
            ThreadParkerHandle.StateParked
        );
    }

    /// <summary>
    /// Parks the calling thread until awakened or until timeout expires.
    /// Restores StateRunning upon return.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Park(int timeoutMs)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return false;
        }

        // Check if unpark was signaled prior to parking
        if (
            Interlocked.CompareExchange(
                ref *_isSleepingState,
                ThreadParkerHandle.StateRunning,
                ThreadParkerHandle.StateSignaled
            ) == ThreadParkerHandle.StateSignaled
        )
        {
            _waitEvent.Reset();
            return true;
        }

        // Ensure state is Parked
        Interlocked.CompareExchange(
            ref *_isSleepingState,
            ThreadParkerHandle.StateParked,
            ThreadParkerHandle.StateRunning
        );

        Interlocked.Increment(ref _activeWaiters);
        try
        {
            if (Volatile.Read(ref _isDisposed) != 0)
            {
                return false;
            }

            if (timeoutMs < 0)
            {
                return _waitEvent.WaitOne();
            }

            return _waitEvent.WaitOne(timeoutMs);
        }
        finally
        {
            Volatile.Write(ref *_isSleepingState, ThreadParkerHandle.StateRunning);
            _waitEvent.Reset();
            Interlocked.Decrement(ref _activeWaiters);
        }
    }

    /// <summary>
    /// Signals the waiter thread and unblocks it from kernel wait.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unpark()
    {
        if (Volatile.Read(ref _isDisposed) != 0)
        {
            return;
        }

        int prevState = Interlocked.Exchange(
            ref *_isSleepingState,
            ThreadParkerHandle.StateSignaled
        );

        // Only pulse the underlying OS synchronization event if the thread is actually armed/sleeping
        if (prevState == ThreadParkerHandle.StateParked)
        {
            _waitEvent.Set();
        }
    }

    private static void OnUnparkTrampoline(void* context)
    {
        if (context == null)
        {
            return;
        }

        var handle = GCHandle.FromIntPtr((IntPtr)context);
        if (handle.IsAllocated && handle.Target is ThreadParker target)
        {
            target.Unpark();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        // Wake any waiter currently blocked
        Unpark();

        // Spin until all threads have completely evacuated Park() and its finally block
        var spinner = new SpinWait();
        while (Volatile.Read(ref _activeWaiters) > 0)
        {
            Unpark();
            spinner.SpinOnce();
        }

        _waitEvent.Dispose();

        if (_gcHandle.IsAllocated)
        {
            _gcHandle.Free();
        }

        if (_isSleepingState != null)
        {
            HeapAllocator.Free(_isSleepingState);
        }
    }
}
