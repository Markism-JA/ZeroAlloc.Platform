using System.Runtime.InteropServices;
using Platform.Memory;
using Platform.Native.Posix;
using Platform.Native.Posix.Enums;
using Platform.Native.Posix.Structs;
using Platform.Native.Windows.Structs;

namespace Platform.Tests.Memory;

public sealed class NativeMemoryMapTests
{
    [Fact]
    public unsafe void AnonymousMap_CanReadAndWriteMemory()
    {
        const nuint size = 65536;
        using var map = NativeMemoryMap.CreateAnonymous(size);

        Assert.True(map.Pointer != null);
        Assert.Equal(size, map.Length);

        var span = map.AsSpan();
        span.Fill(0xAA);

        Assert.Equal(0xAA, span[0]);
        Assert.Equal(0xAA, span[^1]);
    }

    [Fact]
    public void SharedMap_CrossViewReadWrite_DataIsCoherent()
    {
        string name = $"test_shm_{Guid.NewGuid():N}";
        const nuint size = 4096;

        using var owner = NativeMemoryMap.CreateNewShared(name, size);
        using var reader = NativeMemoryMap.OpenExistingShared(name, size);

        owner.AsSpan()[42] = 0x7F;

        Assert.Equal(0x7F, reader.AsSpan()[42]);
    }

    [Fact]
    public void CreateNewShared_ThrowsWhenAlreadyExists()
    {
        string name = $"test_shm_dup_{Guid.NewGuid():N}";
        const nuint size = 4096;

        using var map1 = NativeMemoryMap.CreateNewShared(name, size);

        Assert.Throws<IOException>(() =>
        {
            using var map2 = NativeMemoryMap.CreateNewShared(name, size);
        });
    }

    [Fact]
    public void OpenExistingShared_ThrowsNotFound_WhenDoesNotExist()
    {
        string name = $"non_existent_{Guid.NewGuid():N}";

        Assert.Throws<FileNotFoundException>(() =>
        {
            using var map = NativeMemoryMap.OpenExistingShared(name, 4096);
        });
    }

    [Fact]
    public void TryOpenExistingShared_ReturnsFalse_WhenDoesNotExist()
    {
        string name = $"non_existent_{Guid.NewGuid():N}";

        bool opened = NativeMemoryMap.TryOpenExistingShared(
            name,
            4096,
            MemoryMapProtection.ReadWrite,
            MemoryMapOptions.None,
            out var map
        );

        Assert.False(opened);
        Assert.Null(map);
    }

    [Fact]
    public void NonOwner_DoesNotUnlinkSharedMemoryOnDispose()
    {
        string name = $"test_unlink_{Guid.NewGuid():N}";
        const nuint size = 4096;

        using (var owner = NativeMemoryMap.CreateNewShared(name, size))
        {
            using (
                var reader = NativeMemoryMap.OpenExistingShared(
                    name,
                    size,
                    MemoryMapProtection.ReadWrite
                )
            )
            {
                reader.AsSpan()[0] = 0x01;
            } // Reader disposes here (must NOT unlink)

            // Owner should still have valid access
            Assert.Equal(0x01, owner.AsSpan()[0]);

            // A new reader should still be able to open it
            using var secondReader = NativeMemoryMap.OpenExistingShared(
                name,
                size,
                MemoryMapProtection.ReadWrite
            );
            Assert.Equal(0x01, secondReader.AsSpan()[0]);
        } // Owner disposes here (unlinks on Linux, releases handle on Windows)

        // Now it must be gone
        Assert.False(
            NativeMemoryMap.TryOpenExistingShared(
                name,
                size,
                MemoryMapProtection.Read,
                MemoryMapOptions.None,
                out _
            )
        );
    }

    [Fact]
    public async Task OpenOrCreateShared_SurvivesSevereTOCTOURace()
    {
        string name = $"test_toctou_{Guid.NewGuid():N}";
        const nuint size = 4096;
        const int concurrency = 16;

        using var barrier = new Barrier(concurrency);
        var maps = new NativeMemoryMap[concurrency];

        try
        {
            var tasks = Enumerable
                .Range(0, concurrency)
                .Select(i =>
                    Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        maps[i] = NativeMemoryMap.OpenOrCreateShared(name, size);
                    })
                )
                .ToArray();

            await Task.WhenAll(tasks);

            // Verify all tasks acquired valid pointers to the exact same region
            for (int i = 0; i < concurrency; i++)
            {
                Assert.NotNull(maps[i]);

                unsafe
                {
                    Assert.True(maps[i].Pointer != null);
                }
            }

