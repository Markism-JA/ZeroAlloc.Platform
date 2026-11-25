namespace Platform.CorePinning;

/// <summary>
/// Thread-to-core affinity policy.
/// </summary>
public enum AffinityMode : byte
{
    /// <summary>Do not alter OS scheduler thread placement.</summary>
    Disabled,

    /// <summary>Pin the thread exclusively to a single logical CPU core.</summary>
    DedicatedCore,
}
