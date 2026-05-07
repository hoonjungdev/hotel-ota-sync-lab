using HotelOtaSync.Domain;

namespace HotelOtaSync.Application.Channels;

/// Application-layer use case: pull the latest ARI from a single channel and
/// stash it in IRateSnapshotCache. Owned by Application (not Infrastructure)
/// because it speaks only to ports — the only thing it knows about a "channel"
/// is the IChannelClient interface.
///
/// W4 callers: admin endpoint POST /admin/channels/{channel}/refresh.
/// W5 callers: a BackgroundService cron job will invoke this on a schedule.
public sealed class ChannelRateRefresher
{
    private readonly IReadOnlyDictionary<ChannelCode, IChannelClient> _byChannel;
    private readonly IRateSnapshotCache _cache;
    private readonly TimeSpan _ttl;

    public ChannelRateRefresher(
        IEnumerable<IChannelClient> channels,
        IRateSnapshotCache cache,
        TimeSpan? ttl = null)
    {
        _byChannel = channels.ToDictionary(c => c.Channel);
        _cache = cache;
        _ttl = ttl ?? TimeSpan.FromMinutes(5);
    }

    /// Returns the number of RatePoints written to the cache. Throws
    /// <see cref="ChannelException"/> on adapter failure (transport, OTA error
    /// payload, retries exhausted, …) so callers can surface partial-fleet
    /// outages without losing the channel-specific failure kind.
    public async Task<int> RefreshAsync(
        ChannelCode channel,
        string hotelCode,
        DateRange stay,
        CancellationToken cancellationToken)
    {
        if (!_byChannel.TryGetValue(channel, out var client))
        {
            throw new ChannelException(channel, ChannelFailureKind.BadRequest,
                $"No IChannelClient registered for channel '{channel}'.");
        }

        var points = await client.PullAvailabilityAsync(
            new AvailabilityQuery(hotelCode, stay), cancellationToken);

        if (points.Count == 0) return 0;

        await _cache.SetAsync(channel, hotelCode, points, _ttl, cancellationToken);
        return points.Count;
    }
}
