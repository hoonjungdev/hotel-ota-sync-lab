using HotelOtaSync.Application.Channels;
using HotelOtaSync.Infrastructure.Cache;

namespace HotelOtaSync.UnitTests.Cache;

/// Pin the Redis layout: changing key/field formats invalidates every
/// existing cached snapshot in production, so the format is part of the
/// public contract even though the type is internal.
public class RedisRateSnapshotCacheKeyingTests
{
    [Fact]
    public void BuildKey_FollowsDocumentedLayout()
    {
        var key = RedisRateSnapshotCache.BuildKey(ChannelCode.BlueWave, "HOTEL-1");

        Assert.Equal("rates:BlueWave:HOTEL-1", key);
    }

    [Fact]
    public void BuildKey_PartitionsByChannel()
    {
        var blueKey = RedisRateSnapshotCache.BuildKey(ChannelCode.BlueWave, "HOTEL-1");
        var skyKey = RedisRateSnapshotCache.BuildKey(ChannelCode.SkyTrip, "HOTEL-1");

        Assert.NotEqual(blueKey, skyKey);
    }

    [Fact]
    public void FieldFor_IsIso8601Date()
    {
        Assert.Equal("2026-06-01", RedisRateSnapshotCache.FieldFor(new DateOnly(2026, 6, 1)));
        Assert.Equal("2026-12-31", RedisRateSnapshotCache.FieldFor(new DateOnly(2026, 12, 31)));
    }
}
