using System.Runtime.InteropServices;
using Platform.Native.Windows.Enums;

namespace Platform.Native.Windows;

internal static partial class Winmm
{
    private const string LibName = "winmm.dll";

    /// <summary>
    /// Requests a minimum resolution for periodic timers.
    /// </summary>
    /// <param name="uMilliseconds">Minimum timer resolution, in milliseconds, for the application.</param>
    /// <returns>Returns <see cref="MmResult.NoError"/> if successful or <see cref="MmResult.NoCanDo"/> if the resolution is out of range.</returns>
    /// <remarks>
    /// See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timebeginperiod"/>
    /// </remarks>
    [LibraryImport(LibName, EntryPoint = "timeBeginPeriod")]
    public static partial MmResult TimeBeginPeriod(uint uMilliseconds);

    /// <summary>
    /// Clears a previously set minimum timer resolution.
    /// </summary>
    /// <param name="uMilliseconds">Minimum timer resolution specified in the previous call to <see cref="TimeBeginPeriod"/>.</param>
    /// <returns>Returns <see cref="MmResult.NoError"/> if successful or <see cref="MmResult.NoCanDo"/> if the resolution is out of range.</returns>
    /// <remarks>
    /// See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/timeapi/nf-timeapi-timeendperiod"/>
    /// </remarks>
    [LibraryImport(LibName, EntryPoint = "timeEndPeriod")]
    public static partial MmResult TimeEndPeriod(uint uMilliseconds);
}
