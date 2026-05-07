namespace HotelOtaSync.Application.Channels;

/// Capability flags an adapter advertises so use-cases can gate behaviour
/// without sprinkling channel names through Application code.
///
/// Keep flags minimal — only add a flag when a real OTA difference forces a
/// branch in calling code (the canonical case study being GreenLeaf's lack of
/// multi-room atomicity, see ADR-W3).
public sealed record ChannelCapabilities
{
    /// Adapter implements <see cref="IChannelClient.PullAvailabilityAsync"/>.
    public required bool SupportsAvailabilityPull { get; init; }

    /// Adapter implements <see cref="IChannelClient.PushReservationAsync"/>.
    public required bool SupportsReservationPush { get; init; }

    /// OTA either holds rooms across multiple line items in one request or
    /// rolls back on partial failure. When false, callers must serialise
    /// per-room pushes and deal with partial-success themselves.
    public required bool SupportsAtomicMultiRoom { get; init; }
}
