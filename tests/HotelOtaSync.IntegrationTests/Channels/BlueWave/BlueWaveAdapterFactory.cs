using HotelOtaSync.Application.Channels;
using HotelOtaSync.Infrastructure.Channels.BlueWave;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;

namespace HotelOtaSync.IntegrationTests.Channels.BlueWave;

internal static class BlueWaveAdapterFactory
{
    /// Builds a real BlueWave adapter (with the Polly pipeline configured by
    /// AddBlueWaveChannel) wired to the in-memory MockOta.BlueWave handler.
    /// `outboundHandler`, when supplied, is inserted just outside the
    /// resilience pipeline so retries pass through it on every attempt.
    public static (ServiceProvider sp, IChannelClient channel) Build(
        BlueWaveTestFixture fixture,
        IDictionary<string, string?>? overrides = null,
        DelegatingHandler? outboundHandler = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Channels:BlueWave:BaseUrl"] = "http://blue.invalid/",
            ["Channels:BlueWave:PerAttemptTimeout"] = "00:00:02",
            ["Channels:BlueWave:TotalRequestTimeout"] = "00:00:10",
            ["Channels:BlueWave:MaxRetryAttempts"] = "2",
            ["Channels:BlueWave:RetryBaseDelay"] = "00:00:00.010",
            // Keep the breaker out of the way for retry-focused tests.
            ["Channels:BlueWave:CircuitMinimumThroughput"] = "100",
            ["Channels:BlueWave:CircuitFailureRatio"] = "0.99",
        };
        if (overrides is not null)
        {
            foreach (var (k, v) in overrides) settings[k] = v;
        }

        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddBlueWaveChannel(config);

        // Replace the primary handler with the in-memory mock's handler. This
        // sits at the bottom of the HttpClient pipeline (below Polly), so the
        // resilience strategy still drives retries above it.
        services.AddHttpClient<BlueWaveClient>()
            .ConfigurePrimaryHttpMessageHandler(() => fixture.CreateHandler());

        if (outboundHandler is not null)
        {
            // Register the handler as a transient and add it to the pipeline.
            // We register a factory so the same instance can be observed by
            // the test for assertion.
            services.TryAddEnumerable(ServiceDescriptor.Transient<IHttpMessageHandlerBuilderFilter, NoopFilter>());
            services.AddHttpClient<BlueWaveClient>()
                .AddHttpMessageHandler(() => outboundHandler);
        }

        var sp = services.BuildServiceProvider();
        var channel = sp.GetRequiredService<IChannelClient>();
        return (sp, channel);
    }

    /// Filter required only because AddHttpMessageHandler historically needs
    /// the builder pipeline to be initialised. Kept as a no-op.
    private sealed class NoopFilter : IHttpMessageHandlerBuilderFilter
    {
        public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) => next;
    }
}
