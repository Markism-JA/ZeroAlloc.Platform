using System.Runtime.CompilerServices;

namespace Platform.Benchmarking.Harness.Orchestration;

/// <summary>
/// Pre-warms JIT compilation, native code generation, and CPU instruction/data cache lines
/// across single-producer single-consumer pipelines prior to timed measurement windows.
/// </summary>
public static class SpscWarmupDriver
{
    public delegate bool TryEnqueueDelegate<T>(in T item);
    public delegate bool TryDequeueDelegate<T>(out T item);

    /// <summary>
    /// Executes a warm-up cycle over an arbitrary queue/pipeline, asserting that
    /// the pipeline is functional and steady-state cache lines are primed.
    /// </summary>
    /// <typeparam name="T">Payload struct type.</typeparam>
    /// <param name="enqueue">Non-blocking enqueue delegate or method pointer wrapper.</param>
    /// <param name="dequeue">Non-blocking dequeue delegate or method pointer wrapper.</param>
    /// <param name="sample">Prototype item to cycle through.</param>
    /// <param name="warmupIterations">Number of items to cycle (default 10,000).</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void WarmupPipeline<T>(
        TryEnqueueDelegate<T> enqueue,
        TryDequeueDelegate<T> dequeue,
        in T sample,
        int warmupIterations = 10_000
    )
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(enqueue);
        ArgumentNullException.ThrowIfNull(dequeue);

        for (int i = 0; i < warmupIterations; i++)
        {
            while (!enqueue(in sample))
            {
                Thread.SpinWait(2);
            }

            while (!dequeue(out _))
            {
                Thread.SpinWait(2);
            }
        }
    }

    /// <summary>
    /// Pure unsafe method pointer variant for maximum zero-overhead priming.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe void WarmupPipelineNative<T>(
        delegate* <T*, void*, bool> tryEnqueue,
        delegate* <T*, void*, bool> tryDequeue,
        void* queueContext,
        in T sample,
        int warmupIterations = 10_000
    )
        where T : unmanaged
    {
        fixed (T* pSample = &sample)
        {
            T dummy = default;
            for (int i = 0; i < warmupIterations; i++)
            {
                while (!tryEnqueue(pSample, queueContext))
                {
                    Thread.SpinWait(2);
                }

                while (!tryDequeue(&dummy, queueContext))
                {
                    Thread.SpinWait(2);
                }
            }
        }
    }
}
