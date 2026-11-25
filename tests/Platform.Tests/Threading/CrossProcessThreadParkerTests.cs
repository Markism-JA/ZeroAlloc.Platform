using System.Diagnostics;
using System.Runtime.InteropServices;
using Platform.Threading;

namespace Platform.Tests.Threading;

public unsafe class CrossProcessThreadParkerTests
{
    private static bool IsSupportedOs =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static string GenerateUniqueName() => $"test_parker_{Guid.NewGuid():N}";

    #region Argument Validation & Lifecycle

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OpenOrCreate_InvalidName_ThrowsArgumentException(string? invalidName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            CrossProcessThreadParker.OpenOrCreate(invalidName!)
        );
    }

    [Fact]
    public void OpenOrCreate_UnsupportedOs_ThrowsPlatformNotSupportedException()
    {
        if (IsSupportedOs)
            return;

        Assert.Throws<PlatformNotSupportedException>(() =>
            CrossProcessThreadParker.OpenOrCreate(GenerateUniqueName(), isOwner: true)
        );
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_IsIdempotent()
    {
        if (!IsSupportedOs)
            return;

        string name = GenerateUniqueName();
        var parker = CrossProcessThreadParker.OpenOrCreate(name, isOwner: true);

        parker.Dispose();
        parker.Dispose();
        parker.Dispose();
    }

    #endregion

    #region Timeout & Pre-Signaling Semantics

    [Fact]
    public void Park_WhenNotSignaled_TimesOutAndReturnsFalse()
    {
        if (!IsSupportedOs)
            return;

        string name = GenerateUniqueName();
        using var parker = CrossProcessThreadParker.OpenOrCreate(name, isOwner: true);

        var sw = Stopwatch.StartNew();
        const int timeoutMs = 50;

        bool result = parker.Park(timeoutMs);
        sw.Stop();

        Assert.False(result);
        Assert.True(
            sw.ElapsedMilliseconds >= timeoutMs - 5,
            $"Expected timeout >= {timeoutMs - 5}ms, but elapsed was {sw.ElapsedMilliseconds}ms."
        );
    }

    [Fact]
    public void Park_WhenPreSignaled_ReturnsTrueImmediately()
    {
        if (!IsSupportedOs)
            return;

        string name = GenerateUniqueName();
        using var parker = CrossProcessThreadParker.OpenOrCreate(name, isOwner: true);

        parker.Unpark();

        var sw = Stopwatch.StartNew();
        bool result = parker.Park(1000);
        sw.Stop();

        Assert.True(result);
        Assert.True(
            sw.ElapsedMilliseconds < 50,
            $"Expected immediate unpark (<50ms), but took {sw.ElapsedMilliseconds}ms."
        );
    }

    [Fact]
    public void Park_InfiniteTimeout_WakesWhenUnparked()
    {
        if (!IsSupportedOs)
            return;

        string name = GenerateUniqueName();
        using var parker = CrossProcessThreadParker.OpenOrCreate(name, isOwner: true);
        using var started = new ManualResetEventSlim(false);

        var worker = new Thread(() =>
        {
            started.Set();
            Thread.Sleep(25);
            parker.Unpark();
        })
        {
            IsBackground = true,
        };

        worker.Start();
        started.Wait();

        bool woke = parker.Park(-1);
        worker.Join(TimeSpan.FromSeconds(2));

        Assert.True(woke);
    }

    #endregion

    #region Inter-Thread Coordination

    [Fact]
    public void Park_WhenUnparkedFromBackgroundThread_WakesAndReturnsTrue()
    {
        if (!IsSupportedOs)
            return;

        string name = GenerateUniqueName();
        using var parker = CrossProcessThreadParker.OpenOrCreate(name, isOwner: true);

        var threadStarted = new ManualResetEventSlim(false);

        var worker = new Thread(() =>
        {
            threadStarted.Set();
            Thread.Sleep(30);
            parker.Unpark();
        })
        {
            IsBackground = true,
        };

        worker.Start();
        threadStarted.Wait();

        var sw = Stopwatch.StartNew();
        bool unparked = parker.Park(2000);
        sw.Stop();

        worker.Join();

        Assert.True(unparked);
        Assert.True(
            sw.ElapsedMilliseconds < 500,
            $"Expected prompt wakeup (<500ms), but took {sw.ElapsedMilliseconds}ms."
        );
    }

    [Fact]
    public void Unpark_StaticRawHandle_WakesParkedThread()
    {
        if (!IsSupportedOs)
            return;

        string name = GenerateUniqueName();
        using var parker = CrossProcessThreadParker.OpenOrCreate(name, isOwner: true);
        void* rawHandle = parker.Handle;

        Assert.True(rawHandle != null, "Raw handle pointer must not be null.");

        var threadStarted = new ManualResetEventSlim(false);

        var worker = new Thread(() =>
        {
            threadStarted.Set();
            Thread.Sleep(30);
            CrossProcessThreadParker.Unpark(rawHandle);
        })
        {
            IsBackground = true,
        };

        worker.Start();
        threadStarted.Wait();

        bool unparked = parker.Park(2000);
        worker.Join();

        Assert.True(unparked);
    }

    #endregion

    #region Multi-Instance Cross-Process Simulation

    [Fact]
    public void CrossInstance_SameNamedSignal_UnparksPeerInstance()
    {
        if (!IsSupportedOs)
            return;

        string sharedName = GenerateUniqueName();

        using var receiverParker = CrossProcessThreadParker.OpenOrCreate(sharedName, isOwner: true);
        using var senderParker = CrossProcessThreadParker.OpenOrCreate(sharedName, isOwner: false);

        var readyToPark = new ManualResetEventSlim(false);
        bool receiverResult = false;

        var receiverThread = new Thread(() =>
        {
            readyToPark.Set();
            receiverResult = receiverParker.Park(2000);
        })
        {
            IsBackground = true,
        };

        receiverThread.Start();
        readyToPark.Wait();
        Thread.Sleep(20);

        senderParker.Unpark();
        receiverThread.Join(TimeSpan.FromSeconds(2));

        Assert.True(receiverResult, "Receiver did not unpark from cross-instance signal.");
    }

    [Fact]
    public void PingPong_MultipleCycles_ExecutesWithoutDeadlockOrStateDrift()
    {
        if (!IsSupportedOs)
            return;

        string nodeToPeerName = GenerateUniqueName();
        string peerToNodeName = GenerateUniqueName();

        using var nodeTx = CrossProcessThreadParker.OpenOrCreate(nodeToPeerName, isOwner: true);
        using var nodeRx = CrossProcessThreadParker.OpenOrCreate(peerToNodeName, isOwner: true);

        using var peerRx = CrossProcessThreadParker.OpenOrCreate(nodeToPeerName, isOwner: false);
        using var peerTx = CrossProcessThreadParker.OpenOrCreate(peerToNodeName, isOwner: false);

        const int iterations = 100;
        int peerCycles = 0;

        var peerThread = new Thread(() =>
        {
            for (int i = 0; i < iterations; i++)
            {
                if (!peerRx.Park(2000))
                    return;

                peerCycles++;
                peerTx.Unpark();
            }
        })
        {
            IsBackground = true,
        };

        peerThread.Start();

        for (int i = 0; i < iterations; i++)
        {
            nodeTx.Unpark();
            bool received = nodeRx.Park(2000);
            Assert.True(received, $"Node timed out waiting for peer ping-pong at iteration {i}.");
        }

        peerThread.Join(TimeSpan.FromSeconds(2));

        Assert.Equal(iterations, peerCycles);
    }

    #endregion
}
