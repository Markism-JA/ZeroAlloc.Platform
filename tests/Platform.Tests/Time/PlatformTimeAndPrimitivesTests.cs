using System.Runtime.InteropServices;
using Platform.Threading;
using Platform.Time;
using Platform.Timer;

namespace Platform.Tests.Time;

public unsafe class PlatformTimeAndPrimitivesTests
{
    [Fact]
    public void NativeTsc_NowNs_IsUtcAnchoredAndMonotonic()
    {
        long t1 = NativeTsc.NowNs();
        Thread.SpinWait(100);
        long t2 = NativeTsc.NowNs();

        Assert.True(t2 >= t1, $"Expected monotonic timestamps, got t1={t1}, t2={t2}");

        long expectedUtcNs = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) * 100L;
        long driftMs = Math.Abs(t2 - expectedUtcNs) / 1_000_000L;
        Assert.True(driftMs < 5_000, $"Expected UTC-anchored timestamp within 5s, drift={driftMs}ms");
    }

    [Fact]
    public void PlatformClock_ManualMode_OverridesLiveClockAndAdvances()
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
    }

    [Fact]
    public void PlatformClock_NullPointer_FallsBackToLiveClock()
    {
        long liveNs = PlatformClock.NowNs(null);
        long directNs = PlatformClock.NowNs();
        Assert.True(Math.Abs(directNs - liveNs) < 1_000_000_000L);
    }

    [Fact]
    public void HighResolutionTimerScope_ConstructsAndDisposesSafely()
    {
        using var scope = new HighResolutionTimerScope(1);
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
