using System.Runtime.InteropServices;
using Platform.Native.Windows;
using Platform.Native.Windows.Enums;

namespace Platform.Timer;

public class HighResolutionTimerScope : IDisposable
{
    private readonly bool _success;
    private readonly uint _periodMs;

    public HighResolutionTimerScope(uint periodMs)
    {
        _periodMs = periodMs;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MmResult result = Winmm.TimeBeginPeriod(periodMs);
            _success = result == MmResult.NoError;
        }
        else
        {
            _success = false;
        }
    }

    public void Dispose()
    {
        if (_success && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Winmm.TimeEndPeriod(_periodMs);
        }
    }
}
