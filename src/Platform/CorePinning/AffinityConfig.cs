using System.Runtime.InteropServices;

namespace Platform.CorePinning;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AffinityConfig
{
    public AffinityMode Mode;
    public int TargetCore;

    public static AffinityConfig None => new() { Mode = AffinityMode.Disabled };
}
