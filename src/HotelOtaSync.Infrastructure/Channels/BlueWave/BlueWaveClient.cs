using System.Net.Http.Headers;
using System.Xml.Serialization;
using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;
using HotelOtaSync.Infrastructure.Channels.BlueWave.Wire;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace HotelOtaSync.Infrastructure.Channels.BlueWave;

/// IChannelClient adapter for the BlueWave OTA (OpenTravel-style XML over
/// HTTP). The HttpClient handed in is expected to already be wrapped in the
/// resilience pipeline configured by AddBlueWaveChannel.
public sealed class BlueWaveClient : IChannelClient
{
    private static readonly XmlSerializer AvailRqSer = new(typeof(WireAvailRq));
    private static readonly XmlSerializer AvailRsSer = new(typeof(WireAvailRs));
    private static readonly XmlSerializer ResNotifRqSer = new(typeof(WireResNotifRq));
    private static readonly XmlSerializer ResNotifRsSer = new(typeof(WireResNotifRs));

    private static readonly MediaTypeHeaderValue XmlMediaType = new("application/xml") { CharSet = "utf-8" };

    private static readonly ChannelCapabilities Caps = new()
    {
        SupportsAvailabilityPull = true,
        SupportsReservationPush = true,
        SupportsAtomicMultiRoom = true,
    };

    private readonly HttpClient _http;

    public BlueWaveClient(HttpClient http) => _http = http;

    public ChannelCode Channel => ChannelCode.BlueWave;
    public ChannelCapabilities Capabilities => Caps;

    public async Task<IReadOnlyList<RatePoint>> PullAvailabilityAsync(
        AvailabilityQuery query,
        CancellationToken cancellationToken)
    {
        var rq = new WireAvailRq
        {
            EchoToken = NewEchoToken(),
            Segments =
            {
                new WireAvailSegment
                {
                    Criteria = new WireSearchCriteria
                    {
                        Criterion = new WireCriterion
                        {
                            Hotel = new WireHotelRef { HotelCode = query.HotelCode },
                            Stay = new WireStayDateRange
                            {
                                Start = query.Stay.Start.ToString("yyyy-MM-dd"),
                                End = query.Stay.End.ToString("yyyy-MM-dd"),
                            },
                        },
                    },
                },
            },
        };

        var rs = await PostXmlAsync<WireAvailRq, WireAvailRs>(
            "ota/HotelAvailRQ", rq, AvailRqSer, AvailRsSer, cancellationToken);

        ThrowIfProtocolError(rs.Errors);

        var points = new List<RatePoint>(rs.RoomStays.Count);
        foreach (var stay in rs.RoomStays)
        {
            if (!DateOnly.TryParse(stay.StayDate, out var date))
            {
                throw new ChannelException(Channel, ChannelFailureKind.ProtocolError,
                    $"BlueWave returned unparseable StayDate '{stay.StayDate}'.");
            }
            points.Add(new RatePoint(
                StayDate: date,
                RoomTypeCode: stay.Rooms.RoomType.Code,
                RatePlanCode: stay.Rates.RatePlan.Code,
                Available: stay.Available,
                Total: new Money(stay.Total.AmountAfterTax, stay.Total.CurrencyCode)));
        }
        return points;
    }

