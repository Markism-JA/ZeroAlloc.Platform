using System.Runtime.InteropServices;

namespace Platform.Native.Windows.Structs;

/// <summary>
/// Specifies attributes applied to objects or object handles by NT system services.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/ntdef/ns-ntdef-_object_attributes"/></remarks>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ObjectAttributes
{
    public int Length;
    public nint RootDirectory;
    public UnicodeString* ObjectName;
    public uint Attributes;
    public nint SecurityDescriptor;
    public nint SecurityQualityOfService;
}
