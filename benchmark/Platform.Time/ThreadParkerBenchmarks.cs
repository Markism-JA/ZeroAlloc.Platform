using BenchmarkDotNet.Attributes;
using Platform.Threading;

namespace Platform.Time;

[MemoryDiagnoser]
public class ThreadParkerBenchmarks
{
    private ThreadParker _nativeParker = null!;
    private NativeThreadParkerHandle _nativeHandle;
    private AutoResetEvent _bclEvent = null!;

    [GlobalSetup]
    public void Setup()
    {
        _nativeParker = new ThreadParker();
        _nativeHandle = _nativeParker.NativeHandle;
        _bclEvent = new AutoResetEvent(false);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _nativeParker.Dispose();
        _bclEvent.Dispose();
    }

    [Benchmark(Baseline = true)]
    public bool Bcl_AutoResetEvent_Set() => _bclEvent.Set();

    [Benchmark]
    public void ThreadParker_Unpark_ElidedWhileRunning()
    {
        // Hot-path producer wake when consumer is actively running (elides kernel syscall)
        _nativeHandle.Unpark();
    }

    [Benchmark]
    public bool ThreadParker_TryArm_Disarm()
    {
        // Uncontended consumer two-phase handshake when queue has work
        bool armed = _nativeParker.TryArm();
        if (armed)
            _nativeParker.Disarm();
        return armed;
    }
}
