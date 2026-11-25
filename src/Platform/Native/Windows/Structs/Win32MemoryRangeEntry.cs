using System.Runtime.InteropServices;

namespace Platform.Native.Windows.Structs;

/// <summary>
/// Specifies a range of virtual addresses to prefetch.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/ns-memoryapi-win32_memory_range_entry"/></remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct Win32MemoryRangeEntry
{
    public void* VirtualAddress;
    public nuint NumberOfBytes;
}
