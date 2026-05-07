namespace HotelOtaSync.Application.Channels;

/// Output port for a single OTA partner. One implementation per channel lives
/// in HotelOtaSync.Infrastructure (BlueWaveClient, SkyTripClient, …).
///
/// Adapters are expected to:
///   - translate channel-specific wire formats (XML/JSON) to the records here,
///   - surface transport failures as <see cref="ChannelException"/>,
///   - leave retry/circuit-breaker decisions to the resilience pipeline they
///     are wrapped in (Polly), not to per-adapter ad-hoc try/catch.
public interface IChannelClient
{
    ChannelCode Channel { get; }
    ChannelCapabilities Capabilities { get; }

    Task<IReadOnlyList<RatePoint>> PullAvailabilityAsync(
        AvailabilityQuery query,
        CancellationToken cancellationToken);

    Task<ReservationPushAck> PushReservationAsync(
        ReservationPush push,
        CancellationToken cancellationToken);
}
