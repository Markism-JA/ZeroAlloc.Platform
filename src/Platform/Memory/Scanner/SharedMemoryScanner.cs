using Platform.Internal;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Platform.Memory.Shared;
using Platform.Native.Posix;
using Platform.Native.Posix.Enums;
using Platform.Native.Posix.Structs;
using Platform.Native.Windows;
using Platform.Native.Windows.Enums;
using Platform.Native.Windows.Structs;

namespace Platform.Memory.Scanner;

public static unsafe class SharedMemoryScanner
{
    private const string _posixShmPath = "/dev/shm";

    /// <summary>
    /// Scans active shared memory segments into a caller-supplied buffer with zero heap allocations.
    /// </summary>
    /// <param name="prefix">Segment name prefix to match (e.g. "observability_telemetry_node").</param>
    /// <param name="destination">Target span to populate with discovered segment snapshots.</param>
    /// <param name="expectedMagic">Optional FourCC magic value to validate.</param>
    /// <param name="expectedAbiVersion">Optional ABI version to validate.</param>
    /// <returns>The total number of valid segments found and written to <paramref name="destination"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Scan(
        string prefix,
        Span<DiscoveredSegment> destination,
        uint expectedMagic = 0,
        uint expectedAbiVersion = 0
    )
    {
        PlatformCompat.ThrowIfNullOrWhiteSpace(prefix);

        if (destination.IsEmpty)
            return 0;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return ScanLinux(prefix, destination, expectedMagic, expectedAbiVersion);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ScanMacOS(prefix, destination, expectedMagic, expectedAbiVersion);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ScanWindows(prefix, destination, expectedMagic, expectedAbiVersion);

        return 0;
    }

    #region Linux POSIX Implementation

    private static int ScanLinux(
        string prefix,
        Span<DiscoveredSegment> destination,
        uint expectedMagic,
        uint expectedAbiVersion
    )
    {
        // Transcode prefix to stack UTF-8 once per scan (0 heap allocations)
        Span<byte> prefixUtf8 = stackalloc byte[Encoding.UTF8.GetMaxByteCount(prefix.Length)];
        int prefixByteCount = Encoding.UTF8.GetBytes(prefix, prefixUtf8);
        prefixUtf8 = prefixUtf8[..prefixByteCount];

        nint dirp = Libc.OpenDir(_posixShmPath);
        if (dirp == 0)
            return 0;

        int count = 0;
        try
        {
            PosixDirent* entry;
            while ((entry = Libc.ReadDir(dirp)) != null && count < destination.Length)
            {
                ReadOnlySpan<byte> dName = PlatformCompat.CreateReadOnlySpanFromNullTerminated(
                    entry->d_name
                );
                if (!dName.StartsWith(prefixUtf8))
                    continue;

                if (
                    TryProbePosix(
                        entry->d_name,
                        dName,
                        expectedMagic,
                        expectedAbiVersion,
                        out destination[count]
                    )
                )
                {
                    count++;
                }
            }
        }
        finally
        {
            Libc.CloseDir(dirp);
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryProbePosix(
        byte* rawNullTerminatedName,
        ReadOnlySpan<byte> nameSpan,
        uint expectedMagic,
        uint expectedAbiVersion,
        out DiscoveredSegment segment
    )
    {
        segment = default;

        int fd = Libc.Shm_open(rawNullTerminatedName, PosixOpenFlags.ReadOnly, 0);
        if (fd < 0)
            return false;

        void* ptr = null;
        try
        {
            // Verify file size prior to mmap to eliminate SIGBUS on in-creation (0-byte) segments
            if (Libc.Fstat(fd, out Stat stat) != 0)
                return false;

            nuint minSuperblockSize = SharedMemorySuperblock.SuperblockSize;
            if (stat.st_size <= 0 || (nuint)stat.st_size < minSuperblockSize)
                return false;

            ptr = Libc.Mmap(
                null,
                minSuperblockSize,
                MemoryMapProt.Read,
                MemoryMapFlags.Shared,
                fd,
                0
            );

            if (ptr == Libc.MAP_FAILED || ptr == null)
                return false;

            var sb = (SharedMemorySuperblock*)ptr;

            // Reject partially formatted or uninitialized superblocks
            if (Volatile.Read(ref sb->State) != (uint)SharedMemoryState.Ready)
                return false;

            if (expectedMagic != 0 && sb->Magic != expectedMagic)
                return false;

            if (expectedAbiVersion != 0 && sb->AbiVersion != expectedAbiVersion)
                return false;

            // Copy a 256-byte superblock snapshot onto a destination stack slice
            segment.Superblock = *sb;

            int copyLength = Math.Min(nameSpan.Length, 64);
            segment.NameLength = copyLength;

            fixed (byte* dest = segment.NameUtf8)
            {
                nameSpan[..copyLength].CopyTo(new Span<byte>(dest, 64));
            }

            return true;
        }
        finally
        {
            if (ptr != null && ptr != Libc.MAP_FAILED)
            {
                Libc.Munmap(ptr, SharedMemorySuperblock.SuperblockSize);
            }
            Libc.Close(fd);
        }
    }

    private static int ScanMacOS(
        string prefix,
        Span<DiscoveredSegment> destination,
        uint expectedMagic,
        uint expectedAbiVersion
    )
    {
        if (!Directory.Exists(PosixAbi.MacShmDirectory))
            return 0;

        Span<byte> nameUtf8 = stackalloc byte[64];
        int count = 0;
        foreach (string fullPath in Directory.EnumerateFiles(PosixAbi.MacShmDirectory))
        {
            if (count >= destination.Length)
                break;

            string fileName = Path.GetFileName(fullPath);
            if (!fileName.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            int fd = Libc.Open(fullPath, PosixOpenFlags.ReadOnly, 0);
            if (fd < 0)
                continue;

            void* ptr = null;
            try
            {
                if (!PosixAbi.TryGetFileSize(fd, out long fileSize))
                    continue;

                nuint minSuperblockSize = SharedMemorySuperblock.SuperblockSize;
                if (fileSize <= 0 || (nuint)fileSize < minSuperblockSize)
                    continue;

                ptr = Libc.Mmap(
                    null,
                    minSuperblockSize,
                    MemoryMapProt.Read,
                    MemoryMapFlags.Shared,
                    fd,
                    0
                );

                if (ptr == Libc.MAP_FAILED || ptr == null)
                    continue;

                var sb = (SharedMemorySuperblock*)ptr;
                if (Volatile.Read(ref sb->State) != (uint)SharedMemoryState.Ready)
                    continue;
                if (expectedMagic != 0 && sb->Magic != expectedMagic)
                    continue;
                if (expectedAbiVersion != 0 && sb->AbiVersion != expectedAbiVersion)
                    continue;

                DiscoveredSegment seg = default;
                seg.Superblock = *sb;

                int nameLen = Encoding.UTF8.GetBytes(
                    fileName.AsSpan(0, Math.Min(fileName.Length, 64)),
                    nameUtf8
                );
                seg.NameLength = nameLen;
                nameUtf8[..nameLen].CopyTo(new Span<byte>(seg.NameUtf8, 64));

                destination[count++] = seg;
            }
            finally
            {
                if (ptr != null && ptr != Libc.MAP_FAILED)
                {
                    Libc.Munmap(ptr, SharedMemorySuperblock.SuperblockSize);
                }
                Libc.Close(fd);
            }
        }

        return count;
    }

    #endregion

    #region Windows NT Implementation

    private static int ScanWindows(
        string prefix,
        Span<DiscoveredSegment> destination,
        uint expectedMagic,
        uint expectedAbiVersion
    )
    {
        ReadOnlySpan<char> prefixSpan = prefix.AsSpan();
        int count = 0;
        int sessionId = Process.GetCurrentProcess().SessionId;
        Span<char> dirPath = stackalloc char[64];

        if (sessionId == 0)
        {
            @"\BaseNamedObjects".AsSpan().CopyTo(dirPath);
            dirPath = dirPath[..17];
        }
        else
        {
            @"\Sessions\".AsSpan().CopyTo(dirPath);
            int idx = 10;
            sessionId.TryFormat(dirPath[idx..], out int written);
            idx += written;
            @"\BaseNamedObjects".AsSpan().CopyTo(dirPath[idx..]);
            dirPath = dirPath[..(idx + 17)];
        }

        fixed (char* pDir = dirPath)
        {
            var usDir = new UnicodeString
            {
                Length = (ushort)(dirPath.Length * sizeof(char)),
                MaximumLength = (ushort)((dirPath.Length + 1) * sizeof(char)),
                Buffer = pDir,
            };

            var objAttr = new ObjectAttributes
            {
                Length = sizeof(ObjectAttributes),
                ObjectName = &usDir,
            };

            if (
                NtDll.NtOpenDirectoryObject(
                    out nint dirHandle,
                    DirectoryAccessRights.Query,
                    in objAttr
                ) != NtStatus.STATUS_SUCCESS
                || dirHandle == 0
            )
                return 0;

            try
            {
                const int scratchSize = 8192;
                const int maxNameLength = 260;

                byte* scratch = stackalloc byte[scratchSize];
                Span<char> nullTerminatedName = stackalloc char[maxNameLength];

                uint context = 0;
                bool restartScan = true;

                fixed (char* pName = nullTerminatedName)
                {
                    while (count < destination.Length)
                    {
                        int status = NtDll.NtQueryDirectoryObject(
                            dirHandle,
                            scratch,
                            scratchSize,
                            returnSingleEntry: false,
                            restartScan: restartScan,
                            ref context,
                            out uint returnLen
                        );
                        restartScan = false;

                        if (
                            (
                                status != NtStatus.STATUS_SUCCESS
                                && status != NtStatus.STATUS_MORE_ENTRIES
                            )
                            || returnLen == 0
                        )
                        {
                            break;
                        }

                        var entry = (ObjectDirectoryInformation*)scratch;
                        while (
                            entry->Name.Buffer != null
                            && entry->Name.Length > 0
                            && count < destination.Length
                        )
                        {
                            ReadOnlySpan<char> type = entry->TypeName.AsSpan();
                            ReadOnlySpan<char> name = entry->Name.AsSpan();

                            if (
                                type.SequenceEqual("Section".AsSpan())
                                && name.StartsWith(prefixSpan)
                                && name.Length < maxNameLength
                            )
                            {
                                name.CopyTo(nullTerminatedName);
                                nullTerminatedName[name.Length] = '\0';

                                if (
                                    TryProbeWin32(
                                        pName,
                                        name,
                                        expectedMagic,
                                        expectedAbiVersion,
                                        out destination[count]
                                    )
                                )
                                {
                                    count++;
                                }
                            }

                            entry++;
                        }

                        if (status != NtStatus.STATUS_MORE_ENTRIES)
                            break;
                    }
                }
            }
            finally
            {
                NtDll.NtClose(dirHandle);
            }
        }

        return count;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryProbeWin32(
        char* nullTerminatedName,
        ReadOnlySpan<char> nameSpan,
        uint expectedMagic,
        uint expectedAbiVersion,
        out DiscoveredSegment segment
    )
    {
        segment = default;

        nint hMap = Kernel32.OpenFileMappingW(FileMapAccess.Read, false, nullTerminatedName);
        if (hMap == 0)
            return false;

        void* ptr = null;
        try
        {
            ptr = Kernel32.MapViewOfFile(
                hMap,
                FileMapAccess.Read,
                0,
                0,
                SharedMemorySuperblock.SuperblockSize
            );
            if (ptr == null)
                return false;

            var sb = (SharedMemorySuperblock*)ptr;
            if (Volatile.Read(ref sb->State) != (uint)SharedMemoryState.Ready)
                return false;

            if (expectedMagic != 0 && sb->Magic != expectedMagic)
                return false;

            if (expectedAbiVersion != 0 && sb->AbiVersion != expectedAbiVersion)
                return false;

            segment.Superblock = *sb;
            fixed (byte* dest = segment.NameUtf8)
            {
                segment.NameLength = Encoding.UTF8.GetBytes(nameSpan, new Span<byte>(dest, 64));
            }

            return true;
        }
        finally
        {
            if (ptr != null)
                Kernel32.UnmapViewOfFile(ptr);

            Kernel32.CloseHandle(hMap);
        }
    }

    #endregion
}
