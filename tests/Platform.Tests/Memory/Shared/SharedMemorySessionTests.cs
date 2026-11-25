using System.Runtime.InteropServices;
using Platform.Memory;
using Platform.Memory.Shared;

namespace Platform.Tests.Memory.Shared;

public sealed unsafe class SharedMemorySessionTests
{
    private const uint ValidMagic = 0x454E474E; // 'ENGN'
    private const uint ValidAbi = 1;
    private const ushort NodeId = 10;

    [StructLayout(LayoutKind.Sequential)]
    private struct PacketHeader
    {
        public uint Sequence;
        public uint PacketCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PacketItem
    {
        public int Id;
        public long Timestamp;
    }

    private static SharedMemoryPlan CreateDefaultPlan() =>
        new SharedMemoryPlan()
            .AddRegion(FourCC.Make("CMDS"), byteCount: 4096, alignment: 64)
            .AddRegion(FourCC.Make("DATA"), byteCount: 8192, alignment: 64);

    #region ABI and Handshake Validation

    [Fact]
    public void Attach_ThrowsOnMagicMismatch()
    {
        var plan = CreateDefaultPlan();
        using var map = NativeMemoryMap.CreateAnonymous(plan.CalculateTotalBytes());

        // Host initializes with ValidMagic
        using var host = SharedMemorySession.Create(map, ValidMagic, ValidAbi, NodeId, plan);

        // Client attempts to attach expecting wrong magic
        Assert.Throws<InvalidDataException>(() =>
        {
            SharedMemorySession.Attach(
                map,
                expectedMagic: 0xDEADBEEF,
                expectedAbiVersion: ValidAbi
            );
        });
    }

    [Fact]
    public void Attach_ThrowsOnAbiVersionMismatch()
    {
        var plan = CreateDefaultPlan();
        using var map = NativeMemoryMap.CreateAnonymous(plan.CalculateTotalBytes());

        using var host = SharedMemorySession.Create(map, ValidMagic, abiVersion: 2, NodeId, plan);

        Assert.Throws<InvalidDataException>(() =>
        {
            SharedMemorySession.Attach(map, ValidMagic, expectedAbiVersion: 1);
        });
    }

    [Fact]
    public void Attach_ThrowsImmediately_WhenStateIsFaulted()
    {
        var plan = CreateDefaultPlan();
        using var map = NativeMemoryMap.CreateAnonymous(plan.CalculateTotalBytes());

        using var host = SharedMemorySession.Create(map, ValidMagic, ValidAbi, NodeId, plan);
        host.MarkFaulted();

        // Must fail fast without waiting for the 5-second timeout
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            SharedMemorySession.Attach(map, ValidMagic, ValidAbi, timeout: TimeSpan.FromSeconds(5));
        });

