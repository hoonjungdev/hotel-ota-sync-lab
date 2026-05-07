using HotelOtaSync.Domain;

namespace HotelOtaSync.Application.Channels;

/// One pull-availability request for a single hotel and date range.
public sealed record AvailabilityQuery(string HotelCode, DateRange Stay);
