using System.Runtime.InteropServices;

namespace Platform.Native.Posix.Structs;

/// <summary>
/// POSIX stat layout on 64-bit Linux (x86_64).
/// </summary>
/// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/fstat.2.html"/></remarks>
[StructLayout(LayoutKind.Sequential)]
internal struct Stat
{
    public ulong st_dev;
    public ulong st_ino;
    public ulong st_nlink;
    public uint st_mode;
    public uint st_uid;
    public uint st_gid;
    public uint __pad0;
    public ulong st_rdev;
    public long st_size;
    public long st_blksize;
    public long st_blocks;
    public long st_atime;
    public ulong st_atime_nsec;
    public long st_mtime;
    public ulong st_mtime_nsec;
    public long st_ctime;
    public ulong st_ctime_nsec;
    private long __unused4;
    private long __unused5;
    private long __unused6;
}
