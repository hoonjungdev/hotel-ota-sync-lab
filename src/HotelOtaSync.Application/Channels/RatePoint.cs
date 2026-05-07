using HotelOtaSync.Domain;

namespace HotelOtaSync.Application.Channels;

/// One night of availability + price for a (room type, rate plan) pair, as
/// returned by a channel adapter. Channel-neutral: BlueWave (XML) and SkyTrip
/// (JSON) both project into this record.
public sealed record RatePoint(
    DateOnly StayDate,
    string RoomTypeCode,
    string RatePlanCode,
    int Available,
    Money Total);
