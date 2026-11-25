using Platform.Memory;

namespace Platform.Tests.Memory;

public sealed class NativeMemoryMapFileTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    private string GetTempFilePath()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mmap_test_{Guid.NewGuid():N}.bin");
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string file in _tempFiles)
        {
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch
                { /* best effort */
                }
            }
        }
    }

    [Fact]
    public void CreateNewFile_PersistsDataToDisk_ReadableViaStandardIO()
    {
        string path = GetTempFilePath();
        const nuint size = 4096;

        using (var map = NativeMemoryMap.CreateNewFile(path, size))
        {
            var span = map.AsSpan();
            span[0] = 0xAA;
            span[1024] = 0xBB;
            span[4095] = 0xCC;

            map.Flush(flushToDisk: true);
        } // Unmapped and OS file handles closed here

        // Verify data actually hit the filesystem using standard .NET file APIs
        byte[] fileBytes = File.ReadAllBytes(path);
        Assert.Equal((int)size, fileBytes.Length);
        Assert.Equal(0xAA, fileBytes[0]);
        Assert.Equal(0xBB, fileBytes[1024]);
        Assert.Equal(0xCC, fileBytes[4095]);
    }

    [Fact]
    public void CreateNewFile_PreallocatesFullSizeOnDisk()
    {
        string path = GetTempFilePath();
        const nuint size = 65536;

        using (var map = NativeMemoryMap.CreateNewFile(path, size))
        {
            // Verify posix_fallocate (Linux) or CreateFileMapping (Windows)
            // allocated physical blocks immediately rather than a 0-byte sparse stub.
            var fileInfo = new FileInfo(path);
            Assert.Equal((long)size, fileInfo.Length);
        }
    }

    [Fact]
    public void CreateNewFile_ThrowsWhenFileAlreadyExists()
    {
        string path = GetTempFilePath();
        File.WriteAllBytes(path, new byte[100]);

        Assert.Throws<IOException>(() =>
        {
            using var map = NativeMemoryMap.CreateNewFile(path, 4096);
        });
    }

    [Fact]
    public void OpenExistingFile_WithZeroByteCount_MapsEntireFileLength()
    {
        string path = GetTempFilePath();
        byte[] payload = new byte[8192];
        payload[0] = 0x11;
        payload[8191] = 0x22;
        File.WriteAllBytes(path, payload);

        // byteCount = 0 instructs the factory to detect length via stat / GetFileSizeEx
        using var map = NativeMemoryMap.OpenExistingFile(path, byteCount: 0);

        Assert.Equal((nuint)8192, map.Length);
        Assert.Equal(0x11, map.AsSpan()[0]);
        Assert.Equal(0x22, map.AsSpan()[8191]);
    }

    [Fact]
    public void OpenExistingFile_ThrowsWhenRequestedSizeExceedsFileOnDisk()
    {
        string path = GetTempFilePath();
        File.WriteAllBytes(path, new byte[4096]);

        // Attempting to map 8192B from a 4096B file must fail immediately (InvalidSize)
        Assert.Throws<IOException>(() =>
        {
            using var map = NativeMemoryMap.OpenExistingFile(path, byteCount: 8192);
        });
    }

    [Fact]
    public void FileMap_DisposeDoesNotDeleteUnderlyingFile()
    {
        string path = GetTempFilePath();

        using (var map = NativeMemoryMap.CreateNewFile(path, 4096))
        {
            map.AsSpan()[0] = 0xFE;
        } // Dispose must NOT unlink regular files

        Assert.True(File.Exists(path));
        Assert.Equal(0xFE, File.ReadAllBytes(path)[0]);
    }

    [Fact]
    public void FileMap_ReleasesLockOnDispose_AllowingFileOps()
    {
        string path = GetTempFilePath();

        using (var map = NativeMemoryMap.CreateNewFile(path, 4096))
        {
            map.AsSpan()[0] = 0x01;
        }

        // On Windows, if MapViewOfFile or CreateFile handles are leaked,
        // File.Delete throws an IOException (Sharing violation / ERROR_USER_MAPPED_FILE).
        File.Delete(path);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void OpenOrCreateFile_CoherencyAcrossMultipleMappings()
    {
        string path = GetTempFilePath();
        const nuint size = 4096;

        using var map1 = NativeMemoryMap.OpenOrCreateFile(path, size);
        using var map2 = NativeMemoryMap.OpenOrCreateFile(path, size);

        map1.AsSpan()[42] = 0x7E;

        // Verify write visibility through second view
        Assert.Equal(0x7E, map2.AsSpan()[42]);
    }
}
