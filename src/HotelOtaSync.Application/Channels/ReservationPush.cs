using HotelOtaSync.Domain;

namespace HotelOtaSync.Application.Channels;

/// Channel-neutral payload for pushing a confirmed reservation to an OTA.
/// BookingId is the OUR system's reservation id; the channel may echo it back
/// or assign its own (returned via <see cref="ReservationPushAck"/>).
public sealed record ReservationPush(
    string HotelCode,
    string BookingId,
    DateRange Stay,
    string RoomTypeCode,
    string RatePlanCode,
    int Adults,
    Money Total,
    string GuestGivenName,
    string GuestSurname);

/// Channel-side identifier for a successfully accepted reservation push.
public sealed record ReservationPushAck(string ChannelReservationId);
