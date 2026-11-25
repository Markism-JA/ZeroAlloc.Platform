namespace Platform.Benchmarking.Harness.Pacing;

public enum RateLimiterMode : byte
{
    /// <summary>
    /// Pushes at absolute hardware line rate (tight loop, no artificial delay).
    /// </summary>
    Saturated = 0,

    /// <summary>
    /// Paced constant-rate dispatch to prevent coordinated omission.
    /// </summary>
    Paced = 1,
}
