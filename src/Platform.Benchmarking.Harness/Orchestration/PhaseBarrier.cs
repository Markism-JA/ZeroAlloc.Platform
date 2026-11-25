using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Platform.Threading;

namespace Platform.Benchmarking.Harness.Orchestration;

/// <summary>
/// Cache-aligned, 0-allocation synchronization barrier for multi-core probe rendezvous.
/// Uses progressive spin-wait before falling back to native thread parking.
/// </summary>
public sealed unsafe class PhaseBarrier : IDisposable
{
    private const int _spinCountThreshold = 2048;

    private readonly int _participantCount;
    private readonly ThreadParker[] _parkers;
    private readonly NativeThreadParkerHandle* _nativeHandles;
    private readonly int* _stateBlock; // [0] = Count, [1] = Generation
    private int _isDisposed;

    public int ParticipantCount => _participantCount;

    public PhaseBarrier(int participantCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(participantCount);

        _participantCount = participantCount;

        // Allocate state variables aligned to 64 bytes (cache-line boundary)
        _stateBlock = (int*)NativeMemory.AlignedAlloc(64, 64);
        NativeMemory.Clear(_stateBlock, 64);

        nuint handlesSize = (nuint)(participantCount * sizeof(NativeThreadParkerHandle));
        _nativeHandles = (NativeThreadParkerHandle*)NativeMemory.AllocZeroed(handlesSize);

        _parkers = new ThreadParker[participantCount];
        for (int i = 0; i < participantCount; i++)
        {
            var parker = new ThreadParker();
            _parkers[i] = parker;
            _nativeHandles[i] = parker.NativeHandle;
        }
    }

    /// <summary>
    /// Signals arrival and waits for all participant cores to arrive at this phase.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SignalAndWait(int participantIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(participantIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(participantIndex, _participantCount);

        int* pCount = &_stateBlock[0];
        int* pGen = &_stateBlock[1];

        int currentGen = Volatile.Read(ref *pGen);
        int arrived = Interlocked.Increment(ref *pCount);

        if (arrived == _participantCount)
        {
            // Last core to arrive resets the counter and bumps generation
            Volatile.Write(ref *pCount, 0);
            Interlocked.Increment(ref *pGen);

            // Unconditionally unpark all participants to ensure no token is dropped
            WakeAllParticipants();
            return;
        }

        // Progressive spin-wait
        var spinWait = new SpinWait();
        int spins = 0;
        while (Volatile.Read(ref *pGen) == currentGen)
        {
            if (++spins < _spinCountThreshold)
            {
                spinWait.SpinOnce();
                continue;
            }

            // Fallback to bounded kernel wait (10 ms)
            _parkers[participantIndex].Park(timeoutMs: 10);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void WakeAllParticipants()
    {
        for (int i = 0; i < _participantCount; i++)
        {
            // Direct unpark guarantees the token is delivered regardless of sleep flag timing
            _nativeHandles[i].Unpark();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        if (_parkers != null)
        {
            for (int i = 0; i < _parkers.Length; i++)
            {
                _parkers[i]?.Dispose();
            }
        }

        if (_nativeHandles != null)
        {
            NativeMemory.Free(_nativeHandles);
        }

        if (_stateBlock != null)
        {
            NativeMemory.AlignedFree(_stateBlock);
        }
    }
}
