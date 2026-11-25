using System.Diagnostics;
using System.Runtime.CompilerServices;
using Platform.Threading;

namespace Platform.Tests.Threading;

public unsafe class ThreadParkerTests
{
    private const int StateRunning = ThreadParker.StateRunning;
    private const int StateParked = ThreadParker.StateParked;
    private const int StateSignaled = ThreadParker.StateSignaled;

    [Fact]
    public void NativeHandle_IsBlittableAndExpectedSize()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<NativeThreadParkerHandle>());
        Assert.Equal(sizeof(nint) * 2, sizeof(NativeThreadParkerHandle));
    }

    [Fact]
    public void InitialState_IsRunning()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        Assert.True(handle.IsAllocated);
        Assert.NotEqual(0, (nint)handle.SleepingFlag);
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));
    }

    [Fact]
    public void TryArm_TransitionsRunningToParked()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        Assert.True(parker.TryArm());
        Assert.Equal(StateParked, Volatile.Read(ref *handle.SleepingFlag));

        // Disarm resets back to Running
        parker.Disarm();
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));
    }

    [Fact]
    public void TryArm_FailsWhenAlreadySignaled()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        // Producer signals before consumer arms
        handle.Unpark();
        Assert.Equal(StateSignaled, Volatile.Read(ref *handle.SleepingFlag));

        // TryArm must fail to indicate work was received
        Assert.False(parker.TryArm());

        // Subsequent Park immediately consumes the signal and resets to StateRunning
        bool acquired = parker.Park(timeoutMs: 1000);
        Assert.True(acquired);
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));
    }

    [Fact]
    public void Park_TimesOutWhenNotSignaled()
    {
        using var parker = new ThreadParker();

        Assert.True(parker.TryArm());

        var sw = Stopwatch.StartNew();
        bool signaled = parker.Park(timeoutMs: 50);
        sw.Stop();

        Assert.False(signaled);
        Assert.Equal(StateRunning, Volatile.Read(ref *parker.NativeHandle.SleepingFlag));
        Assert.True(
            sw.ElapsedMilliseconds >= 30,
            $"Expected timeout ~50ms, elapsed: {sw.ElapsedMilliseconds}ms"
        );
    }

    [Fact]
    public void Unpark_WakesParkedThreadImmediately()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;
        bool parkResult = false;

        using var threadEnteredPark = new ManualResetEventSlim(false);

        var task = Task.Run(() =>
        {
            if (parker.TryArm())
            {
                threadEnteredPark.Set();
                parkResult = parker.Park(timeoutMs: 5000);
            }
        });

        Assert.True(
            threadEnteredPark.Wait(TimeSpan.FromSeconds(2)),
            "Worker thread failed to arm."
        );

        // Ensure worker has entered kernel wait before unparking
        SpinWait.SpinUntil(
            () => Volatile.Read(ref *handle.SleepingFlag) == StateParked,
            TimeSpan.FromSeconds(1)
        );

        handle.Unpark();

        Assert.True(task.Wait(TimeSpan.FromSeconds(2)), "Park did not wake within timeout.");
        Assert.True(parkResult);
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));
    }

    [Fact]
    public void Unpark_WhileArmed_BypassesKernelWait()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        // 1. Arm
        Assert.True(parker.TryArm());

        // 2. Upstream signals before Park() is invoked
        handle.Unpark();

        // 3. Park() must observe the signal and return immediately without sleeping
        var sw = Stopwatch.StartNew();
        bool acquired = parker.Park(timeoutMs: 5000);
        sw.Stop();

        Assert.True(acquired);
        Assert.True(
            sw.ElapsedMilliseconds < 250,
            $"Expected immediate bypass, took {sw.ElapsedMilliseconds}ms"
        );
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));
    }

    [Fact]
    public void WakeIfSleeping_WorksIdenticalToUnparkUnderAtomicProtocol()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        // Ensure WakeIfSleeping safely signals when armed
        Assert.True(parker.TryArm());
        handle.WakeIfSleeping();

        bool acquired = parker.Park(timeoutMs: 5000);
        Assert.True(acquired);
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));
    }

    [Fact]
    public void MultipleUnparks_DoNotCausePhantomParkFallThrough()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        // Flood unpark signals (stress testing POSIX counting semaphore balance)
        for (int i = 0; i < 100; i++)
        {
            handle.Unpark();
        }

        // Cycle 1: Pre-signaled, must return immediately
        Assert.False(parker.TryArm());
        bool firstPark = parker.Park(timeoutMs: 500);
        Assert.True(firstPark);
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));

        // Cycle 2: All surplus signals must have been drained; this pass MUST block and timeout
        Assert.True(parker.TryArm());

        var sw = Stopwatch.StartNew();
        bool secondPark = parker.Park(timeoutMs: 50);
        sw.Stop();

        Assert.False(secondPark);
        Assert.True(
            sw.ElapsedMilliseconds >= 30,
            $"Phantom fall-through detected! Took {sw.ElapsedMilliseconds}ms"
        );
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));
    }

    [Fact]
    public void ConcurrentProducers_HammeringWake_WakesThreadSafely()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;
        const int iterations = 50;

        for (int i = 0; i < iterations; i++)
        {
            bool woke = false;
            using var barrier = new Barrier(5);

            var consumerTask = Task.Run(() =>
            {
                barrier.SignalAndWait();
                if (parker.TryArm())
                {
                    woke = parker.Park(timeoutMs: 2000);
                }
                else
                {
                    // Pre-signaled before TryArm
                    woke = parker.Park(timeoutMs: 2000);
                }
            });

            var producerTasks = new Task[4];
            for (int p = 0; p < producerTasks.Length; p++)
            {
                int producerId = p;
                producerTasks[p] = Task.Run(() =>
                {
                    barrier.SignalAndWait();
                    if (producerId > 0)
                    {
                        Thread.SpinWait(producerId * 20);
                    }
                    handle.WakeIfSleeping();
                    handle.Unpark();
                });
            }

            Assert.True(
                Task.WaitAll(producerTasks, TimeSpan.FromSeconds(3)),
                $"Producers timed out on iteration {i}"
            );
            Assert.True(
                consumerTask.Wait(TimeSpan.FromSeconds(3)),
                $"Consumer hung on iteration {i}"
            );
            Assert.True(woke, $"Consumer timed out waiting on iteration {i}");

            // If a lagging producer latched StateSignaled after consumer completed,
            // consume that pending signal so the next iteration starts fresh.
            int finalState = Volatile.Read(ref *handle.SleepingFlag);
            Assert.True(
                finalState is StateRunning or StateSignaled,
                $"Unexpected state {finalState} on iteration {i}"
            );

            if (finalState == StateSignaled)
            {
                parker.Park(0);
            }

            Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));
        }
    }

    [Fact]
    public void Disarm_RestoresRunningStateSafely()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        Assert.True(parker.TryArm());
        Assert.Equal(StateParked, Volatile.Read(ref *handle.SleepingFlag));

        parker.Disarm();
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));

        // Since it was disarmed, it should arm successfully again
        Assert.True(parker.TryArm());
        parker.Disarm();
        Assert.Equal(StateRunning, Volatile.Read(ref *handle.SleepingFlag));
    }

    [Fact]
    public void Dispose_IsIdempotentAndUnblocksWaiters()
    {
        var parker = new ThreadParker();
        var handle = parker.NativeHandle;
        bool parkCompleted = false;

        using var threadStarted = new ManualResetEventSlim(false);

        var task = Task.Run(() =>
        {
            if (parker.TryArm())
            {
                threadStarted.Set();
                parkCompleted = parker.Park(timeoutMs: 5000);
            }
        });

        Assert.True(threadStarted.Wait(TimeSpan.FromSeconds(2)));

        // Wait until armed/parked
        SpinWait.SpinUntil(
            () => Volatile.Read(ref *handle.SleepingFlag) != StateRunning,
            TimeSpan.FromSeconds(1)
        );

        parker.Unpark();
        parker.Dispose();

        // Multiple dispose calls must be idempotent no-ops
        parker.Dispose();

        Assert.True(task.Wait(TimeSpan.FromSeconds(2)), "Task failed to terminate after Dispose.");
        Assert.True(parkCompleted);
    }
}
