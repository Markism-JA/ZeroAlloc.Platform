using System.Runtime.InteropServices;
using Platform.Memory;
using Platform.Memory.Shared;

namespace Platform.Tests.Memory.Shared;

public class SharedMemoryRegionTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct TestHeader
    {
        public uint Magic;
        public uint Count;
        public long Sequence;
    }

    private enum RegionTag : uint
    {
        Payload = 0x5041594C,
    }

    [Fact]
    public void Region_TypedReadWriteAsRefAndHeaderTrailingSpan_WorkCorrectly()
    {
        string shmName = $"test_region_{Guid.NewGuid():N}";
        uint magic = FourCC.Make("REGN");
        var plan = new SharedMemoryPlan().AddRegion(RegionTag.Payload, 256, alignment: 64);

        using var session = SharedMemorySession.CreateShared(shmName, magic, 1, 7, plan);

        SharedMemoryRegion region = session.GetRegion(RegionTag.Payload);
        Assert.Equal((nuint)256, region.Length);
        Assert.Equal((uint)RegionTag.Payload, region.Tag);

        // Read/Write primitives
        region.Write(0, 0x1122334455667788UL);
        Assert.Equal(0x1122334455667788UL, region.Read<ulong>(0));

        // AsRef & typed AsSpan
        ref TestHeader hdr = ref region.GetHeaderAndTrailingSpan<TestHeader, int>(out Span<int> items);
        hdr.Magic = magic;
        hdr.Count = 4;
        hdr.Sequence = 99;

        Assert.Equal((256 - 16) / sizeof(int), items.Length);
        items[0] = 10;
        items[1] = 20;
        items[2] = 30;
        items[3] = 40;

        Assert.Equal(magic, region.AsRef<TestHeader>(0).Magic);
        Assert.Equal(40, region.AsSpan<int>(16, 4)[3]);
    }

    [Fact]
    public void Region_OutOfBoundsAndNegativeOffsets_ThrowArgumentOutOfRangeException()
    {
        string shmName = $"test_region_oob_{Guid.NewGuid():N}";
        var plan = new SharedMemoryPlan().AddRegion(RegionTag.Payload, 64, alignment: 64);

        using var session = SharedMemorySession.CreateShared(shmName, FourCC.Make("BOUN"), 1, 1, plan);
        SharedMemoryRegion region = session.GetRegion(RegionTag.Payload);

        Assert.Throws<ArgumentOutOfRangeException>(() => region.Read<long>(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => region.Read<long>(60));
        Assert.Throws<ArgumentOutOfRangeException>(() => region.Write(60, 42L));
        Assert.Throws<ArgumentOutOfRangeException>(() => region.AsSpan<long>(0, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => region.AsSpan<long>(-8, 1));
    }

    [Fact]
    public void NativeMemoryMapExtensions_CopyFromClearReadWriteAsRef_WorkCorrectly()
    {
        using var map = NativeMemoryMap.CreateAnonymous(256);

        byte[] payload = [1, 2, 3, 4, 5, 6, 7, 8];
        map.CopyFrom(16, payload);
        Assert.Equal(1, map.Read<byte>(16));
        Assert.Equal(8, map.Read<byte>(23));

        map.Write(64, 0xDEADBEEFCAFEBABEUL);
        Assert.Equal(0xDEADBEEFCAFEBABEUL, map.AsRef<ulong>(64));

        map.Clear(64, 8);
        Assert.Equal(0UL, map.Read<ulong>(64));

        Assert.Throws<ArgumentOutOfRangeException>(() => map.Read<ulong>(252));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.CopyFrom(252, payload));
        Assert.Throws<ArgumentOutOfRangeException>(() => map.Clear(250, 10));
    }
}
