namespace Platform.Native.Windows.Enums;

/// <summary>
/// Memory protection options for allocated or mapped virtual pages.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/memory/memory-protection-constants"/></remarks>
[Flags]
internal enum PageProtection : uint
{
    /// <summary>Disables all access to the committed region of pages.</summary>
    NoAccess = 0x01,

    /// <summary>Enables read-only access to the committed region of pages.</summary>
    ReadOnly = 0x02,

    /// <summary>Enables read-only or read/write access to the committed region of pages.</summary>
    ReadWrite = 0x04,

    /// <summary>Enables read-only or copy-on-write access to mapped views of a file mapping service.</summary>
    WriteCopy = 0x08,

    /// <summary>Enables execute access to the committed region of pages.</summary>
    Execute = 0x10,

    /// <summary>Enables execute or read-only access to the committed region of pages.</summary>
    ExecuteRead = 0x20,

    /// <summary>Enables execute, read-only, or read/write access to the committed region of pages.</summary>
    ExecuteReadWrite = 0x40,
}
