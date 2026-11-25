using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.CorePinning;
using Platform.Threading;

namespace Platform.Tests.CorePinning;

public class CorePinningTests
{
    private static bool IsAffinitySupportedOs =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        || RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    [Fact]
    public unsafe void AffinityStructs_AreBlittableAndCompact()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<AffinityConfig>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<EngineAffinityProfile>());
        Assert.Equal(5, sizeof(AffinityConfig));
        Assert.Equal(5, sizeof(EngineAffinityProfile));

        var none = AffinityConfig.None;
        Assert.Equal(AffinityMode.Disabled, none.Mode);
        Assert.Equal(0, none.TargetCore);

        var dev = EngineAffinityProfile.MonolithDev;
        Assert.Equal(AffinityMode.Disabled, dev.Mode);
        Assert.Equal(0, dev.BaseCoreOffset);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(64)]
    [InlineData(128)]
    public void PinCurrentThreadToCore_OutOfRange_ThrowsArgumentOutOfRangeException(int invalidCore)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ThreadAffinityUtility.PinCurrentThreadToCore(invalidCore)
        );
    }

    [Fact]
    public void SetCurrentThreadAffinityMask_ZeroMask_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ThreadAffinityUtility.SetCurrentThreadAffinityMask(0)
        );
    }

    [Fact]
    public void PinCurrentThreadToCore_Core0_SucceedsOnLinuxAndWindows_ThrowsOnMacOS()
    {
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
        })
        {
            IsBackground = true,
        };

        thread.Start();
        thread.Join(TimeSpan.FromSeconds(2));

        if (IsAffinitySupportedOs)
        {
            Assert.Null(captured);
        }
        else
        {
            Assert.IsType<PlatformNotSupportedException>(captured);
        }
    }

    [Fact]
    public void SetCurrentThreadAffinityMask_MultiCoreMask_SucceedsOnLinuxAndWindows_ThrowsOnMacOS()
    {
        int availableCores = Math.Min(Environment.ProcessorCount, 4);
        ulong mask = (1UL << availableCores) - 1UL;

        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                ThreadAffinityUtility.SetCurrentThreadAffinityMask(mask);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();
        thread.Join(TimeSpan.FromSeconds(2));

        if (IsAffinitySupportedOs)
        {
            Assert.Null(captured);
        }
        else
        {
            Assert.IsType<PlatformNotSupportedException>(captured);
        }
    }
}
