using System.Runtime.InteropServices;

namespace Platform.Native.Windows;

internal static partial class Kernel32
{
    /// <summary>
    /// Retrieves a pseudo handle for the current process.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentprocess"/></remarks>
    [LibraryImport(LibName, EntryPoint = "GetCurrentProcess")]
    public static partial nint GetCurrentProcess();

    /// <summary>
    /// Opens an existing local process object.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess"/></remarks>
    [LibraryImport(LibName, EntryPoint = "OpenProcess", SetLastError = true)]
    public static partial nint OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int dwProcessId
    );

    /// <summary>
    /// Retrieves the termination status of the specified process.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getexitcodeprocess"/></remarks>
    [LibraryImport(LibName, EntryPoint = "GetExitCodeProcess", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetExitCodeProcess(nint hProcess, out uint lpExitCode);
}
