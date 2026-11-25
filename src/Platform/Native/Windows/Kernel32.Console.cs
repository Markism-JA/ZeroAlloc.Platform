using System.Runtime.InteropServices;
using Platform.Native.Windows.Enums;

namespace Platform.Native.Windows;

internal static partial class Kernel32
{
    /// <summary>
    /// Retrieves a handle to the specified standard device (standard input, standard output, or standard error).
    /// </summary>
    /// <param name="nStdHandle">The standard device identifier.</param>
    /// <returns>
    /// If successful, returns a handle to the specified device. If unsuccessful, returns <see cref="INVALID_HANDLE_VALUE"/>.
    /// </returns>
    /// <remarks>
    /// See: <see href="https://learn.microsoft.com/en-us/windows/console/getstdhandle"/>
    /// </remarks>
#if NET7_0_OR_GREATER
    [LibraryImport(LibName, EntryPoint = "GetStdHandle", SetLastError = true)]
    public static partial nint GetStdHandle(StandardHandle nStdHandle);
#else
    [DllImport(LibName, EntryPoint = "GetStdHandle", SetLastError = true)]
    public static extern nint GetStdHandle(StandardHandle nStdHandle);
#endif
}
