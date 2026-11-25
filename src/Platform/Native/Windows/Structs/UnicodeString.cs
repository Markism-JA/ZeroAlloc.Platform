using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Platform.Native.Windows.Structs;

/// <summary>
/// Counted Unicode string representation used by NT kernel routines.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/subauth/ns-subauth-unicode_string"/></remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct UnicodeString
{
    public ushort Length;
    public ushort MaximumLength;
    public char* Buffer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<char> AsSpan() =>
        Buffer == null || Length == 0
            ? ReadOnlySpan<char>.Empty
            : new ReadOnlySpan<char>(Buffer, Length / sizeof(char));

    public override readonly string ToString() => AsSpan().ToString();
}
