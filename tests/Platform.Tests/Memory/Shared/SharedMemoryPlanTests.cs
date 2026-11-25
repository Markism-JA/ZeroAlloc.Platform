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

    [Fact]
    public void CalculateTotalBytes_AlignsSuperblockAndPartitionsTo64Bytes()
    {
        var plan = new SharedMemoryPlan()
            .AddRegion(TestTags.RegionA, byteCount: 100, alignment: 64)
            .AddRegion(TestTags.RegionB, byteCount: 50, alignment: 64);

        nuint total = plan.CalculateTotalBytes();

        // Superblock = 4096B (Page 0, 4KB page aligned)
        // Region A   = Offset 4096, Len 100 -> End 4196 -> Aligned to 64: 4224
        // Region B   = Offset 4224, Len 50  -> End 4274 -> Aligned to 64: 4288
        Assert.Equal((nuint)4288, total);
        Assert.Equal((nuint)0, total % 64);
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
        nuint cursor = SharedMemorySuperblock.SuperblockSize;

        for (int i = 0; i < 3; i++)
        {
            Assert.True(entries[i].Offset >= cursor);
            Assert.Equal((nuint)0, entries[i].Offset % entries[i].Alignment);

            cursor = entries[i].Offset + entries[i].Length;
        }

        Assert.True(cursor <= totalBytes);
    }
}
