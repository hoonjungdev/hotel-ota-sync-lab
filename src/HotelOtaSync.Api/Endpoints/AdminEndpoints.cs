using HotelOtaSync.Api.Auth;
using HotelOtaSync.Api.Errors;
using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;

namespace HotelOtaSync.Api.Endpoints;

internal static class AdminEndpoints
{
    private static readonly Dictionary<string, ChannelCode> ByRouteSegment =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["bluewave"] = ChannelCode.BlueWave,
            ["skytrip"] = ChannelCode.SkyTrip,
            ["greenleaf"] = ChannelCode.GreenLeaf,
        };

    public sealed record RefreshRequest(string HotelCode, DateOnly From, DateOnly To);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").AddEndpointFilter<AdminTokenFilter>();

        admin.MapPost("/channels/{channel}/refresh", async (
            string channel,
            RefreshRequest body,
            ChannelRateRefresher refresher,
            CancellationToken ct) =>
        {
            if (!ByRouteSegment.TryGetValue(channel, out var channelCode))
                return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                    title: $"Unknown channel route segment '{channel}'.");

            if (string.IsNullOrWhiteSpace(body?.HotelCode))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "hotelCode is required.");

            if (body.From >= body.To)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Body 'from' must be strictly before 'to' (half-open range).");

            try
            {
                var pointsCached = await refresher.RefreshAsync(
                    channelCode, body.HotelCode, new DateRange(body.From, body.To), ct);
                return Results.Ok(new { pointsCached });
            }
            catch (ChannelException ex)
            {
                return ChannelExceptionMapper.ToResult(ex);
            }
        });

        return app;
    }
}
