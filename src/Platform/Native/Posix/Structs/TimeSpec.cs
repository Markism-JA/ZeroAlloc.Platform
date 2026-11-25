using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Platform.Native.Posix.Structs;

/// <summary>
/// POSIX <c>struct timespec</c> layout (seconds + nanoseconds).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Timespec
{
    /// <summary>Whole seconds component (<c>tv_sec</c>).</summary>
    public nint TvSec;

    /// <summary>Sub-second nanoseconds component (<c>tv_nsec</c>, 0..999,999,999).</summary>
    public nint TvNsec;

    /// <summary>Adds <paramref name="milliseconds"/> to this timestamp, normalizing nanosecond overflow/underflow.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddMilliseconds(int milliseconds)
    {
        long sec = (long)TvSec + (milliseconds / 1000);
        long nsec = TvNsec + (milliseconds % 1000 * 1_000_000L);

        if (nsec >= 1_000_000_000L)
        {
            sec += nsec / 1_000_000_000L;
            nsec %= 1_000_000_000L;
        }
        else if (nsec < 0)
        {
            long borrow = ((-nsec) + 999_999_999L) / 1_000_000_000L;
            sec -= borrow;
            nsec += borrow * 1_000_000_000L;
        }

        TvSec = (nint)sec;
        TvNsec = (nint)nsec;
    }
}
