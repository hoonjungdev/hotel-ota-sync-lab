using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;

namespace HotelOtaSync.Api.Endpoints;

internal static class RatesEndpoints
{
    public static IEndpointRouteBuilder MapRatesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/properties/{hotelCode}/rates", async (
            string hotelCode,
            DateOnly from,
            DateOnly to,
            GetCachedRatesQuery query,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(hotelCode))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "hotelCode is required.");

            if (from >= to)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Query 'from' must be strictly before 'to' (half-open range).");

            var stay = new DateRange(from, to);
            var byChannel = await query.ExecuteAsync(hotelCode, stay, ct);

            return Results.Ok(new
            {
                channels = byChannel.ToDictionary(
                    kvp => kvp.Key.Value,
                    kvp => kvp.Value)
            });
        });

        return app;
    }
}
