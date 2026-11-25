using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Platform.Native.Posix;
using Platform.Native.Windows;
using Platform.Native.Windows.Enums;

namespace Platform.Diagnostics;

/// <summary>
/// Provides low-level, zero-allocation diagnostic sink primitives for unrecoverable runtime states.
/// </summary>
/// <remarks>
/// <para>
/// This utility provides an emergency out-of-band communication channel directly to the host operating
/// system's standard error descriptor (<c>stderr</c>), bypassing the managed runtime, BCL <see cref="Console"/>
/// locks, heap-allocated formatters, and regular telemetry pipelines.
/// </para>
/// <para>
/// <b>Reliability Contract:</b>
/// <list type="bullet">
///   <item>
///     <description><b>Best-Effort Delivery:</b> Writes are synchronous and intentionally best-effort. Interrupted writes (<c>EINTR</c>), partial writes, closed descriptors, or broken pipes are not retried and will not throw.</description>
///   </item>
///   <item>
///     <description><b>Zero Allocations:</b> Executes without touching the GC heap, memory pools, or managed formatters.</description>
///   </item>
///   <item>
///     <description><b>Never Throws:</b> All OS errors and invalid handle conditions are silently discarded to prevent secondary faults during crash reporting.</description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <b>Caution:</b> Do not use this for ordinary operational logging or recoverable startup errors.
/// Early setup failures must throw standard managed exceptions.
/// </para>
/// </remarks>
public static unsafe class NativeDiagnostics
{
    /// <summary>
    /// Synchronously emits a UTF-8 encoded byte sequence directly to the host operating system's standard error stream.
    /// </summary>
    /// <param name="utf8Message">
    /// The UTF-8 encoded message payload to write. Typically supplied via a compile-time string literal span (e.g., <c>"message\n"u8</c>).
    /// </param>
    /// <remarks>
    /// This method performs zero managed allocations, avoids BCL stream locking, and routes directly to the underlying
    /// platform write syscall (<c>write</c> on POSIX platforms, <c>WriteFile</c> on Windows).
    /// Partial writes and OS-level delivery failures are silently tolerated.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write(ReadOnlySpan<byte> utf8Message)
    {
        if (utf8Message.IsEmpty)
            return;

        Debug.Assert(utf8Message.Length >= 0);

        fixed (byte* ptr = utf8Message)
        {
            if (OperatingSystem.IsWindows())
            {
                nint handle = Kernel32.GetStdHandle(StandardHandle.Error);
                if (handle != 0 && handle != Kernel32.INVALID_HANDLE_VALUE)
                {
                    uint written;
                    _ = Kernel32.WriteFile(handle, ptr, (uint)utf8Message.Length, &written, null);
                }

                return;
            }

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                _ = Libc.Write(PosixFileDescriptors.Stderr, ptr, (nuint)utf8Message.Length);
            }
        }
    }

    /// <summary>
    /// Synchronously writes a UTF-8 message directly to standard error and immediately terminates the process.
    /// </summary>
    /// <param name="utf8Message">
    /// The UTF-8 encoded fatal error description to output prior to termination.
    /// </param>
    /// <remarks>
    /// Attempts to emit the diagnostic message directly to the OS standard error sink before terminating the process
    /// via <see cref="Environment.FailFast(string)"/>. Finalizers, managed exception handlers, and unwind routines
    /// are bypassed to prevent secondary faults or cascading memory corruption, producing a deterministic core dump.
    /// </remarks>
    [DoesNotReturn]
    public static void FailFast(ReadOnlySpan<byte> utf8Message)
    {
        Write(utf8Message);
        Environment.FailFast(string.Empty);
    }
}