        Assert.Contains("Faulted", ex.Message);
    }

    [Fact]
    public void Attach_ThrowsImmediately_WhenStateIsTornDown()
    {
        var plan = CreateDefaultPlan();
        using var map = NativeMemoryMap.CreateAnonymous(plan.CalculateTotalBytes());

        var sb = (SharedMemorySuperblock*)map.Pointer;
        plan.FormatSuperblock(sb, ValidMagic, ValidAbi, NodeId, map.Length);
        sb->State = (uint)SharedMemoryState.TornDown;

        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            SharedMemorySession.Attach(map, ValidMagic, ValidAbi);
        });

        Assert.Contains("torn down", ex.Message);
    }

    [Fact]
    public void Dispose_OwnerPublishesTornDownState()
    {
        string name = $"test_torndown_{Guid.NewGuid():N}";
        var plan = CreateDefaultPlan();

        using var host = SharedMemorySession.CreateShared(name, ValidMagic, ValidAbi, NodeId, plan);
        using var client = SharedMemorySession.AttachShared(name, ValidMagic, ValidAbi, plan);

        Assert.Equal(SharedMemoryState.Ready, client.State);

        // Host disposes: writes TornDown to superblock, unmaps its view, and unlinks
        host.Dispose();

        // Client's mapping remains resident and observes the TornDown state
        Assert.Equal(SharedMemoryState.TornDown, client.State);
    }

    #endregion

    #region Fast O(1) Region Access & Typed Projections

    [Fact]
    public void GetRegion_ReturnsZeroCostHandle_WithDirectPointerAccess()
    {
        var plan = CreateDefaultPlan();
        using var map = NativeMemoryMap.CreateAnonymous(plan.CalculateTotalBytes());
        using var session = SharedMemorySession.Create(map, ValidMagic, ValidAbi, NodeId, plan);

        uint tag = FourCC.Make("CMDS");
        SharedMemoryRegion region = session.GetRegion(tag);

        Assert.Equal(tag, region.Tag);
        Assert.Equal((nuint)4096, region.Length);
        Assert.True(region.Pointer != null);

        // Write through typed accessor; read back through Span
        region.Write(offsetBytes: 0, 0x11223344);
        Assert.Equal(0x11223344, region.Read<int>(0));
        Assert.Equal(0x44, region.AsSpan()[0]);
    }

    [Fact]
    public void GetRegion_ThrowsKeyNotFound_OnMissingTag()
    {
        var plan = CreateDefaultPlan();
        using var map = NativeMemoryMap.CreateAnonymous(plan.CalculateTotalBytes());
        using var session = SharedMemorySession.Create(map, ValidMagic, ValidAbi, NodeId, plan);

        Assert.Throws<KeyNotFoundException>(() =>
        {
            session.GetRegion(FourCC.Make("NONE"));
        });
    }

    [Fact]
    public void GetHeaderAndTrailingSpan_SlicesCompositeStructureAccurately()
    {
        uint tag = FourCC.Make("CMDS");
        const nuint regionSize = 1024;

        var plan = new SharedMemoryPlan().AddRegion(tag, regionSize);
        using var map = NativeMemoryMap.CreateAnonymous(plan.CalculateTotalBytes());
        using var session = SharedMemorySession.Create(map, ValidMagic, ValidAbi, NodeId, plan);

        SharedMemoryRegion region = session.GetRegion(tag);

        ref PacketHeader header = ref region.GetHeaderAndTrailingSpan<PacketHeader, PacketItem>(
            out Span<PacketItem> items
        );

        header.Sequence = 999;
        header.PacketCount = (uint)items.Length;

        // Verify correct element capacity calculation
        // Header = 8B. Remaining = 1024 - 8 = 1016B. PacketItem = 16B. 1016 / 16 = 63 items.
        int expectedItems = (1024 - sizeof(PacketHeader)) / sizeof(PacketItem);
        Assert.Equal(expectedItems, items.Length);

        items[0] = new PacketItem { Id = 1, Timestamp = 123456 };
        items[^1] = new PacketItem { Id = 63, Timestamp = 654321 };

        // Verify layout coherence when re-slicing
        ref PacketHeader rereadHeader = ref region.GetHeaderAndTrailingSpan<
            PacketHeader,
            PacketItem
        >(out Span<PacketItem> rereadItems);
        Assert.Equal(999u, rereadHeader.Sequence);
        Assert.Equal(1, rereadItems[0].Id);
        Assert.Equal(63, rereadItems[^1].Id);
    }

    [Fact]
    public void GetHeaderAndTrailingSpan_ThrowsWhenRegionSmallerThanHeader()
    {
        uint tag = FourCC.Make("TINY");
        var plan = new SharedMemoryPlan().AddRegion(tag, 16); // Only 16 bytes

        using var map = NativeMemoryMap.CreateAnonymous(plan.CalculateTotalBytes());
        using var session = SharedMemorySession.Create(map, ValidMagic, ValidAbi, NodeId, plan);

        SharedMemoryRegion region = session.GetRegion(tag);

        // PacketHeader (8B) + 16-byte item fits, but a large header must throw
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            region.GetHeaderAndTrailingSpan<LargeHeader, PacketItem>(out _);
        });
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct LargeHeader { }

    #endregion

    #region Heartbeat & Liveness

    [Fact]
    public void Heartbeat_PulsesAndDetectsLivenessCorrectly()
    {
        var plan = CreateDefaultPlan();
        using var map = NativeMemoryMap.CreateAnonymous(plan.CalculateTotalBytes());
        using var session = SharedMemorySession.Create(map, ValidMagic, ValidAbi, NodeId, plan);

        // Before any beat
        Assert.False(session.IsPeerAlive(TimeSpan.FromSeconds(1)));

        // Pulse heartbeat
        session.BeatHeartbeat();

        // Must now be alive
        Assert.True(session.IsPeerAlive(TimeSpan.FromSeconds(2)));
        Assert.True(session.GetHeartbeatAge() < TimeSpan.FromSeconds(1));
    }

    #endregion
}
