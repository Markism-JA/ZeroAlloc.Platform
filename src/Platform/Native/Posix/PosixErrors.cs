namespace Platform.Native.Posix;

/// <summary>
/// Standard POSIX errno constants.
/// </summary>
/// <remarks>See: <see href="https://man7.org/linux/man-pages/man3/errno.3.html"/></remarks>
internal static class PosixErrors
{
    /// <summary>Operation not permitted.</summary>
    public const int EPERM = 1;

    /// <summary>No such file or directory.</summary>
    public const int ENOENT = 2;

    /// <summary>No such process.</summary>
    public const int ESRCH = 3;

    /// <summary>Permission denied.</summary>
    public const int EACCES = 13;

    /// <summary>File exists.</summary>
    public const int EEXIST = 17;

    /// <summary>Invalid argument.</summary>
    public const int EINVAL = 22;
}
