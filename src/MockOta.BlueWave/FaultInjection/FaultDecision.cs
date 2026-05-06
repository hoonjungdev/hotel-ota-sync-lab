namespace MockOta.BlueWave.FaultInjection;

/// What the policy decides to do for a single request, before the
/// controller runs. The middleware applies the decision: it sleeps for
/// DelayMs (if > 0), then either short-circuits with HttpStatusCode (if set)
/// or passes the request through to the next middleware.
public readonly record struct FaultDecision(int DelayMs, int? HttpStatusCode)
{
    public static readonly FaultDecision PassThrough = new(0, null);

    public bool ShouldShortCircuit => HttpStatusCode is not null;
}
