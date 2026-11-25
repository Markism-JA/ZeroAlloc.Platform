using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Internal;
using Platform.Memory;
using Platform.Native.Posix.Enums;
using Platform.Native.Posix.Structs;

namespace Platform.Native.Posix;

/// <summary>
/// Translates POSIX ABI constants and syscalls cleanly between Linux (x64/ARM64) and Darwin/macOS (ARM64/x64).
/// </summary>
internal static class PosixAbi
{
    internal const string MacShmDirectory = "/tmp/zeroalloc_shm";

    public static bool IsPosix =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static MemoryMapFlags AnonymousMapFlag =>
        IsMacOS ? (MemoryMapFlags)0x1000 : MemoryMapFlags.Anonymous;

    public static MsyncFlags MsyncSyncFlag =>
        IsMacOS ? (MsyncFlags)0x10 : MsyncFlags.Sync;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MemoryMapFlags WithPreFault(MemoryMapFlags flags, MemoryMapOptions options)
    {
        if (!IsMacOS && (options & MemoryMapOptions.PreFault) != 0)
        {
            flags |= MemoryMapFlags.Populate;
        }

        return flags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PosixOpenFlags TranslateOpenFlags(PosixOpenFlags linuxFlags)
    {
        if (!IsMacOS)
            return linuxFlags;

        // Access mode bits (O_RDONLY=0, O_WRONLY=1, O_RDWR=2) are identical on Linux and Darwin.
        int darwin = (int)linuxFlags & 0x3;

        if ((linuxFlags & PosixOpenFlags.Create) != 0)
            darwin |= 0x0200; // Darwin O_CREAT
        if ((linuxFlags & PosixOpenFlags.Exclusive) != 0)
            darwin |= 0x0800; // Darwin O_EXCL
        if ((linuxFlags & PosixOpenFlags.Truncate) != 0)
            darwin |= 0x0400; // Darwin O_TRUNC
        if ((linuxFlags & PosixOpenFlags.Sync) != 0 || (linuxFlags & PosixOpenFlags.DSync) != 0)
            darwin |= 0x0080; // Darwin O_SYNC

        return (PosixOpenFlags)darwin;
    }

    public static bool TryGetFileSize(int fd, out long size)
    {
        if (IsMacOS)
        {
            int res =
                RuntimeInformation.ProcessArchitecture == Architecture.X64
                    ? Libc.FstatDarwinX64(fd, out DarwinStat dstat)
                    : Libc.FstatDarwinArm64(fd, out dstat);
            if (res != 0)
            {
                size = 0;
                return false;
            }

            size = dstat.st_size;
            return true;
        }

        if (Libc.Fstat(fd, out Stat stat) != 0)
        {
            size = 0;
            return false;
        }

        size = stat.st_size;
        return true;
    }

    public static int AllocateFileSpace(int fd, nuint byteCount)
    {
        if (IsMacOS)
        {
            return Libc.Ftruncate(fd, (nint)byteCount) == 0 ? 0 : PlatformCompat.GetLastError();
        }

        return Libc.Posix_Fallocate(fd, 0, (nint)byteCount);
    }

    public static int FlushDataSync(int fd)
    {
        return IsMacOS ? Libc.Fsync(fd) : Libc.Fdatasync(fd);
    }

    public static string GetNormalizedSharedPath(string name)
    {
        if (IsMacOS)
        {
            string sanitized = name.TrimStart('/').Replace('/', '_').Replace('\\', '_');
            return Path.Combine(MacShmDirectory, sanitized);
        }

        return name.StartsWith('/') ? name : "/" + name;
    }

    public static int OpenFile(string path, PosixOpenFlags linuxFlags, int mode)
    {
        int fd = Libc.Open(path, TranslateOpenFlags(linuxFlags), mode);
        // On Apple Silicon ARM64, open(path, oflag, ...) is variadic so C# P/Invoke passes `mode`
        // in register X2 instead of on the stack, creating files with 0000 permissions.
        // Non-variadic fchmod(fd, mode) restores the exact requested permission bits.
        if (fd >= 0 && IsMacOS && (linuxFlags & PosixOpenFlags.Create) != 0)
        {
            _ = Libc.Fchmod(fd, (ushort)mode);
        }

        return fd;
    }

    public static int OpenShared(string normalizedPath, PosixOpenFlags linuxFlags, int mode)
    {
        if (IsMacOS)
        {
            if ((linuxFlags & PosixOpenFlags.Create) != 0)
            {
                Directory.CreateDirectory(MacShmDirectory);
            }

            return OpenFile(normalizedPath, linuxFlags, mode);
        }

        return Libc.Shm_open(normalizedPath, linuxFlags, mode);
    }

    public static void UnlinkShared(string normalizedPath)
    {
        if (IsMacOS)
        {
            Libc.Unlink(normalizedPath);
        }
        else
        {
            Libc.Shm_unlink(normalizedPath);
        }
    }
}
