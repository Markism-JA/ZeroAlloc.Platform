using System.Runtime.InteropServices;

namespace Platform.CorePinning;

/// <summary>
/// Configuration describing whether and how a worker thread should be pinned to a logical CPU core.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AffinityConfig
{
    /// <summary>Core pinning strategy.</summary>
    public AffinityMode Mode;

    /// <summary>Zero-based target logical CPU core index (0..63) when <see cref="Mode"/> is <see cref="AffinityMode.DedicatedCore"/>.</summary>
    public int TargetCore;

    /// <summary>Returns a disabled affinity configuration.</summary>
    public static AffinityConfig None => new() { Mode = AffinityMode.Disabled };
}
