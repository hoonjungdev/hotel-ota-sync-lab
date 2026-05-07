using HotelOtaSync.Application.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace HotelOtaSync.Infrastructure.Cache;

public static class RedisRateCacheServiceCollectionExtensions
{
    /// Wires the Redis-backed IRateSnapshotCache and a singleton
    /// IConnectionMultiplexer. Validates that ConnectionString is set at
    /// startup so deployments fail fast instead of crashing on the first
    /// cache call.
    public static IServiceCollection AddRedisRateCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RedisRateCacheOptions>()
            .Bind(configuration.GetSection(RedisRateCacheOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                $"{RedisRateCacheOptions.SectionName}:ConnectionString must be set.")
            .Validate(o => o.SnapshotTtl > TimeSpan.Zero,
                $"{RedisRateCacheOptions.SectionName}:SnapshotTtl must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<RedisRateCacheOptions>>().Value;
            return ConnectionMultiplexer.Connect(opt.ConnectionString!);
        });

        services.AddSingleton<IRateSnapshotCache, RedisRateSnapshotCache>();

        return services;
    }
}
