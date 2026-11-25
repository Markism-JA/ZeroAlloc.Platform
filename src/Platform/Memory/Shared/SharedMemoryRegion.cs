using Platform.Internal;
using System.Runtime.CompilerServices;

namespace Platform.Memory.Shared;

/// <summary>
/// A lightweight, zero-allocation pre-resolved projection of a partitioned shared memory region.
/// Caches direct unmanaged pointers to eliminate descriptor table scans on hot execution paths.
/// </summary>
public readonly unsafe struct SharedMemoryRegion
{
    private readonly byte* _pointer;
    private readonly nuint _length;
    private readonly uint _tag;
    private readonly nuint _offset;

    /// <summary>Base unmanaged pointer to the start of this partition.</summary>
    public byte* Pointer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _pointer;
    }

    /// <summary>Total byte length of this partition.</summary>
    public nuint Length => _length;

    /// <summary>32-bit partition identifier tag.</summary>
    public uint Tag => _tag;

    /// <summary>Byte offset of this partition from the start of the underlying mapped segment.</summary>
    public nuint Offset => _offset;

    internal SharedMemoryRegion(byte* pointer, nuint length, uint tag, nuint offset)
    {
        _pointer = pointer;
        _length = length;
        _tag = tag;
        _offset = offset;
    }

    /// <summary>Returns a mutable byte span covering the entire partition.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> AsSpan() => new(_pointer, (int)_length);

    /// <summary>Returns a read-only byte span covering the entire partition.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> AsReadOnlySpan() => new(_pointer, (int)_length);

    /// <summary>Projects the entire partition as a contiguous span of unmanaged <typeparamref name="T"/> elements.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan<T>()
        where T : unmanaged
    {
        nuint elementSize = (nuint)sizeof(T);
        int count = (int)(_length / elementSize);
        return new Span<T>(_pointer, count);
    }

    /// <summary>Projects a bounds-checked slice of <paramref name="count"/> elements of <typeparamref name="T"/> starting at <paramref name="offsetBytes"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan<T>(int offsetBytes, int count)
        where T : unmanaged
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);
        PlatformCompat.ThrowIfNegative(count);

        nuint required = (nuint)offsetBytes + (nuint)count * (nuint)sizeof(T);
        if (required > _length)
            ThrowOutOfBounds(offsetBytes, (int)required, _length);

        return new Span<T>(_pointer + offsetBytes, count);
    }

    /// <summary>Returns a bounds-checked managed reference to an unmanaged <typeparamref name="T"/> at <paramref name="offsetBytes"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T AsRef<T>(int offsetBytes = 0)
        where T : unmanaged
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);
        nuint required = (nuint)offsetBytes + (nuint)sizeof(T);
        if (required > _length)
            ThrowOutOfBounds(offsetBytes, sizeof(T), _length);

        return ref Unsafe.AsRef<T>(_pointer + offsetBytes);
    }

    /// <summary>Writes an unmanaged value at <paramref name="offsetBytes"/> (bounds-checked, unaligned-safe).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Write<T>(int offsetBytes, in T value)
        where T : unmanaged
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);
        nuint required = (nuint)offsetBytes + (nuint)sizeof(T);
        if (required > _length)
            ThrowOutOfBounds(offsetBytes, sizeof(T), _length);

        Unsafe.WriteUnaligned(_pointer + offsetBytes, value);
    }

    /// <summary>Reads an unmanaged value from <paramref name="offsetBytes"/> (bounds-checked, unaligned-safe).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Read<T>(int offsetBytes)
        where T : unmanaged
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);
        nuint required = (nuint)offsetBytes + (nuint)sizeof(T);
        if (required > _length)
            ThrowOutOfBounds(offsetBytes, sizeof(T), _length);

        return Unsafe.ReadUnaligned<T>(_pointer + offsetBytes);
    }

    /// <summary>
    /// Projects a composite structure consisting of a fixed control header followed by a contiguous array of items.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref THeader GetHeaderAndTrailingSpan<THeader, TItem>(out Span<TItem> trailingSpan)
        where THeader : unmanaged
        where TItem : unmanaged
    {
        nuint headerSize = (nuint)sizeof(THeader);
        if (headerSize > _length)
            ThrowOutOfBounds(0, sizeof(THeader), _length);

        nuint remainingBytes = _length - headerSize;
        nuint itemSize = (nuint)sizeof(TItem);
        int itemCount = (int)(remainingBytes / itemSize);

        trailingSpan = new Span<TItem>(_pointer + headerSize, itemCount);
        return ref Unsafe.AsRef<THeader>(_pointer);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOutOfBounds(int offset, int requested, nuint capacity) =>
        throw new ArgumentOutOfRangeException(
            nameof(offset),
            $"Access at offset {offset} + {requested}B exceeds region capacity of {capacity}B."
        );
}