            // Write through map 0; assert visibility across all other views
            maps[0].AsSpan()[100] = 0xEE;
            for (int i = 1; i < concurrency; i++)
            {
                Assert.Equal(0xEE, maps[i].AsSpan()[100]);
            }
        }
        finally
        {
            for (int i = 0; i < concurrency; i++)
            {
                maps[i]?.Dispose();
            }
            NativeMemoryMap.UnlinkShared(name);
        }
    }

    [Fact]
    public void OpenExisting_ThrowsWhenExistingSegmentIsTooSmall()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return; // fstat size verification test is POSIX-specific

        string name = $"test_truncate_{Guid.NewGuid():N}";

        // Allocate only 4KB
        using var owner = NativeMemoryMap.CreateNewShared(name, 4096);

        // Attempt to open expecting 8KB -> must fail, preventing SIGBUS
        Assert.Throws<IOException>(() =>
        {
            using var reader = NativeMemoryMap.OpenExistingShared(name, 8192);
        });
    }

    [Fact]
    public unsafe void NativeStat_HasExpectedLinuxLayout()
    {
        if (!OperatingSystem.IsLinux())
            return;

        Assert.Equal(144, sizeof(Stat));

        Assert.Equal((nuint)0, (nuint)Marshal.OffsetOf<Stat>(nameof(Stat.st_dev)));
        Assert.Equal((nuint)8, (nuint)Marshal.OffsetOf<Stat>(nameof(Stat.st_ino)));
        Assert.Equal((nuint)16, (nuint)Marshal.OffsetOf<Stat>(nameof(Stat.st_nlink)));
        Assert.Equal((nuint)24, (nuint)Marshal.OffsetOf<Stat>(nameof(Stat.st_mode)));
        Assert.Equal((nuint)28, (nuint)Marshal.OffsetOf<Stat>(nameof(Stat.st_uid)));
        Assert.Equal((nuint)32, (nuint)Marshal.OffsetOf<Stat>(nameof(Stat.st_gid)));
        Assert.Equal((nuint)40, (nuint)Marshal.OffsetOf<Stat>(nameof(Stat.st_rdev)));
        Assert.Equal((nuint)48, (nuint)Marshal.OffsetOf<Stat>(nameof(Stat.st_size)));
    }

    [Fact]
    public unsafe void NativeWindows_HasExpectedStructLayouts()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Equal(16, sizeof(Win32MemoryRangeEntry));
        Assert.Equal(16, sizeof(UnicodeString));
        Assert.Equal(48, sizeof(ObjectAttributes));
    }

    [Fact]
    public void NativeStat_FstatReadsSharedMemorySize()
    {
        if (!OperatingSystem.IsLinux())
            return;

        string name = $"/test_fstat_{Guid.NewGuid():N}";
        PosixOpenFlags flags =
            PosixOpenFlags.ReadWrite | PosixOpenFlags.Create | PosixOpenFlags.Exclusive;

        int fd = Libc.Shm_open(
            name,
            flags,
            0b110_000_000 /* 0600 */
        );
        Assert.True(fd >= 0);

        try
        {
            Assert.Equal(0, Libc.Ftruncate(fd, 4096));
            Assert.Equal(0, Libc.Fstat(fd, out Stat stat));
            Assert.Equal(4096, stat.st_size);
        }
        finally
        {
            Libc.Close(fd);
            Libc.Shm_unlink(name);
        }
    }

    [Fact]
    public unsafe void NativeMmap_MunmapRoundTrip()
    {
        if (!OperatingSystem.IsLinux())
            return;

        const nuint size = 4096;
        string name = $"/test_mmap_{Guid.NewGuid():N}";
        PosixOpenFlags flags =
            PosixOpenFlags.ReadWrite | PosixOpenFlags.Create | PosixOpenFlags.Exclusive;

        int fd = Libc.Shm_open(
            name,
            flags,
            0b110_000_000 /* 0600 */
        );
        Assert.True(fd >= 0);

        try
        {
            Assert.Equal(0, Libc.Ftruncate(fd, (nint)size));

            void* ptr = Libc.Mmap(
                null,
                size,
                MemoryMapProt.Read | MemoryMapProt.Write,
                MemoryMapFlags.Shared,
                fd,
                0
            );

            Assert.True(ptr != Libc.MAP_FAILED);

            ((byte*)ptr)[0] = 0x7F;
            Assert.Equal(0x7F, ((byte*)ptr)[0]);

            Assert.Equal(0, Libc.Munmap(ptr, size));
        }
        finally
        {
            Libc.Close(fd);
            Libc.Shm_unlink(name);
        }
    }
}
