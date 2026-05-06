using Microsoft.Extensions.Options;

namespace MockOta.BlueWave.FaultInjection;

/// Sits in front of every request. Asks the policy what to do, then either
/// sleeps + short-circuits with a 5xx, or sleeps + passes through.
///
/// Header overrides recognised on every request (so contract tests stay
/// deterministic regardless of the global Options state):
///   X-Mock-Inject-Latency-Ms     numeric, e.g. "750"
///   X-Mock-Inject-Status         numeric, e.g. "503" (forces a 5xx)
///   X-Mock-Inject-PassThrough    "true" disables fault injection for this
///                                 request even when Options.Enabled is on.
public sealed class FaultInjectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<FaultInjectionOptions> _options;
    private readonly ILogger<FaultInjectionMiddleware> _logger;

    public FaultInjectionMiddleware(
        RequestDelegate next,
        IOptionsMonitor<FaultInjectionOptions> options,
        ILogger<FaultInjectionMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var decision = Decide(ctx, _options.CurrentValue);

        if (decision.DelayMs > 0)
        {
            _logger.LogInformation("FaultInjection: delaying {DelayMs}ms for {Path}",
                decision.DelayMs, ctx.Request.Path);
            await Task.Delay(decision.DelayMs, ctx.RequestAborted);
        }

        if (decision.ShouldShortCircuit)
        {
            _logger.LogWarning("FaultInjection: short-circuit {Status} for {Path}",
                decision.HttpStatusCode, ctx.Request.Path);
            ctx.Response.StatusCode = decision.HttpStatusCode!.Value;
            return;
        }

        await _next(ctx);
    }

    /// Policy:
    ///   1. X-Mock-Inject-PassThrough = "true" → never inject, even if Enabled.
    ///   2. Any X-Mock-Inject-Latency-Ms / X-Mock-Inject-Status header wins over
    ///      Options state — keeps contract tests deterministic without touching
    ///      global config.
    ///   3. Otherwise, when Options.Enabled, roll independent dice for latency
    ///      and 5xx. If Seed is set, the RNG is seeded from (Seed, TraceId)
    ///      so failures are reproducible from the trace id alone.
    internal static FaultDecision Decide(HttpContext ctx, FaultInjectionOptions opts)
    {
        var h = ctx.Request.Headers;
        if (h["X-Mock-Inject-PassThrough"] == "true") return FaultDecision.PassThrough;
        var hDelay = int.TryParse(h["X-Mock-Inject-Latency-Ms"], out var d) ? d : (int?)null;
        var hStatus = int.TryParse(h["X-Mock-Inject-Status"], out var s) ? s : (int?)null;
        if (hDelay is not null || hStatus is not null) return new FaultDecision(hDelay ?? 0, hStatus);
        if (!opts.Enabled) return FaultDecision.PassThrough;
        var rng = opts.Seed is int seed ? new Random(HashCode.Combine(seed, ctx.TraceIdentifier)) : Random.Shared;
        var delay = rng.NextDouble() < opts.LatencyProbability ? opts.LatencyMs : 0;
        int? status = rng.NextDouble() < opts.ServerErrorProbability ? opts.ServerErrorStatusCode : null;
        return new FaultDecision(delay, status);
    }
}
