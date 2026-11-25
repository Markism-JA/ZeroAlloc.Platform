using Platform.Memory;
using Platform.Memory.Shared;

namespace Platform.Tests.Memory.Shared;

public sealed unsafe class SharedMemoryPlanTests
{
    private enum TestTags : uint
    {
        RegionA = 0x41414141, // 'AAAA'
        RegionB = 0x42424242, // 'BBBB'
        RegionC = 0x43434343, // 'CCCC'
    }

    private enum ByteTag : byte
    {
        First = 1,
        Second = 2,
    }

    private enum UShortTag : ushort
    {
        Alpha = 0x1001,
        Beta = 0x1002,
    }

    [Fact]
    public void CalculateTotalBytes_AlignsSuperblockToSystemPageAndPartitionsTo64Bytes()
    {
        var plan = new SharedMemoryPlan()
            .AddRegion(TestTags.RegionA, byteCount: 100, alignment: 64)
            .AddRegion(TestTags.RegionB, byteCount: 50, alignment: 64);

        nuint total = plan.CalculateTotalBytes();

        // Page 0 = max(4096, SystemPageSize) -> 4096 on 4KB x64, 16384 on 16KB Apple Silicon
        // Region A = Page0 + 100 -> aligned to 64 = Page0 + 128
        // Region B = Page0 + 128 + 50 -> aligned to 64 = Page0 + 192
        nuint page0 = Math.Max(
            (nuint)SharedMemorySuperblock.SuperblockSize,
            (nuint)Environment.SystemPageSize
        );
        Assert.Equal(page0 + 192, total);
        Assert.Equal((nuint)0, total % 64);
    }

    [Fact]
    public void AddRegion_SupportsByteUShortAndUIntEnumTags()
    {
        var plan = new SharedMemoryPlan()
            .AddRegion(ByteTag.First, 64)
            .AddRegion(UShortTag.Alpha, 128)
            .AddRegion(TestTags.RegionC, 256);

        nuint totalBytes = plan.CalculateTotalBytes();
        using var map = NativeMemoryMap.CreateAnonymous(totalBytes);
        var sb = (SharedMemorySuperblock*)map.Pointer;
        plan.FormatSuperblock(sb, FourCC.Make("TEST"), 1, 1, totalBytes);

        Assert.Equal(3, sb->RegionCount);
        SharedMemoryRegionEntry* entries = sb->GetRegionEntries();
        Assert.Equal((uint)ByteTag.First, entries[0].Tag);
        Assert.Equal((uint)UShortTag.Alpha, entries[1].Tag);
        Assert.Equal((uint)TestTags.RegionC, entries[2].Tag);
    }

    [Fact]
    public void AddRegion_ThrowsOnZeroByteCount()
    {
        var plan = new SharedMemoryPlan();
        Assert.Throws<ArgumentOutOfRangeException>(() => plan.AddRegion(TestTags.RegionA, 0));
    }

    [Fact]
    public void AddRegion_ThrowsWhenExceedingMaxRegions()
    {
        var plan = new SharedMemoryPlan();

        for (uint i = 0; i < SharedMemorySuperblock.MaxRegions; i++)
        {
            plan.AddRegion(i + 1, 128);
        }

        Assert.Throws<InvalidOperationException>(() =>
        {
            plan.AddRegion(0xFFFF, 128);
        });
    }

    [Fact]
    public void AddRegion_ThrowsOnDuplicateTag()
    {
        var plan = new SharedMemoryPlan().AddRegion(TestTags.RegionA, 128);

        Assert.Throws<ArgumentException>(() =>
        {
            plan.AddRegion(TestTags.RegionA, 256);
        });
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(3u)]
    [InlineData(7u)]
    [InlineData(12u)] // Non-power-of-two
    public void AddRegion_ThrowsOnInvalidAlignment(nuint alignment)
    {
        var plan = new SharedMemoryPlan();

        Assert.Throws<ArgumentException>(() =>
        {
            plan.AddRegion(TestTags.RegionA, 128, alignment);
        });
    }

    [Fact]
    public void FormatSuperblock_ThrowsWhenBackingLengthTooSmall()
    {
        var plan = new SharedMemoryPlan().AddRegion(TestTags.RegionA, 1024);

        nuint requiredBytes = plan.CalculateTotalBytes();

        using var map = NativeMemoryMap.CreateAnonymous(requiredBytes - 64);
        var sb = (SharedMemorySuperblock*)map.Pointer;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            plan.FormatSuperblock(sb, 0x1234, 1, 0, map.Length);
        });
    }

    [Fact]
    public void FormatSuperblock_GuaranteesStrictNonOverlappingPartitions()
    {
        var plan = new SharedMemoryPlan()
            .AddRegion(TestTags.RegionA, 137, alignment: 64)
            .AddRegion(TestTags.RegionB, 219, alignment: 128)
            .AddRegion(TestTags.RegionC, 512, alignment: 64);

        nuint totalBytes = plan.CalculateTotalBytes();
        using var map = NativeMemoryMap.CreateAnonymous(totalBytes);

        var sb = (SharedMemorySuperblock*)map.Pointer;
        plan.FormatSuperblock(sb, 0x454E474E, 1, 42, totalBytes);

        Assert.Equal(3, sb->RegionCount);
        Assert.Equal((uint)SharedMemoryState.Initializing, sb->State);

        SharedMemoryRegionEntry* entries = sb->GetRegionEntries();
        nuint cursor = Math.Max(
            (nuint)SharedMemorySuperblock.SuperblockSize,
            (nuint)Environment.SystemPageSize
        );

        for (int i = 0; i < 3; i++)
        {
            Assert.True(entries[i].Offset >= cursor);
            Assert.Equal((nuint)0, entries[i].Offset % entries[i].Alignment);

            cursor = entries[i].Offset + entries[i].Length;
        }

        Assert.True(cursor <= totalBytes);
    }
}
