using StackExchange.Redis;
using Testcontainers.Redis;

namespace HotelOtaSync.IntegrationTests.Cache;

/// xUnit class fixture: spins up a real Redis 7 container once per test
/// class via Testcontainers and exposes a live IConnectionMultiplexer.
///
/// Image is pinned to redis:7-alpine to match deploy/docker-compose.yml so
/// these tests prove the production cache path works against the same
/// engine version we ship.
public sealed class RedisTestFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine").Build();

    public IConnectionMultiplexer Multiplexer { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        Multiplexer = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        if (Multiplexer is not null) await Multiplexer.DisposeAsync();
        await _container.DisposeAsync();
    }
}
