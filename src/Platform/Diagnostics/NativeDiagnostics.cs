using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Platform.Native.Posix;
using Platform.Native.Windows;
using Platform.Native.Windows.Enums;

namespace Platform.Diagnostics;

/// <summary>
/// Provides low-level, zero-allocation diagnostic sink primitives for unrecoverable runtime states.
/// </summary>
public static unsafe class NativeDiagnostics
{
    /// <summary>
    /// Synchronously writes a UTF-8 byte span directly to OS standard error (<c>stderr</c>) with zero managed allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ReadOnlySpan<byte> utf8Message)
    {
        if (utf8Message.IsEmpty)
            return;

        Debug.Assert(utf8Message.Length >= 0);

        fixed (byte* ptr = utf8Message)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                nint handle = Kernel32.GetStdHandle(StandardHandle.Error);
                if (handle != 0 && handle != Kernel32.INVALID_HANDLE_VALUE)
                {
                    uint written;
                    _ = Kernel32.WriteFile(handle, ptr, (uint)utf8Message.Length, &written, null);
                }

                return;
            }

            if (
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            )
            {
                _ = Libc.Write(PosixFileDescriptors.Stderr, ptr, (nuint)utf8Message.Length);
                return;
            }
        }

        Console.Error.Write(Encoding.UTF8.GetString(utf8Message));
    }

    /// <summary>
    /// Writes a UTF-8 fatal message directly to <c>stderr</c> and immediately terminates the process via <see cref="Environment.FailFast(string)"/>.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FailFast(ReadOnlySpan<byte> utf8Message)
    {
        Write(utf8Message);
        Environment.FailFast(string.Empty);
    }

    /// <summary>
    /// Writes a fatal message to standard error and immediately terminates the process via <see cref="Environment.FailFast(string)"/>.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void FailFast(string message)
    {
        Console.Error.WriteLine(message);
        Environment.FailFast(message);
    }
}
