using System.Text.Json;
using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;
using StackExchange.Redis;

namespace HotelOtaSync.Infrastructure.Cache;

/// Redis-backed rate snapshot cache. Layout per (channel, hotel):
///
///   key   = "rates:{channel}:{hotelCode}"   (Hash)
///   field = "yyyy-MM-dd"                    (one per night)
///   value = JSON `RatePoint[]` covering every (room type, rate plan) for
///           that night.
///
/// Rationale:
///   - Hash gives us namespace grouping per (channel, hotel) — refresh and
///     invalidation hit one key, not N.
///   - HMSET / HMGET cover the whole pipeline in one round trip.
///   - StackExchange.Redis does not support per-field TTL, so the TTL is
///     applied to the container key. Acceptable: a refresh either replaces
///     all fields OR the whole key expires together; either way callers get
///     a consistent cliff, not stale half-updated data.
public sealed class RedisRateSnapshotCache : IRateSnapshotCache
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IConnectionMultiplexer _redis;

    public RedisRateSnapshotCache(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<IReadOnlyList<RatePoint>> GetAsync(
        ChannelCode channel,
        string hotelCode,
        DateRange stay,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var key = BuildKey(channel, hotelCode);

        var fields = stay.EachStayDate().Select(FieldFor).ToArray();
        if (fields.Length == 0) return Array.Empty<RatePoint>();

        var values = await db.HashGetAsync(key, Array.ConvertAll(fields, f => (RedisValue)f))
            .ConfigureAwait(false);

        var result = new List<RatePoint>(fields.Length * 4);
        foreach (var v in values)
        {
            if (v.IsNullOrEmpty) continue;
            var bucket = JsonSerializer.Deserialize<List<RatePoint>>((string)v!, JsonOpts);
            if (bucket is { Count: > 0 }) result.AddRange(bucket);
        }
        return result;
    }

    public async Task SetAsync(
        ChannelCode channel,
        string hotelCode,
        IReadOnlyList<RatePoint> points,
        TimeSpan ttl,
        CancellationToken cancellationToken)
    {
        if (points.Count == 0) return;

        var db = _redis.GetDatabase();
        var key = BuildKey(channel, hotelCode);

        var byDay = points
            .GroupBy(p => p.StayDate)
            .Select(g => new HashEntry(FieldFor(g.Key), JsonSerializer.Serialize(g.ToList(), JsonOpts)))
            .ToArray();

        // Atomic per-(channel, hotel): HSET batch + EXPIRE in one transaction
        // so a reader cannot observe a half-written snapshot.
        var tx = db.CreateTransaction();
        _ = tx.HashSetAsync(key, byDay);
        _ = tx.KeyExpireAsync(key, ttl);
        await tx.ExecuteAsync().ConfigureAwait(false);
    }

    public async Task InvalidateAsync(
        ChannelCode channel,
        string hotelCode,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(BuildKey(channel, hotelCode)).ConfigureAwait(false);
    }

    internal static string BuildKey(ChannelCode channel, string hotelCode) =>
        $"rates:{channel.Value}:{hotelCode}";

    internal static string FieldFor(DateOnly date) => date.ToString("yyyy-MM-dd");
}
