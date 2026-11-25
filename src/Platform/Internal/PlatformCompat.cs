using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Platform.Internal;

internal static unsafe class PlatformCompat
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetLastError()
    {
#if NET6_0_OR_GREATER
        return Marshal.GetLastPInvokeError();
#else
        return Marshal.GetLastWin32Error();
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetErrorMessage(int error)
    {
#if NET6_0_OR_GREATER
        return Marshal.GetPInvokeErrorMessage(error);
#else
        return $"OS error {error}";
#endif
    }

    public static nuint SystemPageSize
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
#if NET5_0_OR_GREATER
            return (nuint)Environment.SystemPageSize;
#else
            return 4096;
#endif
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPow2(uint value) => value != 0 && (value & (value - 1)) == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TTo BitCast<TFrom, TTo>(TFrom source)
        where TFrom : struct
        where TTo : struct
    {
#if NET8_0_OR_GREATER
        return Unsafe.BitCast<TFrom, TTo>(source);
#else
        return Unsafe.As<TFrom, TTo>(ref source);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> CreateReadOnlySpanFromNullTerminated(byte* value)
    {
#if NET6_0_OR_GREATER
        return MemoryMarshal.CreateReadOnlySpanFromNullTerminated(value);
#else
        if (value == null)
            return ReadOnlySpan<byte>.Empty;

        int len = 0;
        while (value[len] != 0)
            len++;
        return new ReadOnlySpan<byte>(value, len);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull(object? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null)
            throw new ArgumentNullException(paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull(void* value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == null)
            throw new ArgumentNullException(paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfZero(nuint value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == 0)
            throw new ArgumentOutOfRangeException(paramName, "Value must be non-zero.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfZero(ulong value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == 0)
            throw new ArgumentOutOfRangeException(paramName, "Value must be non-zero.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative(int value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, "Value must be non-negative.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative(long value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, "Value must be non-negative.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfGreaterThan(int value, int other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value > other)
            throw new ArgumentOutOfRangeException(paramName, $"Value must be <= {other}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfGreaterThan(nuint value, nuint other, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value > other)
            throw new ArgumentOutOfRangeException(paramName, $"Value must be <= {other}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrWhiteSpace(string? value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value cannot be null or whitespace.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed(bool condition, object instance)
    {
        if (condition)
            throw new ObjectDisposedException(instance.GetType().FullName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed(bool condition, Type type)
    {
        if (condition)
            throw new ObjectDisposedException(type.FullName);
    }
}
