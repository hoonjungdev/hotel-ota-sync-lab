using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;

namespace HotelOtaSync.UnitTests.Channels;

public class ChannelRateRefresherTests
{
    private static readonly DateRange Stay = new(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3));

    [Fact]
    public async Task RefreshAsync_RegisteredChannel_WritesCacheAndReturnsCount()
    {
        var points = new[]
        {
            new RatePoint(new DateOnly(2026, 6, 1), "DLX", "BAR", 5, new Money(220m, "USD")),
            new RatePoint(new DateOnly(2026, 6, 2), "DLX", "BAR", 4, new Money(240m, "USD")),
        };
        var fakeChannel = new FakeChannelClient(ChannelCode.BlueWave, points);
        var fakeCache = new FakeRateSnapshotCache();
        var refresher = new ChannelRateRefresher(new[] { (IChannelClient)fakeChannel }, fakeCache);

        var written = await refresher.RefreshAsync(ChannelCode.BlueWave, "HOTEL-1", Stay, CancellationToken.None);

        Assert.Equal(2, written);
        Assert.Single(fakeCache.SetCalls);
        var (channel, hotel, capturedPoints, _) = fakeCache.SetCalls[0];
        Assert.Equal(ChannelCode.BlueWave, channel);
        Assert.Equal("HOTEL-1", hotel);
        Assert.Equal(2, capturedPoints.Count);
    }

    [Fact]
    public async Task RefreshAsync_UnregisteredChannel_ThrowsChannelException()
    {
        var fakeCache = new FakeRateSnapshotCache();
        var refresher = new ChannelRateRefresher(Array.Empty<IChannelClient>(), fakeCache);

        var ex = await Assert.ThrowsAsync<ChannelException>(() =>
            refresher.RefreshAsync(ChannelCode.BlueWave, "HOTEL-1", Stay, CancellationToken.None));

        Assert.Equal(ChannelFailureKind.BadRequest, ex.Kind);
        Assert.Empty(fakeCache.SetCalls);
    }

    [Fact]
    public async Task RefreshAsync_AdapterReturnsEmpty_DoesNotTouchCache()
    {
        var fakeChannel = new FakeChannelClient(ChannelCode.BlueWave, Array.Empty<RatePoint>());
        var fakeCache = new FakeRateSnapshotCache();
        var refresher = new ChannelRateRefresher(new[] { (IChannelClient)fakeChannel }, fakeCache);

        var written = await refresher.RefreshAsync(ChannelCode.BlueWave, "HOTEL-1", Stay, CancellationToken.None);

        Assert.Equal(0, written);
        Assert.Empty(fakeCache.SetCalls);
    }

    [Fact]
    public async Task RefreshAsync_AdapterThrows_PropagatesToCaller()
    {
        var fakeChannel = new FakeChannelClient(ChannelCode.BlueWave, throwOnPull:
            new ChannelException(ChannelCode.BlueWave, ChannelFailureKind.UpstreamServerError, "boom"));
        var fakeCache = new FakeRateSnapshotCache();
        var refresher = new ChannelRateRefresher(new[] { (IChannelClient)fakeChannel }, fakeCache);

        var ex = await Assert.ThrowsAsync<ChannelException>(() =>
            refresher.RefreshAsync(ChannelCode.BlueWave, "HOTEL-1", Stay, CancellationToken.None));

        Assert.Equal(ChannelFailureKind.UpstreamServerError, ex.Kind);
        Assert.Empty(fakeCache.SetCalls);
    }

    private sealed class FakeChannelClient : IChannelClient
    {
        private readonly IReadOnlyList<RatePoint> _points;
        private readonly Exception? _throwOnPull;

        public FakeChannelClient(ChannelCode channel, IReadOnlyList<RatePoint> points, Exception? throwOnPull = null)
        {
            Channel = channel;
            _points = points;
            _throwOnPull = throwOnPull;
        }

        public FakeChannelClient(ChannelCode channel, Exception throwOnPull)
            : this(channel, Array.Empty<RatePoint>(), throwOnPull) { }

        public ChannelCode Channel { get; }
        public ChannelCapabilities Capabilities { get; } = new()
        {
            SupportsAvailabilityPull = true,
            SupportsReservationPush = true,
            SupportsAtomicMultiRoom = true,
        };

        public Task<IReadOnlyList<RatePoint>> PullAvailabilityAsync(AvailabilityQuery query, CancellationToken cancellationToken)
            => _throwOnPull is not null ? Task.FromException<IReadOnlyList<RatePoint>>(_throwOnPull) : Task.FromResult(_points);

        public Task<ReservationPushAck> PushReservationAsync(ReservationPush push, CancellationToken cancellationToken)
            => throw new NotSupportedException("PushReservationAsync not used in refresher tests.");
    }

    private sealed class FakeRateSnapshotCache : IRateSnapshotCache
    {
        public List<(ChannelCode channel, string hotel, IReadOnlyList<RatePoint> points, TimeSpan ttl)> SetCalls { get; } = new();

        public Task<IReadOnlyList<RatePoint>> GetAsync(ChannelCode channel, string hotelCode, DateRange stay, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<RatePoint>>(Array.Empty<RatePoint>());

        public Task SetAsync(ChannelCode channel, string hotelCode, IReadOnlyList<RatePoint> points, TimeSpan ttl, CancellationToken cancellationToken)
        {
            SetCalls.Add((channel, hotelCode, points, ttl));
            return Task.CompletedTask;
        }

        public Task InvalidateAsync(ChannelCode channel, string hotelCode, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
