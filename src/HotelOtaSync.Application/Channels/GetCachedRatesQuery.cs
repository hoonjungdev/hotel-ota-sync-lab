using HotelOtaSync.Domain;

namespace HotelOtaSync.Application.Channels;

/// Application-layer read use case for the user-facing rate query. Iterates
/// every registered IChannelClient (deduplicated by ChannelCode) and looks
/// each one up in IRateSnapshotCache concurrently. Always emits an entry per
/// registered channel — clients can rely on the response keys reflecting the
/// configured channel set, with empty arrays signalling "configured but no
/// cache data" (vs missing keys signalling "not configured at all").
///
/// Symmetry with ChannelRateRefresher (Part 1): write side pulls from one
/// adapter into the cache; this read side fans out from the cache across
/// every adapter the host knows about.
public sealed class GetCachedRatesQuery
{
    private readonly IReadOnlyList<ChannelCode> _channelCodes;
    private readonly IRateSnapshotCache _cache;

    public GetCachedRatesQuery(IEnumerable<IChannelClient> channels, IRateSnapshotCache cache)
    {
        _channelCodes = channels.Select(c => c.Channel).Distinct().ToList();
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<ChannelCode, IReadOnlyList<RatePoint>>> ExecuteAsync(
        string hotelCode,
        DateRange stay,
        CancellationToken cancellationToken)
    {
        var tasks = _channelCodes
            .Select(code => (code, task: _cache.GetAsync(code, hotelCode, stay, cancellationToken)))
            .ToArray();

        await Task.WhenAll(tasks.Select(t => t.task)).ConfigureAwait(false);

        var result = new Dictionary<ChannelCode, IReadOnlyList<RatePoint>>(_channelCodes.Count);
        foreach (var (code, task) in tasks)
        {
            result[code] = await task.ConfigureAwait(false);
        }
        return result;
    }
}
