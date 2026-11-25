using Platform.Diagnostics;

namespace Platform.Tests.Diagnostics;

public class NativeDiagnosticsTests
{
    [Fact]
    public void Write_EmptySpan_DoesNotThrowOrAllocate()
    {
        // Warm up JIT
        NativeDiagnostics.Write(ReadOnlySpan<byte>.Empty);

        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeDiagnostics.Write(ReadOnlySpan<byte>.Empty);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
    }

    [Fact]
    public void Write_Utf8Literal_ExecutesWithZeroAllocations()
    {
        ReadOnlySpan<byte> msg = "[ZeroAlloc.Platform.Tests] NativeDiagnostics.Write probe\n"u8;

        // Warm up JIT
        NativeDiagnostics.Write(msg);

        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeDiagnostics.Write(msg);
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(before, after);
    }
}
