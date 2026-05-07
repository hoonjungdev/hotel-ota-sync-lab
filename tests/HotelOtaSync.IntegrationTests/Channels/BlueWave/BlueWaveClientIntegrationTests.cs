using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;

namespace HotelOtaSync.IntegrationTests.Channels.BlueWave;

[Collection("BlueWave")]
public class BlueWaveClientIntegrationTests : IClassFixture<BlueWaveTestFixture>
{
    private readonly BlueWaveTestFixture _fixture;

    public BlueWaveClientIntegrationTests(BlueWaveTestFixture fixture) => _fixture = fixture;

    private static readonly DateRange June2Nights = new(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3));

    [Fact]
    public async Task Pull_HappyPath_ReturnsRoomStaysFromMock()
    {
        var (sp, channel) = BlueWaveAdapterFactory.Build(_fixture);
        await using var _ = sp;

        var points = await channel.PullAvailabilityAsync(
            new AvailabilityQuery("HOTEL-1", June2Nights), CancellationToken.None);

        // 2 nights * 2 room types * 2 rate plans = 8.
        Assert.Equal(8, points.Count);
        Assert.All(points, p => Assert.Equal("USD", p.Total.Currency));
        Assert.Contains(points, p => p.RoomTypeCode == "DLX" && p.RatePlanCode == "BAR");
        Assert.Contains(points, p => p.RoomTypeCode == "STD" && p.RatePlanCode == "FLEX");
    }

    [Fact]
    public async Task Pull_TwoTransient503sThenSuccess_PollyRetriesAndSucceeds()
    {
        // MaxRetryAttempts=2 means up to 3 total attempts. We inject 503 on
        // the first 2; the 3rd should pass through and return real data.
        var failer = new FailFirstNHandler(failFirst: 2, injectStatus: 503);
        var (sp, channel) = BlueWaveAdapterFactory.Build(_fixture, outboundHandler: failer);
        await using var _ = sp;

        var points = await channel.PullAvailabilityAsync(
            new AvailabilityQuery("HOTEL-1", June2Nights), CancellationToken.None);

        Assert.Equal(8, points.Count);
        Assert.Equal(3, failer.TotalSeen);
        Assert.Equal(2, failer.InjectedCount);
    }

    [Fact]
    public async Task Pull_PersistentServerError_ExhaustsRetriesAndThrowsUpstream()
    {
        // 99 503s with MaxRetryAttempts=2 → all 3 attempts fail.
        var failer = new FailFirstNHandler(failFirst: 99, injectStatus: 503);
        var (sp, channel) = BlueWaveAdapterFactory.Build(_fixture, outboundHandler: failer);
        await using var _ = sp;

        var ex = await Assert.ThrowsAsync<ChannelException>(() =>
            channel.PullAvailabilityAsync(new AvailabilityQuery("HOTEL-1", June2Nights), CancellationToken.None));

        Assert.Equal(ChannelFailureKind.UpstreamServerError, ex.Kind);
        Assert.Equal(3, failer.TotalSeen);
    }

    [Fact]
    public async Task Push_HappyPath_ReturnsAckIdEchoedFromMock()
    {
        var (sp, channel) = BlueWaveAdapterFactory.Build(_fixture);
        await using var _ = sp;

        var ack = await channel.PushReservationAsync(new ReservationPush(
            HotelCode: "HOTEL-1",
            BookingId: "BK-INT-001",
            Stay: June2Nights,
            RoomTypeCode: "DLX",
            RatePlanCode: "BAR",
            Adults: 2,
            Total: new Money(440m, "USD"),
            GuestGivenName: "Alex",
            GuestSurname: "Tester"), CancellationToken.None);

        // Mock echoes back the BookingId we sent in UniqueID.
        Assert.Equal("BK-INT-001", ack.ChannelReservationId);
    }

    [Fact]
    public async Task Capabilities_Match_BlueWaveBaseline()
    {
        var (sp, channel) = BlueWaveAdapterFactory.Build(_fixture);
        await using var _ = sp;

        Assert.Equal(ChannelCode.BlueWave, channel.Channel);
        Assert.True(channel.Capabilities.SupportsAvailabilityPull);
        Assert.True(channel.Capabilities.SupportsReservationPush);
        Assert.True(channel.Capabilities.SupportsAtomicMultiRoom);
    }
}
