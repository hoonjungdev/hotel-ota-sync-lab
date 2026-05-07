namespace HotelOtaSync.Infrastructure.Cache;

public sealed class RedisRateCacheOptions
{
    public const string SectionName = "Cache:Redis";

    /// StackExchange.Redis connection string, e.g. "localhost:6379".
    /// Validated at startup by AddRedisRateCache.
    public string? ConnectionString { get; set; }

    /// TTL applied to each per-(channel, hotel) hash key after a refresh.
    /// Defaults to 5 minutes — short enough that a stalled worker is visible
    /// (cache cliffs to empty) without making the cache useless on a tight
    /// pull cadence.
    public TimeSpan SnapshotTtl { get; set; } = TimeSpan.FromMinutes(5);
}
