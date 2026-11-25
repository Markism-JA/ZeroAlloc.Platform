using Platform.Internal;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Platform.Memory.Shared;

public static unsafe class NativeMemoryMapExtensions
{
    /// <summary>
    /// Projects a typed reference directly over unmanaged data at the specified byte offset.
    /// Ideal for inspecting IPC headers, control blocks, or seqlock slots at offset 0.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsRef<T>(this NativeMemoryMap map, int offsetBytes = 0)
        where T : unmanaged
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);

        nuint requiredBytes = (nuint)offsetBytes + (nuint)sizeof(T);
        if (requiredBytes > map.Length)
        {
            ThrowOutOfBounds(offsetBytes, sizeof(T), map.Length);
        }

        byte* targetPtr = map.Pointer + offsetBytes;
        AssertAligned<T>(targetPtr);

        return ref Unsafe.AsRef<T>(targetPtr);
    }

    /// <summary>
    /// Writes a single unmanaged struct directly into the mapped region at the target offset.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(this NativeMemoryMap map, int offsetBytes, in T value)
        where T : unmanaged
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);

        nuint requiredBytes = (nuint)offsetBytes + (nuint)sizeof(T);
        if (requiredBytes > map.Length)
        {
            ThrowOutOfBounds(offsetBytes, sizeof(T), map.Length);
        }

        Unsafe.WriteUnaligned(map.Pointer + offsetBytes, value);
    }

    /// <summary>
    /// Reads an unmanaged value from the specified byte offset without taking a reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(this NativeMemoryMap map, int offsetBytes)
        where T : unmanaged
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);

        nuint requiredBytes = (nuint)offsetBytes + (nuint)sizeof(T);
        if (requiredBytes > map.Length)
        {
            ThrowOutOfBounds(offsetBytes, sizeof(T), map.Length);
        }

        return Unsafe.ReadUnaligned<T>(map.Pointer + offsetBytes);
    }

    /// <summary>
    /// Copies a contiguous byte buffer from memory into the mapped region.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void CopyFrom(
        this NativeMemoryMap map,
        int offsetBytes,
        ReadOnlySpan<byte> source
    )
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);

        nuint requiredBytes = (nuint)offsetBytes + (nuint)source.Length;
        if (requiredBytes > map.Length)
        {
            ThrowOutOfBounds(offsetBytes, source.Length, map.Length);
        }

        fixed (byte* srcPtr = source)
        {
            Unsafe.CopyBlockUnaligned(map.Pointer + offsetBytes, srcPtr, (uint)source.Length);
        }
    }

    /// <summary>
    /// Zeroes out the entire mapped address range or a specific sub-slice.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Clear(this NativeMemoryMap map, int offsetBytes = 0, int count = -1)
    {
        PlatformCompat.ThrowIfNegative(offsetBytes);

        nuint targetLength = count < 0 ? map.Length - (nuint)offsetBytes : (nuint)count;
        if ((nuint)offsetBytes + targetLength > map.Length)
        {
            ThrowOutOfBounds(offsetBytes, (int)targetLength, map.Length);
        }

        Unsafe.InitBlockUnaligned(map.Pointer + offsetBytes, 0, (uint)targetLength);
    }

    [Conditional("DEBUG")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AssertAligned<T>(void* ptr)
        where T : unmanaged
    {
        // Warns on misaligned struct overlays that could cause torn reads or CPU penalties
        int alignment =
            Unsafe.SizeOf<T>() >= 16 ? 16
            : Unsafe.SizeOf<T>() >= 8 ? 8
            : 4;
        Debug.Assert(
            ((nuint)ptr % (nuint)alignment) == 0,
            $"Pointer for {typeof(T).Name} is not aligned to {alignment} bytes."
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowOutOfBounds(int offset, int requested, nuint capacity)
    {
        throw new ArgumentOutOfRangeException(
            nameof(offset),
            $"Access at offset {offset} + {requested}B exceeds total mapped capacity of {capacity}B."
        );
    }
}
