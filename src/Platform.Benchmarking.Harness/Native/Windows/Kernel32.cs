using System.Runtime.InteropServices;

internal static partial class Kernel32
{
    public const string LibName = "kernel32.dll";

    [LibraryImport(LibName, EntryPoint = "LocalFree", SetLastError = true)]
    public static partial uint LocalFree(nint hMem);
}
