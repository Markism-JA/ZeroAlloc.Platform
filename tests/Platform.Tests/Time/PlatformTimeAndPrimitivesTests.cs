using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Threading;
using Platform.Time;
using Platform.Timer;

namespace Platform.Tests.Time;

public class PlatformTimeAndPrimitivesTests
{
    [Fact]
    public unsafe void PlatformClockBlock_HasExpectedCacheLineLayout()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PlatformClockBlock>());
        Assert.Equal(64, sizeof(PlatformClockBlock));
        Assert.Equal((nint)0, Marshal.OffsetOf<PlatformClockBlock>(nameof(PlatformClockBlock.UtcEpochNs)));
        Assert.Equal((nint)8, Marshal.OffsetOf<PlatformClockBlock>(nameof(PlatformClockBlock.UtcEpochMs)));
        Assert.Equal((nint)16, Marshal.OffsetOf<PlatformClockBlock>(nameof(PlatformClockBlock.MonotonicTicks)));
        Assert.Equal((nint)24, Marshal.OffsetOf<PlatformClockBlock>(nameof(PlatformClockBlock.IsManual)));
    }

    [Fact]
    public void NativeTsc_NowNs_IsUtcAnchoredAndMonotonic()
    {
        long prev = NativeTsc.NowNs();
        for (int i = 0; i < 10_000; i++)
        {
            long curr = NativeTsc.NowNs();
            Assert.True(curr >= prev, $"Monotonicity violated at iter {i}: prev={prev}, curr={curr}");
            prev = curr;
        }

        long expectedUtcNs = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) * 100L;
        long driftMs = Math.Abs(prev - expectedUtcNs) / 1_000_000L;
        Assert.True(driftMs < 5_000, $"Expected UTC-anchored timestamp within 5s, drift={driftMs}ms");

        long ms = NativeTsc.NowMs();
        Assert.True(Math.Abs(ms - (prev / 1_000_000L)) < 1_000);
    }

    [Fact]
    public async Task NativeTsc_ConcurrentReaders_AreMonotonicWithoutTornReads()
    {
        const int threadCount = 4;
        const int readsPerThread = 200_000;
        long[] regressions = new long[threadCount];

        var tasks = new Task[threadCount];
        for (int t = 0; t < threadCount; t++)
        {
            int idx = t;
            tasks[t] = Task.Run(() =>
            {
                long prev = NativeTsc.NowNs();
                for (int i = 0; i < readsPerThread; i++)
                {
                    long curr = NativeTsc.NowNs();
                    if (curr < prev)
                        regressions[idx]++;
                    prev = curr;
                }
            });
        }

        await Task.WhenAll(tasks);
        Assert.All(regressions, r => Assert.Equal(0L, r));
    }

    [Fact]
    public unsafe void PlatformClock_ManualMode_OverridesLiveClockAndResetsToLive()
    {
        PlatformClockBlock block = default;
        const long anchorNs = 1_700_000_000_000_000_000L;

        PlatformClock.SetManualTime(&block, anchorNs);
        Assert.Equal(anchorNs, PlatformClock.NowNs(&block));
        Assert.Equal(anchorNs / 1_000_000L, PlatformClock.NowMs(&block));
        Assert.Equal(1, block.MonotonicTicks);

        PlatformClock.AdvanceTime(&block, TimeSpan.FromMilliseconds(250));
        Assert.Equal(anchorNs + 250_000_000L, PlatformClock.NowNs(&block));
        Assert.Equal((anchorNs / 1_000_000L) + 250L, PlatformClock.NowMs(&block));
        Assert.Equal(2, block.MonotonicTicks);

        PlatformClock.ResetToLive(&block);
        Assert.Equal(0, block.IsManual);
        long liveNs = PlatformClock.NowNs(&block);
        Assert.NotEqual(anchorNs + 250_000_000L, liveNs);
    }

    [Fact]
    public unsafe void PlatformClock_NullAndNearNullPointers_FallBackSafelyToLiveClock()
    {
        long directNs = PlatformClock.NowNs();
        long nullNs = PlatformClock.NowNs(null);
        long nearNullNs = PlatformClock.NowNs((PlatformClockBlock*)128);

        Assert.True(Math.Abs(directNs - nullNs) < 1_000_000_000L);
        Assert.True(Math.Abs(directNs - nearNullNs) < 1_000_000_000L);

        long directMs = PlatformClock.NowMs();
        long nullMs = PlatformClock.NowMs(null);
        long nearNullMs = PlatformClock.NowMs((PlatformClockBlock*)128);

        Assert.True(Math.Abs(directMs - nullMs) < 1_000L);
        Assert.True(Math.Abs(directMs - nearNullMs) < 1_000L);
    }

    [Fact]
    public void HighResolutionTimerScope_IsIdempotentAndSupportsNesting()
    {
        using var outer = new HighResolutionTimerScope(1);
        using (var inner = new HighResolutionTimerScope(1))
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.True(inner.IsActive);
            }
            else
            {
                Assert.False(inner.IsActive);
            }
        }

        outer.Dispose();
        Assert.False(outer.IsActive);

        // Second Dispose must be a safe idempotent no-op
        outer.Dispose();
        Assert.False(outer.IsActive);
    }

    [Fact]
    public void ThreadAffinityUtility_ValidatesArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ThreadAffinityUtility.PinCurrentThreadToCore(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThreadAffinityUtility.PinCurrentThreadToCore(64));
        Assert.Throws<ArgumentOutOfRangeException>(() => ThreadAffinityUtility.SetCurrentThreadAffinityMask(0));
    }

    [Fact]
    public void ThreadAffinityUtility_PinsWorkerThreadToCore0_OnLinuxAndWindows()
    {
        if (
            !RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        )
        {
            return;
        }

        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                ThreadAffinityUtility.PinCurrentThreadToCore(0);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.Start();
        thread.Join();
        Assert.Null(captured);
    }
}
