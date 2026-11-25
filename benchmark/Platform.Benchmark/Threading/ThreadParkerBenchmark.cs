using BenchmarkDotNet.Attributes;
using Platform.Threading;

namespace Platform.Benchmark.Threading;

[MemoryDiagnoser]
public class ThreadParkerBenchmark
{
    private const int BatchSize = 64;

    private ThreadParker _nativeParker = null!;
    private NativeThreadParkerHandle _nativeHandle;
    private AutoResetEvent _bclEvent = null!;

    // Persistent ping-pong worker for real cross-thread wake latency
    private ThreadParker _pingNative = null!;
    private ThreadParker _pongNative = null!;
    private Thread _nativeWorker = null!;

    private AutoResetEvent _pingBcl = null!;
    private AutoResetEvent _pongBcl = null!;
    private Thread _bclWorker = null!;

    private volatile bool _stop;

    [GlobalSetup]
    public void Setup()
    {
        _nativeParker = new ThreadParker();
        _nativeHandle = _nativeParker.NativeHandle;
        _bclEvent = new AutoResetEvent(false);

        _stop = false;

        _pingNative = new ThreadParker();
        _pongNative = new ThreadParker();
        _nativeWorker = new Thread(() =>
        {
            while (!_stop)
            {
                if (_pingNative.TryArm())
                {
                    if (!_pingNative.Park(100))
                        continue;
                }
                if (_stop)
                    break;
                _pongNative.Unpark();
            }
        })
        {
            IsBackground = true,
            Name = "Bench_NativeParkerWorker",
        };
        _nativeWorker.Start();

        _pingBcl = new AutoResetEvent(false);
        _pongBcl = new AutoResetEvent(false);
        _bclWorker = new Thread(() =>
        {
            while (!_stop)
            {
                if (!_pingBcl.WaitOne(100))
                    continue;
                if (_stop)
                    break;
                _pongBcl.Set();
            }
        })
        {
            IsBackground = true,
            Name = "Bench_BclEventWorker",
        };
        _bclWorker.Start();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _stop = true;
        _pingNative.Unpark();
        _pongNative.Unpark();
        _pingBcl.Set();
        _pongBcl.Set();

        _nativeWorker.Join(TimeSpan.FromSeconds(2));
        _bclWorker.Join(TimeSpan.FromSeconds(2));

        _nativeParker.Dispose();
        _bclEvent.Dispose();
        _pingNative.Dispose();
        _pongNative.Dispose();
        _pingBcl.Dispose();
        _pongBcl.Dispose();
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = BatchSize)]
    public int Bcl_AutoResetEvent_Set_Uncontended()
    {
        int count = 0;
        for (int i = 0; i < BatchSize; i++)
        {
            if (_bclEvent.Set())
                count++;
        }
        return count;
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public void ThreadParker_Unpark_ElidedWhileRunning()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            _nativeHandle.Unpark();
        }
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public int ThreadParker_TryArm_Disarm()
    {
        int armed = 0;
        for (int i = 0; i < BatchSize; i++)
        {
            if (_nativeParker.TryArm())
            {
                _nativeParker.Disarm();
                armed++;
            }
        }
        return armed;
    }

    [Benchmark]
    public bool Bcl_AutoResetEvent_PingPongRoundTrip()
    {
        _pingBcl.Set();
        return _pongBcl.WaitOne(1000);
    }

    [Benchmark]
    public bool ThreadParker_PingPongRoundTrip()
    {
        _pingNative.Unpark();
        if (_pongNative.TryArm())
        {
            return _pongNative.Park(1000);
        }
        return true;
    }
}
