using System.Diagnostics;
using System.Runtime.CompilerServices;
using Platform.Threading;

namespace Platform.Tests.Threading;

public class ThreadParkerTests
{
    private const int StateRunning = ThreadParker.StateRunning;
    private const int StateParked = ThreadParker.StateParked;
    private const int StateSignaled = ThreadParker.StateSignaled;

    private static unsafe int ReadFlag(NativeThreadParkerHandle handle) =>
        Volatile.Read(ref *handle.SleepingFlag);

    [Fact]
    public unsafe void NativeHandle_IsBlittableAndExpectedSize()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<NativeThreadParkerHandle>());
        Assert.Equal(sizeof(nint) * 3, sizeof(NativeThreadParkerHandle));
    }

    [Fact]
    public unsafe void InitialState_IsRunning()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        Assert.True(handle.IsAllocated);
        Assert.NotEqual(0, (nint)handle.SleepingFlag);
        Assert.Equal(StateRunning, ReadFlag(handle));
        Assert.False(parker.IsSleeping);
    }

    [Fact]
    public void TryArm_TransitionsRunningToParked()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        Assert.True(parker.TryArm());
        Assert.Equal(StateParked, ReadFlag(handle));
        Assert.True(parker.IsSleeping);

        // Disarm resets back to Running
        parker.Disarm();
        Assert.Equal(StateRunning, ReadFlag(handle));
        Assert.False(parker.IsSleeping);
    }

    [Fact]
    public void TryArm_FailsAndConsumesSignalWhenAlreadySignaled()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        // Producer signals before consumer arms
        handle.Unpark();
        Assert.Equal(StateSignaled, ReadFlag(handle));

        // TryArm fails to abort sleep and consumes StateSignaled -> StateRunning
        Assert.False(parker.TryArm());
        Assert.Equal(StateRunning, ReadFlag(handle));

        // Subsequent TryArm succeeds cleanly
        Assert.True(parker.TryArm());
        parker.Disarm();
        Assert.Equal(StateRunning, ReadFlag(handle));
    }

    [Fact]
    public void Park_ConsumesPreSignalImmediately()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        handle.Unpark();
        Assert.Equal(StateSignaled, ReadFlag(handle));

        bool acquired = parker.Park(timeoutMs: 1000);
        Assert.True(acquired);
        Assert.Equal(StateRunning, ReadFlag(handle));
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
        Assert.Equal(StateRunning, ReadFlag(parker.NativeHandle));
        Assert.True(
            sw.ElapsedMilliseconds >= 30,
            $"Expected timeout ~50ms, elapsed: {sw.ElapsedMilliseconds}ms"
        );
    }

    [Fact]
    public async Task Unpark_WakesParkedThreadImmediately()
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
            () => ReadFlag(handle) == StateParked,
            TimeSpan.FromSeconds(1)
        );

        handle.Unpark();

        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(parkResult);
        Assert.Equal(StateRunning, ReadFlag(handle));
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
        Assert.Equal(StateRunning, ReadFlag(handle));
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
        Assert.Equal(StateRunning, ReadFlag(handle));
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

        // Cycle 1: Pre-signaled; TryArm must fail, consume signal, and drain surplus tokens
        Assert.False(parker.TryArm());
        Assert.Equal(StateRunning, ReadFlag(handle));

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
        Assert.Equal(StateRunning, ReadFlag(handle));
    }

    [Fact]
    public async Task ConcurrentProducers_HammeringWake_WakesThreadSafely()
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
                    // Pre-signaled before TryArm: TryArm consumed the signal and aborted sleep
                    woke = true;
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

            await Task.WhenAll(producerTasks).WaitAsync(TimeSpan.FromSeconds(3));
            await consumerTask.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(woke, $"Consumer timed out waiting on iteration {i}");

            // If a lagging producer latched StateSignaled after consumer completed,
            // consume that pending signal so the next iteration starts fresh.
            int finalState = ReadFlag(handle);
            Assert.True(
                finalState is StateRunning or StateSignaled,
                $"Unexpected state {finalState} on iteration {i}"
            );

            if (finalState == StateSignaled)
            {
                parker.Park(0);
            }

            Assert.Equal(StateRunning, ReadFlag(handle));
        }
    }

    [Fact]
    public void Disarm_RestoresRunningStateSafely()
    {
        using var parker = new ThreadParker();
        var handle = parker.NativeHandle;

        Assert.True(parker.TryArm());
        Assert.Equal(StateParked, ReadFlag(handle));

        parker.Disarm();
        Assert.Equal(StateRunning, ReadFlag(handle));

        // Since it was disarmed, it should arm successfully again
        Assert.True(parker.TryArm());
        parker.Disarm();
        Assert.Equal(StateRunning, ReadFlag(handle));
    }

    [Fact]
    public async Task Dispose_IsIdempotentAndUnblocksWaiters()
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
            () => ReadFlag(handle) != StateRunning,
            TimeSpan.FromSeconds(1)
        );

        parker.Unpark();
        parker.Dispose();

        // Multiple dispose calls must be idempotent no-ops
        parker.Dispose();

        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(parkCompleted);
    }
}
