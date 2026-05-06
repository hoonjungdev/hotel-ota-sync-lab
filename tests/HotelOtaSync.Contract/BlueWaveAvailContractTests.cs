using System.Net;
using System.Net.Http.Headers;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using MockOta.BlueWave.Contracts;

namespace HotelOtaSync.Contract;

/// Black-box contract tests for the BlueWave mock. These pin the wire shape
/// the W3 BlueWaveClient adapter will couple to. If anything in this file
/// breaks, the adapter's integration assumptions break too — read carefully
/// before changing.
public sealed class BlueWaveAvailContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BlueWaveAvailContractTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task HotelAvailRQ_returns_one_room_stay_per_day_per_combo()
    {
        var rq = new OtaHotelAvailRq
        {
            EchoToken = "test-1",
            Segments =
            {
                new AvailRequestSegment
                {
                    Criteria = new HotelSearchCriteria
                    {
                        Criterion = new Criterion
                        {
                            Hotel = new HotelRef { HotelCode = "HTL001" },
                            Stay = new StayDateRange { Start = "2026-06-01", End = "2026-06-04" }
                        }
                    }
                }
            }
        };

        var rs = await PostXml<OtaHotelAvailRq, OtaHotelAvailRs>("/ota/HotelAvailRQ", rq);

        Assert.NotNull(rs.Success);
        Assert.Null(rs.Errors);
        Assert.Equal("test-1", rs.EchoToken);
        // 3 days * 2 room types * 2 rate plans = 12 stays
        Assert.Equal(12, rs.RoomStays.Count);
        Assert.All(rs.RoomStays, s => Assert.Equal("USD", s.Total.CurrencyCode));
        Assert.All(rs.RoomStays, s => Assert.Contains(s.Rooms.RoomType.Code, new[] { "DLX", "STD" }));
    }

    [Fact]
    public async Task HotelAvailRQ_with_invalid_dates_returns_error_316()
    {
        var rq = new OtaHotelAvailRq
        {
            Segments =
            {
                new AvailRequestSegment
                {
                    Criteria = new HotelSearchCriteria
                    {
                        Criterion = new Criterion
                        {
                            Hotel = new HotelRef { HotelCode = "HTL001" },
                            Stay = new StayDateRange { Start = "2026-06-04", End = "2026-06-01" }
                        }
                    }
                }
            }
        };

        var rs = await PostXml<OtaHotelAvailRq, OtaHotelAvailRs>("/ota/HotelAvailRQ", rq);

        Assert.Null(rs.Success);
        Assert.NotNull(rs.Errors);
        Assert.Equal("316", rs.Errors!.Items[0].Code);
    }

    [Fact]
    public async Task FaultInjection_header_forces_503()
    {
        var rq = new OtaHotelAvailRq
        {
            Segments =
            {
                new AvailRequestSegment
                {
                    Criteria = new HotelSearchCriteria
                    {
                        Criterion = new Criterion
                        {
                            Hotel = new HotelRef { HotelCode = "HTL001" },
                            Stay = new StayDateRange { Start = "2026-06-01", End = "2026-06-02" }
                        }
                    }
                }
            }
        };

        var req = BuildXmlRequest("/ota/HotelAvailRQ", rq);
        req.Headers.Add("X-Mock-Inject-Status", "503");
        var response = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private async Task<TRs> PostXml<TRq, TRs>(string path, TRq body)
    {
        var response = await _client.SendAsync(BuildXmlRequest(path, body));
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var serializer = new XmlSerializer(typeof(TRs));
        using var ms = new MemoryStream(bytes);
        return (TRs)serializer.Deserialize(ms)!;
    }

    private static HttpRequestMessage BuildXmlRequest<T>(string path, T body)
    {
        // Serialize via MemoryStream so the resulting bytes carry a real
        // UTF-8 declaration. StringWriter would advertise utf-16 in the
        // <?xml ?> header, then StringContent re-encodes to utf-8 → ASP.NET
        // throws on the mismatch.
        var serializer = new XmlSerializer(typeof(T));
        using var ms = new MemoryStream();
        serializer.Serialize(ms, body);
        var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(ms.ToArray())
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = "utf-8" };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
        return req;
    }
}
