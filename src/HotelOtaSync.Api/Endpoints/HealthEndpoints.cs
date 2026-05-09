using StackExchange.Redis;

namespace HotelOtaSync.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (IConnectionMultiplexer redis, CancellationToken ct) =>
        {
            try
            {
                var db = redis.GetDatabase();
                var latency = await db.PingAsync();
                return Results.Ok(new { status = "ok", redisPingMs = latency.TotalMilliseconds });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Redis unreachable",
                    detail: ex.Message);
            }
        });
        return app;
    }
}
