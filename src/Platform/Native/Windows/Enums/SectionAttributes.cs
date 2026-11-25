namespace Platform.Native.Windows.Enums;

/// <summary>
/// Section allocation attributes for CreateFileMapping.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createfilemappingw"/></remarks>
[Flags]
internal enum SectionAttributes : uint
{
    /// <summary>Allocates physical storage in memory or in the paging file for all pages.</summary>
    Commit = 0x08000000,

    /// <summary>Reserves a range of the process's virtual address space without allocating physical storage.</summary>
    Reserve = 0x04000000,
}
