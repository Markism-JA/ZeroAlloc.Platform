using System.Runtime.InteropServices;

namespace Platform.Native.Windows;

internal static unsafe partial class Kernel32
{
    /// <summary>
    /// Writes data synchronously to the specified file or input/output (I/O) device.
    /// </summary>
    /// <param name="hFile">A handle to the file or I/O device.</param>
    /// <param name="lpBuffer">A pointer to the buffer containing the data to be written.</param>
    /// <param name="nNumberOfBytesToWrite">The number of bytes to be written.</param>
    /// <param name="lpNumberOfBytesWritten">A pointer to the variable that receives the number of bytes written.</param>
    /// <param name="lpOverlapped">A pointer to an <c>OVERLAPPED</c> structure, or <c>null</c> for synchronous operations.</param>
    /// <returns><c>true</c> if the function succeeds; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-writefile"/>
    /// </remarks>
    [LibraryImport(LibName, EntryPoint = "WriteFile", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WriteFile(
        nint hFile,
        byte* lpBuffer,
        uint nNumberOfBytesToWrite,
        uint* lpNumberOfBytesWritten,
        void* lpOverlapped
    );
}
