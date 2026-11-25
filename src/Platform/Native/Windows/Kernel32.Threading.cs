using System.Runtime.InteropServices;

namespace Platform.Native.Windows;

internal static unsafe partial class Kernel32
{
    public const uint WaitObject0 = 0x0000_0000;
    public const uint WaitFailed = 0xFFFF_FFFF;

    /// <summary>
    /// Retrieves a pseudo handle for the calling thread.
    /// </summary>
    /// <remarks>
    /// See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentthread"/>
    /// </remarks>
    [LibraryImport(LibName, EntryPoint = "GetCurrentThread")]
    public static partial nint GetCurrentThread();

    /// <summary>
    /// Sets a processor affinity mask for the specified thread.
    /// </summary>
    /// <param name="hThread">A handle to the thread whose affinity mask is to be set.</param>
    /// <param name="dwThreadAffinityMask">The affinity mask for the thread.</param>
    /// <returns>
    /// The previous thread affinity mask on success; otherwise 0. Call <see cref="Marshal.GetLastPInvokeError"/> for failure details.
    /// </returns>
    /// <remarks>
    /// See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadaffinitymask"/>
    /// </remarks>
    [LibraryImport(LibName, EntryPoint = "SetThreadAffinityMask", SetLastError = true)]
    public static partial nuint SetThreadAffinityMask(nint hThread, nuint dwThreadAffinityMask);

    /// <summary>
    /// Creates or opens a named or unnamed event object.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-createeventw"/></remarks>
    [LibraryImport(
        LibName,
        EntryPoint = "CreateEventW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16
    )]
    public static unsafe partial nint CreateEventW(
        void* lpEventAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
        [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
        string? lpName
    );

    /// <summary>
    /// Sets the specified event object to the signaled state.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-setevent"/></remarks>
    [LibraryImport(LibName, EntryPoint = "SetEvent", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetEvent(nint hEvent);

    /// <summary>
    /// Sets the specified event object to the nonsignaled state.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-resetevent"/></remarks>
    [LibraryImport(LibName, EntryPoint = "ResetEvent", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ResetEvent(nint hEvent);

    /// <summary>
    /// Waits until the specified object is in the signaled state or the time-out interval elapses.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/synchapi/nf-synchapi-waitforsingleobject"/></remarks>
    [LibraryImport(LibName, EntryPoint = "WaitForSingleObject", SetLastError = true)]
    public static partial uint WaitForSingleObject(nint hHandle, uint dwMilliseconds);
}
