namespace Platform.Native.Windows.Enums;

/// <summary>
/// Standard device identifiers for <see cref="Kernel32.GetStdHandle"/>.
/// </summary>
/// <remarks>
/// See: <see href="https://learn.microsoft.com/en-us/windows/console/getstdhandle"/>
/// </remarks>
internal enum StandardHandle : int
{
    /// <summary>The standard input device (<c>STD_INPUT_HANDLE</c>).</summary>
    Input = -10,

    /// <summary>The standard output device (<c>STD_OUTPUT_HANDLE</c>).</summary>
    Output = -11,

    /// <summary>The standard error device (<c>STD_ERROR_HANDLE</c>).</summary>
    Error = -12,
}
