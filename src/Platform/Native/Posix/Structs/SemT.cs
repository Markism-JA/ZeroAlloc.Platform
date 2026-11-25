using System.Runtime.InteropServices;

namespace Platform.Native.Posix.Structs;

/// <summary>
/// Unmanaged memory layout for glibc sem_t on Linux x86-64 / arm64 (32 bytes, 8-byte aligned).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 32, Pack = 8)]
public struct SemT
{
    [FieldOffset(0)]
    public long Align;
}
