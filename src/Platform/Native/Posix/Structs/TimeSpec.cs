using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Platform.Native.Posix.Structs;

[StructLayout(LayoutKind.Sequential)]
public struct Timespec
{
    public nint TvSec;
    public nint TvNsec;

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
