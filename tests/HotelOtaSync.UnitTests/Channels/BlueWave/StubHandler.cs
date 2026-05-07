using System.Net;

namespace HotelOtaSync.UnitTests.Channels.BlueWave;

/// Tiny HttpMessageHandler that lets a test prescribe responses inline. Used
/// to exercise BlueWaveClient's status-code and XML-error mapping without
/// pulling in a real HTTP server or Polly pipeline.
internal sealed class StubHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

    public List<HttpRequestMessage> Requests { get; } = new();

    public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    public static StubHandler Xml(HttpStatusCode status, string xmlBody) => new(_ =>
    {
        var resp = new HttpResponseMessage(status)
        {
            Content = new StringContent(xmlBody, System.Text.Encoding.UTF8, "application/xml"),
        };
        return resp;
    });

    public static StubHandler Status(HttpStatusCode status) => new(_ => new HttpResponseMessage(status));

    public static StubHandler Throws(Exception ex) => new(_ => throw ex);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        try
        {
            return Task.FromResult(_respond(request));
        }
        catch (Exception ex)
        {
            return Task.FromException<HttpResponseMessage>(ex);
        }
    }
}
