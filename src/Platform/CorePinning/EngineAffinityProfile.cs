using System.Runtime.InteropServices;

namespace Platform.CorePinning;

/// <summary>
/// Engine-wide CPU core pinning profile defining the base core offset for pinned worker pools.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct EngineAffinityProfile
{
    /// <summary>Core pinning strategy for engine worker threads.</summary>
    public AffinityMode Mode;

    /// <summary>Starting logical CPU core index from which pinned workers are assigned.</summary>
    public int BaseCoreOffset;

    /// <summary>Default development profile with core pinning disabled.</summary>
    public static EngineAffinityProfile MonolithDev =>
        new() { Mode = AffinityMode.Disabled, BaseCoreOffset = 0 };
}
