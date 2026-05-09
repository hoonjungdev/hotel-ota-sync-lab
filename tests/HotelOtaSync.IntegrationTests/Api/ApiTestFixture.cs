using HotelOtaSync.IntegrationTests.Cache;
using HotelOtaSync.IntegrationTests.Channels.BlueWave;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace HotelOtaSync.IntegrationTests.Api;

/// xUnit class fixture: one Testcontainers Redis + one in-memory MockOta.BlueWave +
/// one WebApplicationFactory&lt;HotelOtaSync.Api.ApiAssemblyMarker&gt;, shared across
/// the integration test class. Each test still partitions data by GUID-suffixed
/// hotel codes so cross-talk is impossible.
///
/// Wiring strategy:
/// - Redis: replace the Api's IConnectionMultiplexer singleton with the live
///   test container multiplexer. Bypasses the AddRedisRateCache factory so we
///   don't open a second connection. ConnectionString stays as a placeholder
///   to satisfy the startup validator.
/// - BlueWave: hook the Api's BlueWaveClient HttpClient onto the in-memory
///   MockOta.BlueWave handler (same pattern as BlueWaveAdapterFactory).
public sealed class ApiTestFixture : IAsyncLifetime
{
    public const string AdminToken = "test-admin-token-1234567890";

    private readonly RedisTestFixture _redis = new();
    private readonly BlueWaveTestFixture _bluewave = new();
    private WebApplicationFactory<HotelOtaSync.Api.ApiAssemblyMarker>? _factory;

    public IConnectionMultiplexer Redis => _redis.Multiplexer;
    public HttpClient Client => _factory!.CreateClient();

    public async Task InitializeAsync()
    {
        await _redis.InitializeAsync();
        // BlueWaveTestFixture has no async init; ctor wires it up.

        _factory = new WebApplicationFactory<HotelOtaSync.Api.ApiAssemblyMarker>()
            .WithWebHostBuilder(host =>
            {
                host.UseEnvironment("Development");

                host.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Channels:BlueWave:BaseUrl"] = "http://blue.invalid/",
                        ["Channels:BlueWave:PerAttemptTimeout"] = "00:00:02",
                        ["Channels:BlueWave:TotalRequestTimeout"] = "00:00:10",
                        ["Channels:BlueWave:MaxRetryAttempts"] = "2",
                        ["Channels:BlueWave:RetryBaseDelay"] = "00:00:00.010",
                        ["Channels:BlueWave:CircuitFailureRatio"] = "0.99",
                        ["Channels:BlueWave:CircuitMinimumThroughput"] = "100",
                        ["Channels:BlueWave:CircuitSamplingDuration"] = "00:00:30",
                        ["Channels:BlueWave:CircuitBreakDuration"] = "00:00:30",
                        // Placeholder — replaced by the test multiplexer below.
                        // Must be non-empty for AddRedisRateCache's startup validator.
                        ["Cache:Redis:ConnectionString"] = "test-placeholder:6379",
                        ["Cache:Redis:SnapshotTtl"] = "00:05:00",
                        ["Admin:Token"] = AdminToken,
                    });
                });

                host.ConfigureTestServices(services =>
                {
                    // Override the IConnectionMultiplexer registered by AddRedisRateCache
                    // with the live testcontainer multiplexer.
                    services.RemoveAll<IConnectionMultiplexer>();
                    services.AddSingleton<IConnectionMultiplexer>(_redis.Multiplexer);

                    // Pipe BlueWave outbound traffic through the in-memory mock.
                    services.AddHttpClient<HotelOtaSync.Infrastructure.Channels.BlueWave.BlueWaveClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => _bluewave.CreateHandler());
                });
            });

        // Force the host to materialise (triggers ValidateOnStart).
        _ = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        _bluewave.Dispose();
        await _redis.DisposeAsync();
    }
}
