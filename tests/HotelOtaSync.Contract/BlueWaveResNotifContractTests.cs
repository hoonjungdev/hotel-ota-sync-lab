using System.Net.Http.Headers;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using MockOta.BlueWave.Contracts;

namespace HotelOtaSync.Contract;

public sealed class BlueWaveResNotifContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public BlueWaveResNotifContractTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task HotelResNotifRQ_acks_each_reservation_with_its_booking_id()
    {
        var rq = NewRq("BW-100");

        var rs = await PostXml<OtaHotelResNotifRq, OtaHotelResNotifRs>("/ota/HotelResNotifRQ", rq);

        Assert.NotNull(rs.Success);
        Assert.Single(rs.Acks);
        Assert.Equal("BW-100", rs.Acks[0].GlobalInfo.Ids.Items[0].Value);
        Assert.Equal("BlueWave", rs.Acks[0].GlobalInfo.Ids.Items[0].Source);
    }

    [Fact]
    public async Task HotelResNotifRQ_is_idempotent_on_duplicate_booking_id()
    {
        // Same booking id sent twice — both calls succeed, mock does not error.
        // The downstream Property API is the layer that enforces dedupe in DB.
        var first = await PostXml<OtaHotelResNotifRq, OtaHotelResNotifRs>("/ota/HotelResNotifRQ", NewRq("BW-200"));
        var second = await PostXml<OtaHotelResNotifRq, OtaHotelResNotifRs>("/ota/HotelResNotifRQ", NewRq("BW-200"));

        Assert.NotNull(first.Success);
        Assert.NotNull(second.Success);
        Assert.Equal("BW-200", first.Acks[0].GlobalInfo.Ids.Items[0].Value);
        Assert.Equal("BW-200", second.Acks[0].GlobalInfo.Ids.Items[0].Value);
    }

    private static OtaHotelResNotifRq NewRq(string bookingId) => new()
    {
        EchoToken = "tok-" + bookingId,
        Reservations =
        {
            new HotelReservation
            {
                UniqueIds = { new UniqueId { Type = "14", Id = bookingId, Context = "BlueWave" } },
                RoomStays = new ResRoomStays
                {
                    RoomStay = new ResRoomStay
                    {
                        Rooms = new RoomTypes { RoomType = new RoomType { Code = "DLX" } },
                        Rates = new RatePlans { RatePlan = new RatePlan { Code = "BAR" } },
                        GuestCounts = new GuestCounts { Count = new GuestCount { Count = 2 } },
                        TimeSpan = new TimeSpanRange { Start = "2026-06-01", End = "2026-06-03" },
                        Total = new Total { AmountAfterTax = 440.00m, CurrencyCode = "USD" }
                    }
                },
                Guests = new ResGuests
                {
                    Guest = new ResGuest
                    {
                        Profiles = new Profiles
                        {
                            Profile = new ProfileInfo
                            {
                                Inner = new Profile
                                {
                                    Customer = new Customer
                                    {
                                        Name = new PersonName { GivenName = "Jane", Surname = "Doe" }
                                    }
                                }
                            }
                        }
                    }
                },
                GlobalInfo = new ResGlobalInfo
                {
                    Ids = new HotelReservationIds
                    {
                        Items = { new HotelReservationId { Type = "14", Value = bookingId } }
                    }
                }
            }
        }
    };

    private async Task<TRs> PostXml<TRq, TRs>(string path, TRq body)
    {
        var serializer = new XmlSerializer(typeof(TRq));
        using var bodyMs = new MemoryStream();
        serializer.Serialize(bodyMs, body);

        var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new ByteArrayContent(bodyMs.ToArray())
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml") { CharSet = "utf-8" };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));

        var response = await _client.SendAsync(req);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        var rsSerializer = new XmlSerializer(typeof(TRs));
        using var rsMs = new MemoryStream(bytes);
        return (TRs)rsSerializer.Deserialize(rsMs)!;
    }
}
