using System.Runtime.InteropServices;

namespace Platform.Native.Posix.Structs;

/// <summary>
/// Directory entry structure returned by readdir.
/// </summary>
/// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/readdir.3.html"/></remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PosixDirent
{
    public ulong d_ino;
    public long d_off;
    public ushort d_reclen;
    public byte d_type;
    public fixed byte d_name[256];
}
