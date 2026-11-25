using System.Runtime.InteropServices;

namespace Platform.Native.Windows;

internal static partial class Kernel32
{
    /// <summary>
    /// Retrieves a pseudo handle for the current process.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentprocess"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "GetCurrentProcess")]
    public static partial nint GetCurrentProcess();
#else
    [DllImport(LibName, EntryPoint = "GetCurrentProcess")]
    public static extern nint GetCurrentProcess();
#endif

    /// <summary>
    /// Opens an existing local process object.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocess"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "OpenProcess", SetLastError = true)]
    public static partial nint OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int dwProcessId
    );
#else
    [DllImport(LibName, EntryPoint = "OpenProcess", SetLastError = true)]
    public static extern nint OpenProcess(
        uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int dwProcessId
    );
#endif

    /// <summary>
    /// Retrieves the termination status of the specified process.
    /// </summary>
    /// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getexitcodeprocess"/></remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "GetExitCodeProcess", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetExitCodeProcess(nint hProcess, out uint lpExitCode);
#else
    [DllImport(LibName, EntryPoint = "GetExitCodeProcess", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetExitCodeProcess(nint hProcess, out uint lpExitCode);
#endif
}
