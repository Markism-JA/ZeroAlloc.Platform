using System.Runtime.InteropServices;

namespace Platform.Native.Windows.Structs;

/// <summary>
/// Directory entry layout returned by NtQueryDirectoryObject.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ObjectDirectoryInformation
{
    public UnicodeString Name;
    public UnicodeString TypeName;
}
