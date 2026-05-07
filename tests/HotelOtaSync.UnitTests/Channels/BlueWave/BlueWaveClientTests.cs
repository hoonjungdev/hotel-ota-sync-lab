using System.Net;
using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;
using HotelOtaSync.Infrastructure.Channels.BlueWave;

namespace HotelOtaSync.UnitTests.Channels.BlueWave;

/// Unit tests for BlueWaveClient. The Polly pipeline is intentionally NOT in
/// scope here — these tests target XML mapping and HTTP-status -> ChannelException
/// translation. Retry/circuit-breaker behaviour is covered by the integration
/// tests in HotelOtaSync.IntegrationTests against the real MockOta.BlueWave.
public class BlueWaveClientTests
{
    private static BlueWaveClient ClientWith(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://blue.test") });

    private static AvailabilityQuery SampleQuery() =>
        new("HOTEL-1", new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3)));

    private static ReservationPush SamplePush() => new(
        HotelCode: "HOTEL-1",
        BookingId: "BK-001",
        Stay: new DateRange(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3)),
        RoomTypeCode: "DLX",
        RatePlanCode: "BAR",
        Adults: 2,
        Total: new Money(440m, "USD"),
        GuestGivenName: "Alex",
        GuestSurname: "Tester");

    [Fact]
    public async Task Pull_HappyPath_MapsRoomStaysToRatePoints()
    {
        const string body = """
            <OTA_HotelAvailRS xmlns="http://www.opentravel.org/OTA/2003/05" Version="1.003">
              <Success/>
              <RoomStays>
                <RoomStay>
                  <RatePlans><RatePlan RatePlanCode="BAR"/></RatePlans>
                  <RoomTypes><RoomType RoomTypeCode="DLX"/></RoomTypes>
                  <StayDate>2026-06-01</StayDate>
                  <Total AmountAfterTax="220.00" CurrencyCode="USD"/>
                  <Available>5</Available>
                </RoomStay>
                <RoomStay>
                  <RatePlans><RatePlan RatePlanCode="FLEX"/></RatePlans>
                  <RoomTypes><RoomType RoomTypeCode="STD"/></RoomTypes>
                  <StayDate>2026-06-02</StayDate>
                  <Total AmountAfterTax="161.00" CurrencyCode="USD"/>
                  <Available>3</Available>
                </RoomStay>
              </RoomStays>
            </OTA_HotelAvailRS>
            """;
        var client = ClientWith(StubHandler.Xml(HttpStatusCode.OK, body));

        var points = await client.PullAvailabilityAsync(SampleQuery(), CancellationToken.None);

        Assert.Collection(points,
            p =>
            {
                Assert.Equal(new DateOnly(2026, 6, 1), p.StayDate);
                Assert.Equal("DLX", p.RoomTypeCode);
                Assert.Equal("BAR", p.RatePlanCode);
                Assert.Equal(5, p.Available);
                Assert.Equal(new Money(220m, "USD"), p.Total);
            },
            p =>
            {
                Assert.Equal(new DateOnly(2026, 6, 2), p.StayDate);
                Assert.Equal("STD", p.RoomTypeCode);
                Assert.Equal("FLEX", p.RatePlanCode);
                Assert.Equal(3, p.Available);
                Assert.Equal(new Money(161m, "USD"), p.Total);
            });
    }

    [Fact]
    public async Task Pull_OtaErrorsPayload_ThrowsProtocolError()
    {
        const string body = """
            <OTA_HotelAvailRS xmlns="http://www.opentravel.org/OTA/2003/05" Version="1.003">
              <Errors>
                <Error Type="3" Code="316">Invalid StayDateRange</Error>
              </Errors>
            </OTA_HotelAvailRS>
            """;
        var client = ClientWith(StubHandler.Xml(HttpStatusCode.OK, body));

        var ex = await Assert.ThrowsAsync<ChannelException>(() =>
            client.PullAvailabilityAsync(SampleQuery(), CancellationToken.None));

        Assert.Equal(ChannelCode.BlueWave, ex.Channel);
        Assert.Equal(ChannelFailureKind.ProtocolError, ex.Kind);
        Assert.Contains("316", ex.Message);
    }

    [Fact]
    public async Task Pull_5xxResponse_ThrowsUpstreamServerError()
    {
        var client = ClientWith(StubHandler.Status(HttpStatusCode.ServiceUnavailable));

        var ex = await Assert.ThrowsAsync<ChannelException>(() =>
            client.PullAvailabilityAsync(SampleQuery(), CancellationToken.None));

        Assert.Equal(ChannelFailureKind.UpstreamServerError, ex.Kind);
    }

    [Fact]
    public async Task Pull_4xxResponse_ThrowsBadRequest()
    {
        var client = ClientWith(StubHandler.Status(HttpStatusCode.BadRequest));

        var ex = await Assert.ThrowsAsync<ChannelException>(() =>
            client.PullAvailabilityAsync(SampleQuery(), CancellationToken.None));

        Assert.Equal(ChannelFailureKind.BadRequest, ex.Kind);
    }

    [Fact]
    public async Task Pull_TransportFailure_ThrowsTransport()
    {
        var client = ClientWith(StubHandler.Throws(new HttpRequestException("dns boom")));

        var ex = await Assert.ThrowsAsync<ChannelException>(() =>
            client.PullAvailabilityAsync(SampleQuery(), CancellationToken.None));

        Assert.Equal(ChannelFailureKind.Transport, ex.Kind);
    }

    [Fact]
    public async Task Push_HappyPath_ReturnsChannelReservationId()
    {
        const string body = """
            <OTA_HotelResNotifRS xmlns="http://www.opentravel.org/OTA/2003/05" Version="1.003">
              <Success/>
              <HotelReservations>
                <HotelReservation>
                  <ResGlobalInfo>
                    <HotelReservationIDs>
                      <HotelReservationID ResID_Type="14" ResID_Value="BW-99001" ResID_Source="BlueWave"/>
                    </HotelReservationIDs>
                  </ResGlobalInfo>
                </HotelReservation>
              </HotelReservations>
            </OTA_HotelResNotifRS>
            """;
        var client = ClientWith(StubHandler.Xml(HttpStatusCode.OK, body));

        var ack = await client.PushReservationAsync(SamplePush(), CancellationToken.None);

        Assert.Equal("BW-99001", ack.ChannelReservationId);
    }

    [Fact]
    public async Task Push_AckMissingHotelReservationId_ThrowsProtocolError()
    {
        const string body = """
            <OTA_HotelResNotifRS xmlns="http://www.opentravel.org/OTA/2003/05" Version="1.003">
              <Success/>
              <HotelReservations>
                <HotelReservation>
                  <ResGlobalInfo>
                    <HotelReservationIDs/>
                  </ResGlobalInfo>
                </HotelReservation>
              </HotelReservations>
            </OTA_HotelResNotifRS>
            """;
        var client = ClientWith(StubHandler.Xml(HttpStatusCode.OK, body));

        var ex = await Assert.ThrowsAsync<ChannelException>(() =>
            client.PushReservationAsync(SamplePush(), CancellationToken.None));

        Assert.Equal(ChannelFailureKind.ProtocolError, ex.Kind);
    }

    [Fact]
    public void Capabilities_AdvertiseBlueWaveBaseline()
    {
        var client = ClientWith(StubHandler.Status(HttpStatusCode.OK));

        Assert.Equal(ChannelCode.BlueWave, client.Channel);
        Assert.True(client.Capabilities.SupportsAvailabilityPull);
        Assert.True(client.Capabilities.SupportsReservationPush);
        Assert.True(client.Capabilities.SupportsAtomicMultiRoom);
    }
}
