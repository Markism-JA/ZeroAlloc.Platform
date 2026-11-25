using System.Runtime.InteropServices;
using System.Text;
using Platform.Memory;
using Platform.Memory.Scanner;
using Platform.Memory.Shared;
using Platform.Native.Posix;
using Platform.Native.Posix.Enums;

namespace Platform.Tests.Memory.Scanner;

public sealed unsafe class SharedMemoryScannerTests : IDisposable
{
    private const string Prefix = "test_scan_repro_";
    private readonly List<string> _allocatedShmNames = new();

    [Fact]
    public void ScanLinux_WhenSegmentIsZeroBytes_SkipsWithoutSigbusOrAccessViolation()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        string zeroByteShmName = $"{Prefix}zero_{Guid.NewGuid():N}";
        _allocatedShmNames.Add(zeroByteShmName);

        // NativeMemoryMap prevents 0-byte files by design (ThrowIfZero + immediate ftruncate).
        // Bypassing it here is strictly required to simulate an in-creation / un-truncated kernel inode.
        int fd = CreateRawZeroByteShm(zeroByteShmName);
        Assert.True(fd >= 0, $"Failed to create raw test shm: {Marshal.GetLastPInvokeError()}");
        Libc.Close(fd);

        Span<DiscoveredSegment> discovered = stackalloc DiscoveredSegment[4];

        // fstat detects st_size <= 0 and exits before mmap to prevent SIGBUS
        int count = SharedMemoryScanner.Scan(Prefix, discovered);

        Assert.Equal(0, count);
    }

    [Fact]
    public void ScanLinux_WhenSegmentIsSmallerThanSuperblock_FailsClosedSafely()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        string truncatedShmName = $"{Prefix}trunc_{Guid.NewGuid():N}";
        _allocatedShmNames.Add(truncatedShmName);

        // Create a 64-byte segment (less than SharedMemorySuperblock.SuperblockSize = 256 bytes)
        using var truncMap = NativeMemoryMap.CreateNewShared(
            truncatedShmName,
            byteCount: 64,
            options: MemoryMapOptions.None
        );

        Span<DiscoveredSegment> discovered = stackalloc DiscoveredSegment[4];
        int count = SharedMemoryScanner.Scan(Prefix, discovered);

        Assert.Equal(0, count);
    }

    [Fact]
    public void ScanLinux_MixedValidAndZeroByteSegments_DiscoversValidNodeWithoutCrashing()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        string zeroByteShmName = $"{Prefix}dead_zero";
        string validShmName = $"{Prefix}alive_node";
        _allocatedShmNames.Add(zeroByteShmName);
        _allocatedShmNames.Add(validShmName);

        // 1. Raw 0-byte corrupt/in-creation segment
        int zeroFd = CreateRawZeroByteShm(zeroByteShmName);
        Assert.True(zeroFd >= 0);
        Libc.Close(zeroFd);

        // 2. Format a valid segment using NativeMemoryMap
        const nuint totalSize = 4096;
        using var validMap = NativeMemoryMap.CreateNewShared(validShmName, totalSize);

        var sb = (SharedMemorySuperblock*)validMap.Pointer;
        sb->Magic = 0x4543484F; // FourCC: 'ECHO'
        sb->AbiVersion = 1;
        sb->NodeId = 777;
        sb->State = (uint)SharedMemoryState.Ready;
        sb->TotalByteLength = totalSize;

        // 3. Scanner skips the 0-byte file and discovers the healthy segment
        Span<DiscoveredSegment> discovered = stackalloc DiscoveredSegment[4];
        int count = SharedMemoryScanner.Scan(
            Prefix,
            discovered,
            expectedMagic: 0x4543484F,
            expectedAbiVersion: 1
        );

        Assert.Equal(1, count);
        Assert.Equal(777, discovered[0].Superblock.NodeId);
        Assert.Equal((uint)SharedMemoryState.Ready, discovered[0].Superblock.State);
    }

    private static int CreateRawZeroByteShm(string name)
    {
        string path = name.StartsWith('/') ? name : "/" + name;
        Span<byte> utf8 = stackalloc byte[128];
        int written = Encoding.UTF8.GetBytes(path, utf8);
        utf8[written] = 0;

        fixed (byte* pName = utf8)
        {
            return Libc.Shm_open(
                pName,
                PosixOpenFlags.Create | PosixOpenFlags.ReadWrite,
                0b110_110_000
            );
        }
    }

    public void Dispose()
    {
        // Use the native platform API directly
        foreach (string name in _allocatedShmNames)
        {
            NativeMemoryMap.UnlinkShared(name);
        }
    }
}
