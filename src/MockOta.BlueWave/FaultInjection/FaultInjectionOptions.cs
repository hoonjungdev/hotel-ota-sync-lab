namespace MockOta.BlueWave.FaultInjection;

/// Bound from the "FaultInjection" section in appsettings.json.
/// Hot-reloadable via IOptionsMonitor — settings.json edits take effect
/// without restarting the mock, which is convenient for live demos.
public sealed class FaultInjectionOptions
{
    /// Master switch. When false, no faults are ever injected regardless of
    /// the other fields.
    public bool Enabled { get; set; }

    /// Probability in [0.0, 1.0] that a request is delayed by LatencyMs.
    public double LatencyProbability { get; set; }

    /// Artificial latency added before the response is written, in ms.
    public int LatencyMs { get; set; }

    /// Probability in [0.0, 1.0] that a request short-circuits with a 5xx
    /// response. Evaluated independently from LatencyProbability.
    public double ServerErrorProbability { get; set; }

    /// HTTP status code to return when a server-error fault fires.
    public int ServerErrorStatusCode { get; set; } = 503;

    /// Optional deterministic seed. When set, the per-request RNG is seeded
    /// from (seed, request-correlation-id) so contract tests can reproduce
    /// the exact pattern of injected faults. Null = nondeterministic.
    public int? Seed { get; set; }
}
