namespace Platform.Native.Windows.Enums;

/// <summary>
/// File attributes and flags for CreateFileW.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew"/></remarks>
[Flags]
internal enum FileAttributesFlags : uint
{
    /// <summary>The file does not have other attributes set.</summary>
    Normal = 0x00000080,
}
