using System.Runtime.InteropServices;

internal static partial class Powrprof
{
    public const string LibName = "Powrprof.dll";

    [LibraryImport(LibName, EntryPoint = "PowerGetActiveScheme", SetLastError = true)]
    public static partial uint PowerGetActiveScheme(
        nint UserRootPowerKey,
        out nint ActivePolicyGuid
    );
}
