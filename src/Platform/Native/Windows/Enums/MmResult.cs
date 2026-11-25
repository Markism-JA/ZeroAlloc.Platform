namespace Platform.Native.Windows.Enums;

/// <summary>
/// Result codes returned by Windows Multimedia (winmm) timer functions.
/// </summary>
/// <remarks>
/// See: <see href="https://learn.microsoft.com/en-us/windows/win32/multimedia/mmsystem-error-returns"/>
/// </remarks>
internal enum MmResult : uint
{
    /// <summary>The operation completed successfully (TIMERR_NOERROR).</summary>
    NoError = 0,

    /// <summary>Unspecified error / requested period is unsupported (TIMERR_NOCANDO).</summary>
    NoCanDo = 97,
}
