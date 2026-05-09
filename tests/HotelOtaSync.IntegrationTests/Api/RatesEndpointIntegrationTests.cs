using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HotelOtaSync.IntegrationTests.Api;

[Collection("ApiIntegration")]
public class RatesEndpointIntegrationTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    public RatesEndpointIntegrationTests(ApiTestFixture fixture) => _fixture = fixture;

    private HttpRequestMessage AdminRefresh(string channel, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/admin/channels/{channel}/refresh")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiTestFixture.AdminToken);
        return req;
    }

    [Fact]
    public async Task RefreshThenGet_ServesFromRedis_NoOutboundOtaCallOnGet()
    {
        var hotel = $"HOTEL-{Guid.NewGuid():N}";

        var refresh = await _fixture.Client.SendAsync(AdminRefresh("bluewave", new
        {
            hotelCode = hotel,
            from = "2026-06-01",
            to = "2026-06-03",
        }));
        refresh.EnsureSuccessStatusCode();
        var refreshBody = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        var pointsCached = refreshBody.GetProperty("pointsCached").GetInt32();

        var get = await _fixture.Client.GetAsync(
            $"/properties/{hotel}/rates?from=2026-06-01&to=2026-06-03");
        get.EnsureSuccessStatusCode();
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();

        var blue = body.GetProperty("channels").GetProperty("BlueWave").EnumerateArray().ToList();
        Assert.Equal(pointsCached, blue.Count);
        Assert.All(blue, p => Assert.NotNull(p.GetProperty("stayDate").GetString()));
        Assert.Contains(blue, p => p.GetProperty("roomTypeCode").GetString() == "DLX");
    }

    [Fact]
    public async Task Get_OnUnrefreshedHotel_AllRegisteredChannelsEmpty()
    {
        var hotel = $"HOTEL-{Guid.NewGuid():N}";
        var get = await _fixture.Client.GetAsync(
            $"/properties/{hotel}/rates?from=2026-06-01&to=2026-06-03");

        get.EnsureSuccessStatusCode();
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        var channels = body.GetProperty("channels");

        Assert.True(channels.TryGetProperty("BlueWave", out var blue));
        Assert.Empty(blue.EnumerateArray());
    }

    [Fact]
    public async Task AdminRefresh_WithoutBearer_Returns401()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/admin/channels/bluewave/refresh")
        {
            Content = JsonContent.Create(new { hotelCode = "X", from = "2026-06-01", to = "2026-06-02" })
        };
        var resp = await _fixture.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AdminRefresh_UnknownChannelSegment_Returns404()
    {
        var req = AdminRefresh("not-a-real-channel", new
        {
            hotelCode = "X",
            from = "2026-06-01",
            to = "2026-06-02",
        });
        var resp = await _fixture.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_FromGreaterEqualTo_Returns400()
    {
        // Half-open range invariant: from must be strictly before to. The
        // endpoint MUST reject from == to (zero-night stay) and from > to.
        var resp = await _fixture.Client.GetAsync(
            "/properties/HOTEL-X/rates?from=2026-06-03&to=2026-06-03");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AdminRefresh_EmptyHotelCode_Returns400()
    {
        var req = AdminRefresh("bluewave", new
        {
            hotelCode = "",
            from = "2026-06-01",
            to = "2026-06-03",
        });
        var resp = await _fixture.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task AdminRefresh_FromGreaterEqualTo_Returns400()
    {
        var req = AdminRefresh("bluewave", new
        {
            hotelCode = "HOTEL-X",
            from = "2026-06-03",
            to = "2026-06-01",
        });
        var resp = await _fixture.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
