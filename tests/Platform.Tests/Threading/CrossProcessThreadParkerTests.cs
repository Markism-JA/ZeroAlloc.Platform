using System.Diagnostics;
using Platform.Threading;

namespace Platform.Tests.Threading;

public unsafe class CrossProcessThreadParkerTests
{
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
    public void Dispose_CalledMultipleTimes_IsIdempotent()
    {
        string name = GenerateUniqueName();
        var parker = CrossProcessThreadParker.OpenOrCreate(name, isOwner: true);

        // Multiple dispose invocations must not throw or double-free OS handles
        parker.Dispose();
        parker.Dispose();
        parker.Dispose();
    }

    #endregion

    #region Timeout & Pre-Signaling Semantics

    [Fact]
    public void Park_WhenNotSignaled_TimesOutAndReturnsFalse()
    {
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
        string name = GenerateUniqueName();
        using var parker = CrossProcessThreadParker.OpenOrCreate(name, isOwner: true);

        // Signal BEFORE parking
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

    #endregion

    #region Inter-Thread Coordination

    [Fact]
    public void Park_WhenUnparkedFromBackgroundThread_WakesAndReturnsTrue()
    {
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
        string name = GenerateUniqueName();
        using var parker = CrossProcessThreadParker.OpenOrCreate(name, isOwner: true);
        void* rawHandle = parker.Handle;

        Assert.True(rawHandle != null, "Raw handle pointer must not be null.");

        var threadStarted = new ManualResetEventSlim(false);

        var worker = new Thread(() =>
        {
            threadStarted.Set();
            Thread.Sleep(30);

            // Cold path invocation using a raw C-ABI pointer (identical to RemoteMailboxSlot)
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
        string sharedName = GenerateUniqueName();

        // Instance A: Owner (simulates local node mailbox receiver)
        using var receiverParker = CrossProcessThreadParker.OpenOrCreate(sharedName, isOwner: true);

        // Instance B: Client (simulates remote sending peer driver)
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

        // Small delay to ensure receiver entered OS kernel wait
        Thread.Sleep(20);

        // Sender kicks the named event from a separate instance
        senderParker.Unpark();

        receiverThread.Join(TimeSpan.FromSeconds(2));

        Assert.True(receiverResult, "Receiver did not unpark from cross-instance signal.");
    }

    [Fact]
    public void PingPong_MultipleCycles_ExecutesWithoutDeadlockOrStateDrift()
    {
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
