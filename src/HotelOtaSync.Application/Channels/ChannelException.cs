namespace HotelOtaSync.Application.Channels;

/// Adapter-level failure that callers (use-cases, workers) can pattern-match
/// without depending on HttpRequestException or XML-specific types.
public sealed class ChannelException : Exception
{
    public ChannelCode Channel { get; }
    public ChannelFailureKind Kind { get; }

    public ChannelException(ChannelCode channel, ChannelFailureKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Channel = channel;
        Kind = kind;
    }
}

public enum ChannelFailureKind
{
    /// Network-level failure: timeout, DNS, connection reset.
    Transport,

    /// Channel returned an HTTP 5xx after retries were exhausted.
    UpstreamServerError,

    /// Channel returned an HTTP 4xx — caller bug, not retried.
    BadRequest,

    /// Response decoded successfully but contained an OTA-level error payload.
    ProtocolError,

    /// Polly's circuit breaker is currently open for this channel.
    CircuitOpen,
}
