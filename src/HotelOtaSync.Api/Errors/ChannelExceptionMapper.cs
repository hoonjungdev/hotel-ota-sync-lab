using HotelOtaSync.Application.Channels;

namespace HotelOtaSync.Api.Errors;

/// Maps ChannelException.Kind to HTTP ProblemDetails. Used by both the admin
/// refresh endpoint and the global exception handler (Program.cs) so every
/// surface gets the same channel-failure semantics.
internal static class ChannelExceptionMapper
{
    public static IResult ToResult(ChannelException ex) => ex.Kind switch
    {
        ChannelFailureKind.BadRequest => Problem(StatusCodes.Status400BadRequest, ex),
        ChannelFailureKind.UpstreamServerError => Problem(StatusCodes.Status502BadGateway, ex),
        ChannelFailureKind.Transport => Problem(StatusCodes.Status502BadGateway, ex),
        ChannelFailureKind.ProtocolError => Problem(StatusCodes.Status502BadGateway, ex),
        ChannelFailureKind.CircuitOpen => Problem(StatusCodes.Status503ServiceUnavailable, ex),
        _ => Problem(StatusCodes.Status500InternalServerError, ex),
    };

    private static IResult Problem(int status, ChannelException ex) =>
        Results.Problem(
            statusCode: status,
            title: $"Channel '{ex.Channel}' failed ({ex.Kind})",
            detail: ex.Message);
}
