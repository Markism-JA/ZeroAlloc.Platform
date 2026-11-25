using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Native.Posix;
using Platform.Native.Windows;

namespace Platform.Threading;

/// <summary>
/// Low-level utility for pinning execution threads to dedicated CPU cores.
/// </summary>
public static unsafe class ThreadAffinityUtility
{
    /// <summary>
    /// Pins the calling thread to a single logical CPU core index.
    /// </summary>
    /// <param name="coreIndex">Zero-based core index (0 to 63).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="coreIndex"/> is outside 0..63.</exception>
    /// <exception cref="Win32Exception">Thrown if Windows fails to apply the affinity mask.</exception>
    /// <exception cref="IOException">Thrown if Linux fails to apply the affinity mask.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void PinCurrentThreadToCore(int coreIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(coreIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(coreIndex, 63);

        ulong affinityMask = 1UL << coreIndex;
        SetCurrentThreadAffinityMask(affinityMask);
    }

    /// <summary>
    /// Sets the CPU affinity bitmask for the calling thread.
    /// </summary>
    /// <param name="affinityMask">64-bit affinity bitmask representing allowed logical processors.</param>
    public static void SetCurrentThreadAffinityMask(ulong affinityMask)
    {
        ArgumentOutOfRangeException.ThrowIfZero(affinityMask);

        if (OperatingSystem.IsWindows())
        {
            nint thread = Kernel32.GetCurrentThread();
            nuint previous = Kernel32.SetThreadAffinityMask(thread, (nuint)affinityMask);

            if (previous == 0)
            {
                int error = Marshal.GetLastPInvokeError();
                throw new Win32Exception(
                    error,
                    $"Failed to set Windows thread affinity mask ({error}): {Marshal.GetPInvokeErrorMessage(error)}"
                );
            }

            return;
        }

        if (OperatingSystem.IsLinux())
        {
            nint thread = Libc.PthreadSelf();
            ulong mask = affinityMask;

            // Linux pthread_setaffinity_np returns the error code directly on failure
            int error = Libc.PthreadSetAffinityNp(thread, sizeof(ulong), &mask);
            if (error != 0)
            {
                throw new IOException(
                    $"Failed to set Linux thread affinity mask ({error}): {Marshal.GetPInvokeErrorMessage(error)}"
                );
            }

            return;
        }

        throw new PlatformNotSupportedException(
            "Setting thread affinity is not supported on this operating system."
        );
    }
}
