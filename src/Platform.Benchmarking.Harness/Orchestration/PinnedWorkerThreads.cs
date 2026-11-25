using System.Runtime.CompilerServices;
using Platform.Threading;

namespace Platform.Benchmarking.Harness.Orchestration;

/// <summary>
/// Encapsulates a dedicated hardware-pinned OS thread running at highest priority,
/// driven by a low-level native <see cref="ThreadParker"/> for deterministic sleep/wake transitions.
/// </summary>
public sealed class PinnedWorkerThread : IDisposable
{
    private readonly Thread _thread;
    private readonly ThreadParker _parker;
    private readonly CancellationTokenSource _cts;
    private int _isDisposed;

    public string Name { get; }
    public int CoreIndex { get; }
    public ThreadParker Parker => _parker;
    public bool IsAlive => _thread.IsAlive;

    private PinnedWorkerThread(string name, int coreIndex, Action<PinnedWorkerContext> workLoop)
    {
        Name = name;
        CoreIndex = coreIndex;
        _parker = new ThreadParker();
        _cts = new CancellationTokenSource();

        _thread = new Thread(() =>
        {
            ThreadAffinityUtility.PinCurrentThreadToCore(coreIndex);

            var context = new PinnedWorkerContext(Name, CoreIndex, _parker, _cts.Token);
            workLoop(context);
        })
        {
            Name = name,
            IsBackground = true,
            Priority = ThreadPriority.Highest,
        };

        _thread.Start();
    }

    /// <summary>
    /// Spawns and hardware-pins a new OS thread to a dedicated core index.
    /// </summary>
    public static PinnedWorkerThread Spawn(
        string name,
        int coreIndex,
        Action<PinnedWorkerContext> workLoop
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(workLoop);

        return new PinnedWorkerThread(name, coreIndex, workLoop);
    }

    /// <summary>
    /// Signals the embedded thread parker, transitioning the worker thread from BLOCKED to RUNNABLE.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unpark() => _parker.Unpark();

    /// <summary>
    /// Waits for the worker thread to exit on its own within the specified timeout.
    /// Does NOT trigger cancellation.
    /// </summary>
    public bool WaitForCompletion(TimeSpan timeout)
    {
        return _thread.Join(timeout);
    }

    /// <summary>
    /// Requests cancellation, unparks the thread if parked, and waits for execution to terminate.
    /// </summary>
    public void Join(TimeSpan timeout)
    {
        _cts.Cancel();
        _parker.Unpark();
        _thread.Join(timeout);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        _cts.Cancel();
        _parker.Unpark();

        if (_thread.IsAlive)
        {
            _thread.Join(TimeSpan.FromSeconds(2));
        }

        _cts.Dispose();
        _parker.Dispose();
    }
}

/// <summary>
/// Operational context passed directly into a <see cref="PinnedWorkerThread"/> execution loop.
/// </summary>
public readonly struct PinnedWorkerContext(
    string name,
    int coreIndex,
    ThreadParker parker,
    CancellationToken cancellationToken
)
{
    public string Name => name;
    public int CoreIndex => coreIndex;
    public ThreadParker Parker => parker;
    public CancellationToken CancellationToken => cancellationToken;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ShouldContinue() => !cancellationToken.IsCancellationRequested;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Park(int timeoutMs = -1) => parker.Park(timeoutMs);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unpark() => parker.Unpark();
}
