using System.Runtime.InteropServices;
using Platform.Native.Windows;
using Platform.Native.Windows.Enums;

namespace Platform.Timer;

/// <summary>
/// RAII scope that raises the Windows multimedia timer resolution via <c>timeBeginPeriod</c>
/// and restores it on <see cref="Dispose"/>. Acts as a safe no-op on Linux and macOS.
/// </summary>
public sealed class HighResolutionTimerScope : IDisposable
{
    private readonly bool _success;
    private readonly uint _periodMs;
    private int _disposed;

    /// <summary>
    /// True while the elevated Windows timer period is actively held by this scope; always false on POSIX.
    /// </summary>
    public bool IsActive => _success && Volatile.Read(ref _disposed) == 0;

    /// <summary>
    /// Requests the specified timer resolution in milliseconds (typically <c>1</c>) on Windows.
    /// </summary>
    /// <param name="periodMs">Requested minimum timer resolution in milliseconds.</param>
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

    /// <summary>
    /// Releases the Windows timer period request if active. Safe to call multiple times.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        if (_success && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Winmm.TimeEndPeriod(_periodMs);
        }
    }
}
