using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Native.Posix;
using Platform.Native.Posix.Enums;
using Platform.Native.Windows;

namespace Platform.Time;

public static unsafe class NativeTsc
{
#if NET7_0_OR_GREATER
    private static readonly delegate* unmanaged[SuppressGCTransition]<ulong> ReadTsc;
#else
    private static readonly delegate* unmanaged[Cdecl]<ulong> ReadTsc;
#endif
    private static readonly bool IsSupported;
    private static readonly double TicksToNs = 1_000_000_000.0 / Stopwatch.Frequency;
    private static readonly long FallbackStopwatchAnchorTicks;
    private static readonly long FallbackUnixEpochAnchorNs;

    // Seqlock-protected anchor block (64 bytes aligned)
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct TscAnchor
    {
        [FieldOffset(0)]
        public int Sequence;

        [FieldOffset(8)]
        public ulong BaseTsc;

        [FieldOffset(16)]
        public long BaseUtcNs;

        [FieldOffset(24)]
        public ulong Multiplier;

        [FieldOffset(32)]
        public byte Shift;
    }

    private static TscAnchor _anchor;
    private static System.Threading.Timer? _syncTimer;

    /// <summary>
    /// True if direct hardware RDTSC sampling is active (x64 with executable page allocation);
    /// false when running on the UTC-anchored <see cref="Stopwatch"/> fallback (ARM64 or W^X restricted).
    /// </summary>
    public static bool IsHardwareTscActive => IsSupported;

    static NativeTsc()
    {
        FallbackStopwatchAnchorTicks = Stopwatch.GetTimestamp();
        FallbackUnixEpochAnchorNs = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) * 100L;

        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            IsSupported = false;
            return;
        }

        ReadOnlySpan<byte> opcodes = [0x0F, 0x31, 0x48, 0xC1, 0xE2, 0x20, 0x48, 0x09, 0xD0, 0xC3];

        void* executablePage = AllocateExecutableMemory((nuint)opcodes.Length);
        if (executablePage == null)
        {
            IsSupported = false;
            return;
        }

        fixed (byte* src = opcodes)
        {
            Buffer.MemoryCopy(src, executablePage, opcodes.Length, opcodes.Length);
        }

#if NET7_0_OR_GREATER
        ReadTsc = (delegate* unmanaged[SuppressGCTransition]<ulong>)executablePage;
#else
        ReadTsc = (delegate* unmanaged[Cdecl]<ulong>)executablePage;
#endif

        // Initial calibration
        CalibrateAnchor(isInitial: true);
        IsSupported = true;

        // Periodically realign anchors with the OS wall-clock every 10 seconds (0% CPU impact)
        _syncTimer = new System.Threading.Timer(
            _ => CalibrateAnchor(isInitial: false),
            null,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowNs()
    {
        if (IsSupported)
        {
            // Lock-free Seqlock read: zero contention across cores
            while (true)
            {
                int seq1 = Volatile.Read(ref _anchor.Sequence);
                if ((seq1 & 1) != 0) // Writer is mid-mutation
                {
                    Thread.SpinWait(2);
                    continue;
                }

                ulong baseTsc = _anchor.BaseTsc;
                long baseUtcNs = _anchor.BaseUtcNs;
                ulong multiplier = _anchor.Multiplier;
                byte shift = _anchor.Shift;

                // x86-64 TSO guarantees LoadLoad ordering in hardware; Volatile.Read provides the compiler fence.
                int seq2 = Volatile.Read(ref _anchor.Sequence);
                if (seq1 == seq2) // Read was consistent
                {
                    ulong tsc = ReadTsc();
                    ulong delta = tsc > baseTsc ? tsc - baseTsc : 0;
                    return baseUtcNs + (long)MulShiftRight(delta, multiplier, shift);
                }

                Thread.SpinWait(2);
            }
        }

        long elapsedTicks = Stopwatch.GetTimestamp() - FallbackStopwatchAnchorTicks;
        return FallbackUnixEpochAnchorNs + (long)(elapsedTicks * TicksToNs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long NowMs() => NowNs() / 1_000_000L;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong MulShiftRight(ulong a, ulong b, byte shift)
    {
#if NET5_0_OR_GREATER
        ulong hi = Math.BigMul(a, b, out ulong lo);
        return (lo >> shift) | (hi << (64 - shift));
#else
        ulong aLo = (uint)a;
        ulong aHi = a >> 32;
        ulong bLo = (uint)b;
        ulong bHi = b >> 32;

        ulong p0 = aLo * bLo;
        ulong p1 = aLo * bHi;
        ulong p2 = aHi * bLo;
        ulong p3 = aHi * bHi;

        ulong mid = (p0 >> 32) + (uint)p1 + (uint)p2;
        ulong lo = (p0 & 0xFFFF_FFFFUL) | (mid << 32);
        ulong hi = p3 + (p1 >> 32) + (p2 >> 32) + (mid >> 32);
        return (lo >> shift) | (hi << (64 - shift));
#endif
    }

    private static void CalibrateAnchor(bool isInitial)
    {
        // 1. Measure TSC cycles over a 10ms window against high-resolution stopwatch
        long swStart = Stopwatch.GetTimestamp();
        ulong tscStart = ReadTsc();

        long targetSw = swStart + (long)(Stopwatch.Frequency * 0.010);
        while (Stopwatch.GetTimestamp() < targetSw)
        {
            Thread.SpinWait(10);
        }

        long swEnd = Stopwatch.GetTimestamp();
        ulong tscEnd = ReadTsc();

        double elapsedSeconds = (double)(swEnd - swStart) / Stopwatch.Frequency;
        if (elapsedSeconds <= 0 || tscEnd <= tscStart)
            return;

        double tscFrequencyHz = (tscEnd - tscStart) / elapsedSeconds;

        byte shift = 24;
        double factor = (1_000_000_000.0 / tscFrequencyHz) * (1 << shift);
        ulong multiplier = (ulong)Math.Round(factor);

        // 2. Anchor directly to current UTC Unix Epoch, clamping monotonically on recalibration
        ulong anchorTsc = ReadTsc();
        long anchorUtcNs = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) * 100L;

        if (!isInitial)
        {
            ulong prevDelta = anchorTsc > _anchor.BaseTsc ? anchorTsc - _anchor.BaseTsc : 0;
            long prevProjectedNs =
                _anchor.BaseUtcNs + (long)MulShiftRight(prevDelta, _anchor.Multiplier, _anchor.Shift);
            if (anchorUtcNs < prevProjectedNs)
            {
                anchorUtcNs = prevProjectedNs;
            }
        }

        // 3. Seqlock write: odd sequence indicates mutation
        int seq = _anchor.Sequence;
        Volatile.Write(ref _anchor.Sequence, seq + 1);
        Thread.MemoryBarrier();

        _anchor.BaseTsc = anchorTsc;
        _anchor.BaseUtcNs = anchorUtcNs;
        _anchor.Multiplier = multiplier;
        _anchor.Shift = shift;

        // Even sequence indicates mutation complete (release semantics)
        Volatile.Write(ref _anchor.Sequence, seq + 2);
    }

    private static void* AllocateExecutableMemory(nuint size)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Kernel32.VirtualAlloc(null, size, 0x3000, 0x40);
        }

        if (
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            || RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        )
        {
            void* ptr = Libc.Mmap(
                null,
                size,
                MemoryMapProt.Read | MemoryMapProt.Write | MemoryMapProt.Execute,
                MemoryMapFlags.Private | MemoryMapFlags.Anonymous,
                -1,
                0
            );

            return ptr == Libc.MAP_FAILED ? null : ptr;
        }

        return null;
    }
}
