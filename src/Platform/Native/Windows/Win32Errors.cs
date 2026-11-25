namespace Platform.Native.Windows;

/// <summary>
/// Common Win32 system error codes.
/// </summary>
/// <remarks>See: <see href="https://learn.microsoft.com/en-us/windows/win32/debug/system-error-codes"/></remarks>
internal static class Win32Errors
{
    /// <summary>The operation completed successfully.</summary>
    public const uint ERROR_SUCCESS = 0;

    /// <summary>The system cannot find the file specified.</summary>
    public const uint ERROR_FILE_NOT_FOUND = 2;

    /// <summary>The system cannot find the path specified.</summary>
    public const uint ERROR_PATH_NOT_FOUND = 3;

    /// <summary>Access is denied.</summary>
    public const uint ERROR_ACCESS_DENIED = 5;

    /// <summary>The handle is invalid.</summary>
    public const uint ERROR_INVALID_HANDLE = 6;

    /// <summary>Not enough memory resources are available to process this command.</summary>
    public const uint ERROR_NOT_ENOUGH_MEMORY = 8;

    /// <summary>The process cannot access the file because it is being used by another process.</summary>
    public const uint ERROR_SHARING_VIOLATION = 32;

    /// <summary>The process cannot access the file because another process has locked a portion of the file.</summary>
    public const uint ERROR_LOCK_VIOLATION = 33;

    /// <summary>The file exists. (Returned by CreateFile with CREATE_NEW)</summary>
    public const uint ERROR_FILE_EXISTS = 80;

    /// <summary>The parameter is incorrect.</summary>
    public const uint ERROR_INVALID_PARAMETER = 87;

    /// <summary>Cannot create a file or kernel object when that file or object already exists.</summary>
    public const uint ERROR_ALREADY_EXISTS = 183;

    /// <summary>The requested operation cannot be performed on a file with a user-mapped section open.</summary>
    public const uint ERROR_USER_MAPPED_FILE = 1224;
}