namespace HotelOtaSync.IntegrationTests.Channels.BlueWave;

/// DelegatingHandler that decorates the FIRST N outbound requests with the
/// MockOta.BlueWave fault-injection header asking for the given HTTP status,
/// then lets subsequent requests pass through. Lets us prove the retry
/// strategy actually retries: with N=2 and MaxRetryAttempts=2 the third
/// attempt (the second retry) should succeed.
internal sealed class FailFirstNHandler : DelegatingHandler
{
    private int _remaining;
    private readonly int _injectStatus;

    public int InjectedCount { get; private set; }
    public int TotalSeen { get; private set; }

    public FailFirstNHandler(int failFirst, int injectStatus = 503)
    {
        _remaining = failFirst;
        _injectStatus = injectStatus;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        TotalSeen++;
        // Polly reuses the same HttpRequestMessage across retries, so we
        // must clear the header on every attempt before deciding whether to
        // re-add it. Otherwise a previous attempt's 503 header sticks.
        request.Headers.Remove("X-Mock-Inject-Status");
        if (Interlocked.Decrement(ref _remaining) >= 0)
        {
            request.Headers.Add("X-Mock-Inject-Status", _injectStatus.ToString(System.Globalization.CultureInfo.InvariantCulture));
            InjectedCount++;
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
