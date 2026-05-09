using HotelOtaSync.Domain;

namespace HotelOtaSync.Application.Channels;

/// Caches the rate snapshot a channel adapter pulled, so user-facing rate
/// queries never hit the OTA. One implementation per cache backend lives in
/// HotelOtaSync.Infrastructure (currently RedisRateSnapshotCache).
///
/// Layout the implementation must honour: per-channel partitioning so a
/// refresh for BlueWave does not stomp on SkyTrip data, and per-stay-date
/// granularity so a single GET can serve any sub-range without re-pulling.
public interface IRateSnapshotCache
{
    /// Returns every cached RatePoint for the (channel, hotel) tuple whose
    /// StayDate falls inside `stay` (half-open, [Start, End)). Missing days
    /// are simply absent from the result — callers decide whether to treat
    /// that as a partial hit or a miss.
    Task<IReadOnlyList<RatePoint>> GetAsync(
        ChannelCode channel,
        string hotelCode,
        DateRange stay,
        CancellationToken cancellationToken);

    /// Atomically replaces the cached entries for every StayDate present in
    /// `points` (per-day granularity — days NOT present in `points` are left
    /// alone). `ttl` is applied to the per-(channel, hotel) container key.
    Task SetAsync(
        ChannelCode channel,
        string hotelCode,
        IReadOnlyList<RatePoint> points,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    /// Drops every cached day for the (channel, hotel). Used by admin tools
    /// and tests; not part of the hot path.
    Task InvalidateAsync(
        ChannelCode channel,
        string hotelCode,
        CancellationToken cancellationToken);
}
