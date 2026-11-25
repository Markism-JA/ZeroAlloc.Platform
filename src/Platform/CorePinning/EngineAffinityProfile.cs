using System.Runtime.InteropServices;

namespace Platform.CorePinning;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EngineAffinityProfile
{
    public AffinityMode Mode;
    public int BaseCoreOffset;
    public static EngineAffinityProfile MonolithDev =>
        new() { Mode = AffinityMode.Disabled, BaseCoreOffset = 0 };
}
