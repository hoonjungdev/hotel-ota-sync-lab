namespace HotelOtaSync.Infrastructure.Channels.BlueWave;

/// Strongly typed configuration for the BlueWave adapter. Values that affect
/// the resilience pipeline (timeouts, retry count) are surfaced here so they
/// can be tuned per-environment via appsettings without touching the
/// adapter code.
public sealed class BlueWaveOptions
{
    public const string SectionName = "Channels:BlueWave";

    /// Required. Validated at startup by AddBlueWaveChannel — a missing
    /// `Channels:BlueWave:BaseUrl` fails fast instead of silently falling
    /// back to a localhost default.
    public Uri? BaseUrl { get; set; }

    /// Per-attempt HTTP timeout. The retry strategy schedules another attempt
    /// when this fires, so keep it well below TotalRequestTimeout.
    public TimeSpan PerAttemptTimeout { get; set; } = TimeSpan.FromSeconds(2);

    /// Hard ceiling on a single PullAvailabilityAsync / PushReservationAsync
    /// call, retries included. Maps onto HttpClient.Timeout.
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(8);

    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// Circuit-breaker tuning. The defaults are deliberately permissive so
    /// transient mock-injected 5xxs in dev don't trip the breaker; tighten
    /// in production via configuration.
    public double CircuitFailureRatio { get; set; } = 0.5;
    public int CircuitMinimumThroughput { get; set; } = 8;
    public TimeSpan CircuitSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CircuitBreakDuration { get; set; } = TimeSpan.FromSeconds(15);
}
