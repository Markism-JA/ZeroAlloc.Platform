using System.Runtime.InteropServices;

namespace Platform.Native.Posix.Structs;

/// <summary>
/// Unmanaged memory layout for glibc <c>sem_t</c> on Linux x86-64 and ARM64 (32 bytes, 8-byte aligned).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 32, Pack = 8)]
public struct SemT
{
    /// <summary>8-byte alignment anchor for the opaque POSIX semaphore storage.</summary>
    [FieldOffset(0)]
    public long Align;
}
