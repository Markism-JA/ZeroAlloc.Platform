using System.Runtime.CompilerServices;

namespace Platform.Memory.Shared;

/// <summary>
/// Packs and unpacks 4-byte ASCII FourCC identifiers into little-endian <see cref="uint"/> tags.
/// </summary>
public static class FourCC
{
    /// <summary>Encodes four ASCII characters into a little-endian 32-bit FourCC tag.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Make(char a, char b, char c, char d) =>
        (byte)a | ((uint)(byte)b << 8) | ((uint)(byte)c << 16) | ((uint)(byte)d << 24);

    /// <summary>Encodes a 4-character span into a little-endian 32-bit FourCC tag.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Make(ReadOnlySpan<char> code)
    {
        if (code.Length != 4)
        {
            throw new ArgumentException("FourCC code must be exactly 4 characters.", nameof(code));
        }

        return (byte)code[0]
            | ((uint)(byte)code[1] << 8)
            | ((uint)(byte)code[2] << 16)
            | ((uint)(byte)code[3] << 24);
    }

    /// <summary>Decodes a little-endian 32-bit FourCC tag back into a 4-character string.</summary>
    public static string ToString(uint code)
    {
        Span<char> chars =
        [
            (char)(byte)(code & 0xFF),
            (char)(byte)((code >> 8) & 0xFF),
            (char)(byte)((code >> 16) & 0xFF),
            (char)(byte)((code >> 24) & 0xFF),
        ];
        return new string(chars);
    }
}