    public async Task<ReservationPushAck> PushReservationAsync(
        ReservationPush push,
        CancellationToken cancellationToken)
    {
        var rq = new WireResNotifRq
        {
            EchoToken = NewEchoToken(),
            Reservations =
            {
                new WireHotelReservation
                {
                    UniqueIds = { new WireUniqueId { Type = "14", Id = push.BookingId, Context = "OurSystem" } },
                    RoomStays = new WireResRoomStays
                    {
                        RoomStay = new WireResRoomStay
                        {
                            Rooms = new WireRoomTypes { RoomType = new WireRoomType { Code = push.RoomTypeCode } },
                            Rates = new WireRatePlans { RatePlan = new WireRatePlan { Code = push.RatePlanCode } },
                            GuestCounts = new WireGuestCounts
                            {
                                Count = new WireGuestCount { AgeQualifying = "10", Count = push.Adults },
                            },
                            TimeSpan = new WireTimeSpanRange
                            {
                                Start = push.Stay.Start.ToString("yyyy-MM-dd"),
                                End = push.Stay.End.ToString("yyyy-MM-dd"),
                            },
                            Total = new WireTotal
                            {
                                AmountAfterTax = push.Total.Amount,
                                CurrencyCode = push.Total.Currency,
                            },
                        },
                    },
                    Guests = new WireResGuests
                    {
                        Guest = new WireResGuest
                        {
                            Profiles = new WireProfiles
                            {
                                Profile = new WireProfileInfo
                                {
                                    Inner = new WireProfile
                                    {
                                        Customer = new WireCustomer
                                        {
                                            Name = new WirePersonName
                                            {
                                                GivenName = push.GuestGivenName,
                                                Surname = push.GuestSurname,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };

        var rs = await PostXmlAsync<WireResNotifRq, WireResNotifRs>(
            "ota/HotelResNotifRQ", rq, ResNotifRqSer, ResNotifRsSer, cancellationToken);

        ThrowIfProtocolError(rs.Errors);

        var ack = rs.Acks.FirstOrDefault();
        var channelId = ack?.GlobalInfo.Ids.Items.FirstOrDefault()?.Value;
        if (string.IsNullOrEmpty(channelId))
        {
            throw new ChannelException(Channel, ChannelFailureKind.ProtocolError,
                "BlueWave accepted reservation but returned no HotelReservationID.");
        }
        return new ReservationPushAck(channelId);
    }

    private async Task<TRs> PostXmlAsync<TRq, TRs>(
        string path,
        TRq rq,
        XmlSerializer rqSer,
        XmlSerializer rsSer,
        CancellationToken cancellationToken)
        where TRq : class
        where TRs : class
    {
        using var content = SerializeToXmlContent(rqSer, rq);

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(path, content, cancellationToken).ConfigureAwait(false);
        }
        catch (BrokenCircuitException ex)
        {
            throw new ChannelException(Channel, ChannelFailureKind.CircuitOpen,
                "Circuit breaker is open for BlueWave.", ex);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new ChannelException(Channel, ChannelFailureKind.Transport,
                "BlueWave call exceeded per-attempt timeout.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ChannelException(Channel, ChannelFailureKind.Transport,
                "BlueWave call timed out.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ChannelException(Channel, ChannelFailureKind.Transport,
                "Transport failure calling BlueWave.", ex);
        }

        using (response)
        {
            var status = (int)response.StatusCode;
            if (status >= 500)
            {
                throw new ChannelException(Channel, ChannelFailureKind.UpstreamServerError,
                    $"BlueWave returned {status} after retries.");
            }
            if (status >= 400)
            {
                throw new ChannelException(Channel, ChannelFailureKind.BadRequest,
                    $"BlueWave rejected request with {status}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return (TRs)rsSer.Deserialize(stream)!;
            }
            catch (InvalidOperationException ex)
            {
                throw new ChannelException(Channel, ChannelFailureKind.ProtocolError,
                    "Failed to deserialize BlueWave XML response.", ex);
            }
        }
    }

    private static ByteArrayContent SerializeToXmlContent<T>(XmlSerializer ser, T payload)
    {
        // Serialize through MemoryStream so the bytes carry a genuine UTF-8
        // declaration. StringBuilder/StringWriter would advertise utf-16 in
        // the `<?xml ?>` header, then StringContent re-encodes to utf-8 and
        // ASP.NET's XmlSerializerInputFormatter throws on the mismatch.
        var nss = new XmlSerializerNamespaces();
        nss.Add("", OtaXml.Namespace);
        using var ms = new MemoryStream();
        ser.Serialize(ms, payload, nss);
        var content = new ByteArrayContent(ms.ToArray());
        content.Headers.ContentType = XmlMediaType;
        return content;
    }

    private void ThrowIfProtocolError(WireErrors? errors)
    {
        if (errors is null || errors.Items.Count == 0) return;
        var first = errors.Items[0];
        throw new ChannelException(Channel, ChannelFailureKind.ProtocolError,
            $"BlueWave returned error {first.Code} (Type {first.Type}): {first.Message}");
    }

    private static string NewEchoToken() => Guid.NewGuid().ToString("N");
}
