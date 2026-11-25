using System.Runtime.CompilerServices;

namespace Platform.Benchmarking.Harness.Telemetry;

/// <summary>
/// Common interface for unmanaged metric structures protected by a 32-bit sequence lock.
/// </summary>
public interface ISeqlockMetricBlock
{
    public uint Sequence { get; set; }
}

/// <summary>
/// Lock-free, zero-allocation Seqlock reader for live progress snapshots across threads.
/// </summary>
public static unsafe class SeqlockMetricsReader
{
    private const int _defaultMaxSpinAttempts = 100_000;

    /// <summary>
    /// Atomically reads an unmanaged Seqlock-protected block without blocking writers.
    /// </summary>
    /// <typeparam name="T">Unmanaged metric struct containing a leading uint sequence field.</typeparam>
    /// <param name="pSource">Pointer to shared unmanaged memory block.</param>
    /// <param name="maxSpinAttempts">Bounded spin threshold before returning current state.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Read<T>(T* pSource, int maxSpinAttempts = _defaultMaxSpinAttempts)
        where T : unmanaged
    {
        if (pSource == null)
            return default;

        T result = default;
        var spin = new SpinWait();
        int attempts = 0;

        uint* pVersion = (uint*)pSource;

        while (attempts++ < maxSpinAttempts)
        {
            uint v1 = Volatile.Read(ref *pVersion);

            // Odd sequence indicates writer is actively mutating the memory block
            if ((v1 & 1) != 0)
            {
                spin.SpinOnce();
                continue;
            }

            // Copy block contents directly
            Unsafe.CopyBlockUnaligned(
                destination: Unsafe.AsPointer(ref result),
                source: pSource,
                byteCount: (uint)sizeof(T)
            );

            // Read memory barrier to prevent reordering
            uint v2 = Volatile.Read(ref *pVersion);

            // Stable read validated: sequence didn't mutate and was never odd
            if (v1 == v2 && (v1 & 1) == 0)
            {
                return result;
            }

            spin.SpinOnce();
        }

        // Bounded fallback: copy current memory directly if spin timed out
        Unsafe.CopyBlockUnaligned(
            destination: Unsafe.AsPointer(ref result),
            source: pSource,
            byteCount: (uint)sizeof(T)
        );

        return result;
    }

    /// <summary>
    /// Writes a complete metric snapshot using Seqlock protocol.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Write<T>(T* pDestination, in T value)
        where T : unmanaged
    {
        if (pDestination == null)
            return;

        uint* pVersion = (uint*)pDestination;
        uint seq = *pVersion;

        // 1. Mark mutating (odd)
        Volatile.Write(ref *pVersion, seq + 1);

        // 2. Copy payload (excluding the 4-byte sequence header)
        byte* pDstPayload = (byte*)pDestination + sizeof(uint);
        fixed (T* pSrc = &value)
        {
            byte* pSrcPayload = (byte*)pSrc + sizeof(uint);
            Unsafe.CopyBlockUnaligned(
                destination: pDstPayload,
                source: pSrcPayload,
                byteCount: (uint)(sizeof(T) - sizeof(uint))
            );
        }

        // 3. Mark completed (even)
        Volatile.Write(ref *pVersion, seq + 2);
    }
}
