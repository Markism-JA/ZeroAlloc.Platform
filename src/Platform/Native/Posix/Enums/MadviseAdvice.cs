namespace Platform.Native.Posix.Enums;

/// <summary>
/// Advice to the kernel about anticipated memory access patterns.
/// </summary>
/// <remarks>See: <see href="https://man7.org/linux/man-pages/man2/madvise.2.html"/></remarks>
internal enum MadviseAdvice : int
{
    /// <summary>No special treatment; default readahead behaviour.</summary>
    Normal = 0,

    /// <summary>Expect page references in random order.</summary>
    Random = 1,

    /// <summary>Expect page references in sequential order.</summary>
    Sequential = 2,

    /// <summary>Expect access in the near future; prefetch physical pages.</summary>
    WillNeed = 3,

    /// <summary>Do not expect access in the near future; release physical backing.</summary>
    DontNeed = 4,

    /// <summary>Enable Transparent Huge Pages (THP) for this range.</summary>
    HugePage = 14,
}
