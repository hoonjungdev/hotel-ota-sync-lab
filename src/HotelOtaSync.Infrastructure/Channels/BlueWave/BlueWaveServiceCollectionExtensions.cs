using HotelOtaSync.Application.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace HotelOtaSync.Infrastructure.Channels.BlueWave;

/// DI helper exposing the BlueWave adapter as IChannelClient. Worker / Api
/// hosts should call this once during startup; everything else (HttpClient,
/// Polly pipeline) is wired up here so callers stay channel-agnostic.
public static class BlueWaveServiceCollectionExtensions
{
    public static IServiceCollection AddBlueWaveChannel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<BlueWaveOptions>()
            .Bind(configuration.GetSection(BlueWaveOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<BlueWaveClient>((sp, http) =>
        {
            var opt = sp.GetRequiredService<IOptions<BlueWaveOptions>>().Value;
            http.BaseAddress = opt.BaseUrl;
            http.Timeout = opt.TotalRequestTimeout;
        }).AddResilienceHandler("blueWave-pipeline", (builder, ctx) =>
        {
            var opt = ctx.ServiceProvider.GetRequiredService<IOptions<BlueWaveOptions>>().Value;

            builder
                .AddTimeout(opt.PerAttemptTimeout)
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = opt.MaxRetryAttempts,
                    Delay = opt.RetryBaseDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = opt.CircuitFailureRatio,
                    MinimumThroughput = opt.CircuitMinimumThroughput,
                    SamplingDuration = opt.CircuitSamplingDuration,
                    BreakDuration = opt.CircuitBreakDuration,
                });
        });

        services.AddTransient<IChannelClient>(sp => sp.GetRequiredService<BlueWaveClient>());

        return services;
    }
}
