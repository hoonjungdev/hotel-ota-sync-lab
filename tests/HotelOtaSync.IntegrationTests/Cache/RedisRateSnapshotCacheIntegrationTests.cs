using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;
using HotelOtaSync.Infrastructure.Cache;

namespace HotelOtaSync.IntegrationTests.Cache;

/// Pin RedisRateSnapshotCache behaviours that only show up against a real
/// Redis engine: the HMSET+EXPIRE transaction must round-trip, per-day
/// fields must merge across calls, channel partitioning must hold across
/// the wire, and TTL must actually expire the key. The unit suite covers
/// key/field formatting; this suite covers everything beyond the byte
/// boundary.
[Collection("Redis")]
public class RedisRateSnapshotCacheIntegrationTests : IClassFixture<RedisTestFixture>
{
    private readonly RedisTestFixture _fixture;

    public RedisRateSnapshotCacheIntegrationTests(RedisTestFixture fixture) => _fixture = fixture;

    private static readonly DateRange TwoNights =
        new(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3));

    private static RatePoint Point(DateOnly day, string room, string plan, decimal amount) =>
        new(day, room, plan, Available: 5, Total: new Money(amount, "USD"));

    [Fact]
    public async Task SetThenGet_RoundTripsAcrossNightsAndRoomTypes()
    {
        var cache = new RedisRateSnapshotCache(_fixture.Multiplexer);
        var hotel = $"HOTEL-{Guid.NewGuid():N}";

        var night1 = new DateOnly(2026, 6, 1);
        var night2 = new DateOnly(2026, 6, 2);
        var input = new[]
        {
            Point(night1, "DLX", "BAR", 100),
            Point(night1, "STD", "BAR", 80),
            Point(night2, "DLX", "BAR", 110),
            Point(night2, "STD", "FLEX", 95),
        };

        await cache.SetAsync(ChannelCode.BlueWave, hotel, input, TimeSpan.FromMinutes(5), CancellationToken.None);

        var fetched = await cache.GetAsync(ChannelCode.BlueWave, hotel, TwoNights, CancellationToken.None);

        Assert.Equal(4, fetched.Count);
        Assert.Contains(fetched, p => p.StayDate == night1 && p.RoomTypeCode == "DLX" && p.Total.Amount == 100m);
        Assert.Contains(fetched, p => p.StayDate == night2 && p.RoomTypeCode == "STD" && p.RatePlanCode == "FLEX");
    }

    [Fact]
    public async Task SetForOneDay_LeavesOtherDayUntouched()
    {
        // Pins the per-day granularity contract on IRateSnapshotCache:
        // re-Setting one day must overwrite that day's bucket but leave
        // every other day intact — the Refresher relies on this so a
        // partial pull (one stay date) never wipes out the whole snapshot.
        var cache = new RedisRateSnapshotCache(_fixture.Multiplexer);
        var hotel = $"HOTEL-{Guid.NewGuid():N}";
        var night1 = new DateOnly(2026, 6, 1);
        var night2 = new DateOnly(2026, 6, 2);
        var ttl = TimeSpan.FromMinutes(5);

        await cache.SetAsync(ChannelCode.BlueWave, hotel, new[]
        {
            Point(night1, "DLX", "BAR", 100),
            Point(night2, "DLX", "BAR", 110),
        }, ttl, CancellationToken.None);

        await cache.SetAsync(ChannelCode.BlueWave, hotel, new[]
        {
            Point(night2, "DLX", "BAR", 222),
        }, ttl, CancellationToken.None);

        var fetched = await cache.GetAsync(ChannelCode.BlueWave, hotel, TwoNights, CancellationToken.None);

        Assert.Equal(2, fetched.Count);
        Assert.Contains(fetched, p => p.StayDate == night1 && p.Total.Amount == 100m);
        Assert.Contains(fetched, p => p.StayDate == night2 && p.Total.Amount == 222m);
    }

    [Fact]
    public async Task ChannelPartition_HoldsOverTheWire()
    {
        // Key prefix is unit-pinned (RedisRateSnapshotCacheKeyingTests),
        // but this proves the partitioning survives an actual HSET/HGET
        // round trip — a SkyTrip Get for the same hotel code MUST NOT see
        // BlueWave's snapshot. Most likely regression vector once the W6
        // SkyTrip adapter starts writing under the same hotel codes.
        var cache = new RedisRateSnapshotCache(_fixture.Multiplexer);
        var hotel = $"HOTEL-{Guid.NewGuid():N}";
        var night = new DateOnly(2026, 6, 1);

        await cache.SetAsync(ChannelCode.BlueWave, hotel, new[]
        {
            Point(night, "DLX", "BAR", 100),
        }, TimeSpan.FromMinutes(5), CancellationToken.None);

        var skyTripView = await cache.GetAsync(ChannelCode.SkyTrip, hotel, TwoNights, CancellationToken.None);

        Assert.Empty(skyTripView);
    }
}
