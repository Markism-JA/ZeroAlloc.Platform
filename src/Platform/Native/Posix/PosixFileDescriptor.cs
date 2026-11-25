namespace Platform.Native.Posix;

/// <summary>
/// Standard POSIX stream file descriptors.
/// </summary>
/// <remarks>
/// See: <see href="https://man7.org/linux/man-pages/man3/stdin.3.html"/>
/// </remarks>
internal static class PosixFileDescriptors
{
    /// <summary>Standard input descriptor (<c>STDIN_FILENO</c>).</summary>
    public const int Stdin = 0;

    /// <summary>Standard output descriptor (<c>STDOUT_FILENO</c>).</summary>
    public const int Stdout = 1;

    /// <summary>Standard error descriptor (<c>STDERR_FILENO</c>).</summary>
    public const int Stderr = 2;
}
