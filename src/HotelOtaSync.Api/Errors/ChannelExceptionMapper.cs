using HotelOtaSync.Application.Channels;

namespace HotelOtaSync.Api.Errors;

/// Maps ChannelException.Kind to HTTP ProblemDetails. Used by the admin
/// refresh endpoint (and any future surface that calls a channel adapter)
/// so every surface gets the same channel-failure semantics.
///
/// `detail` is only populated when running under the Development host
/// environment — in any other environment we omit `ex.Message` to avoid
/// leaking upstream URLs or stack-shaped strings to API consumers. The
/// `title` (channel + Kind) is always safe to expose because it's
/// derived entirely from our own enum.
internal static class ChannelExceptionMapper
{
    public static IResult ToResult(ChannelException ex, IHostEnvironment env) => ex.Kind switch
    {
        ChannelFailureKind.BadRequest => Problem(StatusCodes.Status400BadRequest, ex, env),
        ChannelFailureKind.UpstreamServerError => Problem(StatusCodes.Status502BadGateway, ex, env),
        ChannelFailureKind.Transport => Problem(StatusCodes.Status502BadGateway, ex, env),
        ChannelFailureKind.ProtocolError => Problem(StatusCodes.Status502BadGateway, ex, env),
        ChannelFailureKind.CircuitOpen => Problem(StatusCodes.Status503ServiceUnavailable, ex, env),
        _ => Problem(StatusCodes.Status500InternalServerError, ex, env),
    };

    private static IResult Problem(int status, ChannelException ex, IHostEnvironment env) =>
        Results.Problem(
            statusCode: status,
            title: $"Channel '{ex.Channel}' failed ({ex.Kind})",
            detail: env.IsDevelopment() ? ex.Message : null);
}
