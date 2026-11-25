namespace Platform.Native.Windows;

/// <summary>
/// NTSTATUS return codes from ntdll native system routines.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-erref/596a1078-e883-4972-9bbc-49e60bebca55"/></remarks>
internal static class NtStatus
{
    /// <summary>The operation completed successfully.</summary>
    public const int STATUS_SUCCESS = 0x00000000;

    /// <summary>More entries are available from the directory query.</summary>
    public const int STATUS_MORE_ENTRIES = 0x00000105;

    /// <summary>No more entries are found in the directory.</summary>
    public const int STATUS_NO_MORE_ENTRIES = unchecked((int)0x8000001A);
}
