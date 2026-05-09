using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;

namespace HotelOtaSync.UnitTests.Channels;

public class GetCachedRatesQueryTests
{
    private static readonly DateRange TwoNights =
        new(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3));

    private static RatePoint Point(DateOnly day, decimal amount) =>
        new(day, "DLX", "BAR", 5, new Money(amount, "USD"));

    [Fact]
    public async Task Execute_EmptyCache_EmitsEntryPerRegisteredChannelWithEmptyArray()
    {
        var channels = new IChannelClient[]
        {
            new StubChannel(ChannelCode.BlueWave),
            new StubChannel(ChannelCode.SkyTrip),
        };
        var cache = new InMemoryStubCache();

        var query = new GetCachedRatesQuery(channels, cache);
        var result = await query.ExecuteAsync("HOTEL-1", TwoNights, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Empty(result[ChannelCode.BlueWave]);
        Assert.Empty(result[ChannelCode.SkyTrip]);
    }

    [Fact]
    public async Task Execute_PartialCache_OnlyFilledChannelsHaveData()
    {
        var channels = new IChannelClient[]
        {
            new StubChannel(ChannelCode.BlueWave),
            new StubChannel(ChannelCode.SkyTrip),
        };
        var cache = new InMemoryStubCache();
        await cache.SetAsync(
            ChannelCode.BlueWave, "HOTEL-1",
            new[] { Point(new DateOnly(2026, 6, 1), 100) },
            TimeSpan.FromMinutes(5), CancellationToken.None);

        var query = new GetCachedRatesQuery(channels, cache);
        var result = await query.ExecuteAsync("HOTEL-1", TwoNights, CancellationToken.None);

        Assert.Single(result[ChannelCode.BlueWave]);
        Assert.Empty(result[ChannelCode.SkyTrip]);
    }

    [Fact]
    public async Task Execute_DuplicateChannelRegistration_DeduplicatesByCode()
    {
        var channels = new IChannelClient[]
        {
            new StubChannel(ChannelCode.BlueWave),
            new StubChannel(ChannelCode.BlueWave),
        };
        var cache = new InMemoryStubCache();

        var query = new GetCachedRatesQuery(channels, cache);
        var result = await query.ExecuteAsync("HOTEL-1", TwoNights, CancellationToken.None);

        Assert.Single(result);
        Assert.Contains(ChannelCode.BlueWave, result.Keys);
    }

    private sealed class StubChannel(ChannelCode code) : IChannelClient
    {
        public ChannelCode Channel { get; } = code;
        public ChannelCapabilities Capabilities { get; } = new()
        {
            SupportsAvailabilityPull = true,
            SupportsReservationPush = false,
            SupportsAtomicMultiRoom = false,
        };
        public Task<IReadOnlyList<RatePoint>> PullAvailabilityAsync(AvailabilityQuery q, CancellationToken ct) =>
            throw new InvalidOperationException("Read use case must not call adapters");
        public Task<ReservationPushAck> PushReservationAsync(ReservationPush p, CancellationToken ct) =>
            throw new InvalidOperationException("Read use case must not call adapters");
    }

    private sealed class InMemoryStubCache : IRateSnapshotCache
    {
        private readonly Dictionary<(ChannelCode, string), List<RatePoint>> _data = new();

        public Task<IReadOnlyList<RatePoint>> GetAsync(ChannelCode channel, string hotelCode, DateRange stay, CancellationToken ct)
        {
            if (!_data.TryGetValue((channel, hotelCode), out var list))
                return Task.FromResult<IReadOnlyList<RatePoint>>(Array.Empty<RatePoint>());
            return Task.FromResult<IReadOnlyList<RatePoint>>(
                list.Where(p => p.StayDate >= stay.Start && p.StayDate < stay.End).ToList());
        }

        public Task SetAsync(ChannelCode channel, string hotelCode, IReadOnlyList<RatePoint> points, TimeSpan ttl, CancellationToken ct)
        {
            _data[(channel, hotelCode)] = points.ToList();
            return Task.CompletedTask;
        }

        public Task InvalidateAsync(ChannelCode channel, string hotelCode, CancellationToken ct)
        {
            _data.Remove((channel, hotelCode));
            return Task.CompletedTask;
        }
    }
}
