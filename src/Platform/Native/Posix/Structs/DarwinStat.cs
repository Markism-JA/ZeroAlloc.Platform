using System.Runtime.InteropServices;

namespace Platform.Native.Posix.Structs;

/// <summary>
/// 64-bit Darwin (macOS) struct stat layout (144 bytes, st_size at offset 96).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 144)]
internal struct DarwinStat
{
    [FieldOffset(96)]
    public long st_size;
}
