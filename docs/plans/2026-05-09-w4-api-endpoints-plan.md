# W4 Part 2 — `HotelOtaSync.Api` Endpoints + Compose Service — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Activate `HotelOtaSync.Api` with the W4 demo endpoints (`GET /properties/{hotelCode}/rates` multi-channel envelope, `POST /admin/channels/{channel}/refresh` Bearer-protected), wire in a compose `api` service, and verify the **W4 demo** end to end: `docker compose up` → admin refresh → user GET serves from Redis with zero outbound OTA calls.

**Architecture:** Application gets one new use case (`GetCachedRatesQuery`) for the multi-channel cache fan-out — symmetry with Part 1's `ChannelRateRefresher`. The Api stays a thin Web adapter: endpoint groups, an `IEndpointFilter` for Bearer auth, and a `ChannelException` → ProblemDetails mapper. Compose adds the `api` service downstream of `redis` and `mock-bluewave`.

**Tech Stack:** .NET 10 Minimal API (Endpoint Groups + `IEndpointFilter`), `IOptions` + `IValidateOptions` (matches Part 1's pattern), `WebApplicationFactory<Program>` for integration tests (composes the existing `RedisTestFixture` + `BlueWaveTestFixture`), multi-stage Docker (mirrors `MockOta.BlueWave/Dockerfile`).

**Spec:** [`docs/design/2026-05-09-w4-api-endpoints.md`](../design/2026-05-09-w4-api-endpoints.md).
**Branch:** `feat/w4-api-endpoints` (already created; design doc is the first commit `628949f` + amendments `6814150`).

---

## File Structure

### Create

| Path | Responsibility |
|---|---|
| `src/HotelOtaSync.Application/Channels/GetCachedRatesQuery.cs` | Multi-channel cache fan-out use case |
| `src/HotelOtaSync.Api/Auth/AdminOptions.cs` | `Admin:Token` config binding |
| `src/HotelOtaSync.Api/Auth/AdminOptionsValidator.cs` | Boot-time guard: refuse production with dev token |
| `src/HotelOtaSync.Api/Auth/AdminTokenFilter.cs` | `IEndpointFilter` Bearer check |
| `src/HotelOtaSync.Api/Errors/ChannelExceptionMapper.cs` | `ChannelException.Kind` → HTTP status |
| `src/HotelOtaSync.Api/Endpoints/HealthEndpoints.cs` | `GET /health` (Redis ping) |
| `src/HotelOtaSync.Api/Endpoints/RatesEndpoints.cs` | `GET /properties/{hotelCode}/rates` |
| `src/HotelOtaSync.Api/Endpoints/AdminEndpoints.cs` | `POST /admin/channels/{channel}/refresh` |
| `src/HotelOtaSync.Api/ApiAssemblyMarker.cs` | Public type in `HotelOtaSync.Api` namespace; used as `WebApplicationFactory<T>` type parameter to avoid the global-namespace `Program` ambiguity (MockOta.BlueWave already declares `public partial class Program;`) |
| `src/HotelOtaSync.Api/Dockerfile` | Multi-stage build (mirrors MockOta.BlueWave) |
| `tests/HotelOtaSync.UnitTests/Channels/GetCachedRatesQueryTests.cs` | 3 unit tests |
| `tests/HotelOtaSync.UnitTests/Auth/AdminOptionsValidatorTests.cs` | 3 unit tests |
| `tests/HotelOtaSync.UnitTests/Auth/AdminTokenFilterTests.cs` | 3 unit tests |
| `tests/HotelOtaSync.IntegrationTests/Api/ApiTestFixture.cs` | Composes Redis + BlueWave + WebApplicationFactory |
| `tests/HotelOtaSync.IntegrationTests/Api/RatesEndpointIntegrationTests.cs` | 4 end-to-end tests |

### Modify

| Path | What changes |
|---|---|
| `src/HotelOtaSync.Api/Program.cs` | Full rewrite (currently `Hello World!`) — DI, middleware, endpoint groups, ProblemDetails, `public partial class Program` for tests |
| `src/HotelOtaSync.Api/appsettings.json` | Add Channels/Redis/Admin config defaults |
| `src/HotelOtaSync.Api/appsettings.Development.json` | Dev overrides + dev admin token |
| `deploy/docker-compose.yml` | Add `api` service entry |
| `README.md` | One-paragraph W4 demo runbook + token-override note |
| `CLAUDE.md` | Phase Marker → "W4 complete; W5 next" |

### Conventions

- All intermediate commits on `feat/w4-api-endpoints` use Conventional Commits prefixes (`feat:`, `test:`, `docs:`, `chore:`) — they squash on merge per project convention.
- After each task that touches code, run `dotnet build` (must be 0 warnings) and the relevant test subset. Run `dotnet format --verify-no-changes` before any commit that touches `.cs` files.
- `cd /Users/muho/hoonjungdev/hotel-ota-sync-lab` for all `dotnet` / `git` commands (the working directory drifts if you `cd` into a project subdir).

---

## Task 1: `GetCachedRatesQuery` (Application read use case)

**Files:**
- Create: `src/HotelOtaSync.Application/Channels/GetCachedRatesQuery.cs`
- Create: `tests/HotelOtaSync.UnitTests/Channels/GetCachedRatesQueryTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/HotelOtaSync.UnitTests/Channels/GetCachedRatesQueryTests.cs`:

```csharp
using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;

namespace HotelOtaSync.UnitTests.Channels;

public class GetCachedRatesQueryTests
{
    private static readonly DateRange TwoNights =
        new(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 3));

    private static RatePoint Point(DateOnly day, decimal amount) =>
        new(day, "DLX", "BAR", 5, new Money(amount, "USD"));

    [Fact]
    public async Task Execute_EmptyCache_EmitsEntryPerRegisteredChannelWithEmptyArray()
    {
        var channels = new IChannelClient[]
        {
            new StubChannel(ChannelCode.BlueWave),
            new StubChannel(ChannelCode.SkyTrip),
        };
        var cache = new InMemoryStubCache();

        var query = new GetCachedRatesQuery(channels, cache);
        var result = await query.ExecuteAsync("HOTEL-1", TwoNights, CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Empty(result[ChannelCode.BlueWave]);
        Assert.Empty(result[ChannelCode.SkyTrip]);
    }

    [Fact]
    public async Task Execute_PartialCache_OnlyFilledChannelsHaveData()
    {
        var channels = new IChannelClient[]
        {
            new StubChannel(ChannelCode.BlueWave),
            new StubChannel(ChannelCode.SkyTrip),
        };
        var cache = new InMemoryStubCache();
        await cache.SetAsync(
            ChannelCode.BlueWave, "HOTEL-1",
            new[] { Point(new DateOnly(2026, 6, 1), 100) },
            TimeSpan.FromMinutes(5), CancellationToken.None);

        var query = new GetCachedRatesQuery(channels, cache);
        var result = await query.ExecuteAsync("HOTEL-1", TwoNights, CancellationToken.None);

        Assert.Single(result[ChannelCode.BlueWave]);
        Assert.Empty(result[ChannelCode.SkyTrip]);
    }

    [Fact]
    public async Task Execute_DuplicateChannelRegistration_DeduplicatesByCode()
    {
        // TryAddEnumerable de-dupes by impl type, but if two adapters share a
        // ChannelCode we must NOT call the cache twice for the same key.
        var channels = new IChannelClient[]
        {
            new StubChannel(ChannelCode.BlueWave),
            new StubChannel(ChannelCode.BlueWave),
        };
        var cache = new InMemoryStubCache();

        var query = new GetCachedRatesQuery(channels, cache);
        var result = await query.ExecuteAsync("HOTEL-1", TwoNights, CancellationToken.None);

        Assert.Single(result);
        Assert.Contains(ChannelCode.BlueWave, result.Keys);
    }

    private sealed class StubChannel(ChannelCode code) : IChannelClient
    {
        public ChannelCode Channel { get; } = code;
        public ChannelCapabilities Capabilities { get; } = new(SupportsAtomicMultiRoom: false);
        public Task<IReadOnlyList<RatePoint>> PullAvailabilityAsync(AvailabilityQuery q, CancellationToken ct) =>
            throw new InvalidOperationException("Read use case must not call adapters");
        public Task<ReservationPushAck> PushReservationAsync(ReservationPush p, CancellationToken ct) =>
            throw new InvalidOperationException("Read use case must not call adapters");
    }

    private sealed class InMemoryStubCache : IRateSnapshotCache
    {
        private readonly Dictionary<(ChannelCode, string), List<RatePoint>> _data = new();

        public Task<IReadOnlyList<RatePoint>> GetAsync(ChannelCode channel, string hotelCode, DateRange stay, CancellationToken ct)
        {
            if (!_data.TryGetValue((channel, hotelCode), out var list))
                return Task.FromResult<IReadOnlyList<RatePoint>>(Array.Empty<RatePoint>());
            return Task.FromResult<IReadOnlyList<RatePoint>>(
                list.Where(p => p.StayDate >= stay.Start && p.StayDate < stay.End).ToList());
        }

        public Task SetAsync(ChannelCode channel, string hotelCode, IReadOnlyList<RatePoint> points, TimeSpan ttl, CancellationToken ct)
        {
            _data[(channel, hotelCode)] = points.ToList();
            return Task.CompletedTask;
        }

        public Task InvalidateAsync(ChannelCode channel, string hotelCode, CancellationToken ct)
        {
            _data.Remove((channel, hotelCode));
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2: Verify RED**

```bash
dotnet build tests/HotelOtaSync.UnitTests/HotelOtaSync.UnitTests.csproj 2>&1 | tail -10
```
Expected: build error — `GetCachedRatesQuery` type does not exist.

- [ ] **Step 3: Implement `GetCachedRatesQuery`**

`src/HotelOtaSync.Application/Channels/GetCachedRatesQuery.cs`:

```csharp
using HotelOtaSync.Domain;

namespace HotelOtaSync.Application.Channels;

/// Application-layer read use case for the user-facing rate query. Iterates
/// every registered IChannelClient (deduplicated by ChannelCode) and looks
/// each one up in IRateSnapshotCache concurrently. Always emits an entry per
/// registered channel — clients can rely on the response keys reflecting the
/// configured channel set, with empty arrays signalling "configured but no
/// cache data" (vs missing keys signalling "not configured at all").
///
/// Symmetry with ChannelRateRefresher (Part 1): write side pulls from one
/// adapter into the cache; this read side fans out from the cache across
/// every adapter the host knows about.
public sealed class GetCachedRatesQuery
{
    private readonly IReadOnlyList<ChannelCode> _channelCodes;
    private readonly IRateSnapshotCache _cache;

    public GetCachedRatesQuery(IEnumerable<IChannelClient> channels, IRateSnapshotCache cache)
    {
        _channelCodes = channels.Select(c => c.Channel).Distinct().ToList();
        _cache = cache;
    }

    public async Task<IReadOnlyDictionary<ChannelCode, IReadOnlyList<RatePoint>>> ExecuteAsync(
        string hotelCode,
        DateRange stay,
        CancellationToken cancellationToken)
    {
        var tasks = _channelCodes
            .Select(code => (code, task: _cache.GetAsync(code, hotelCode, stay, cancellationToken)))
            .ToArray();

        await Task.WhenAll(tasks.Select(t => t.task)).ConfigureAwait(false);

        var result = new Dictionary<ChannelCode, IReadOnlyList<RatePoint>>(_channelCodes.Count);
        foreach (var (code, task) in tasks)
        {
            result[code] = await task.ConfigureAwait(false);
        }
        return result;
    }
}
```

- [ ] **Step 4: Verify GREEN**

```bash
dotnet test tests/HotelOtaSync.UnitTests/HotelOtaSync.UnitTests.csproj \
  --filter "FullyQualifiedName~GetCachedRatesQueryTests" --nologo --verbosity quiet 2>&1 | tail -5
```
Expected: 3/3 pass.

- [ ] **Step 5: Format + commit**

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
git add src/HotelOtaSync.Application/Channels/GetCachedRatesQuery.cs \
        tests/HotelOtaSync.UnitTests/Channels/GetCachedRatesQueryTests.cs
git commit -m "feat: GetCachedRatesQuery (multi-channel cache fan-out)"
```

---

## Task 2: `AdminOptions` + `AdminOptionsValidator`

**Files:**
- Create: `src/HotelOtaSync.Api/Auth/AdminOptions.cs`
- Create: `src/HotelOtaSync.Api/Auth/AdminOptionsValidator.cs`
- Create: `tests/HotelOtaSync.UnitTests/Auth/AdminOptionsValidatorTests.cs`

The unit tests project doesn't reference `HotelOtaSync.Api` yet — Task 2 step 1.5 adds it.

- [ ] **Step 1: Add Api ProjectReference to UnitTests**

```bash
cd /Users/muho/hoonjungdev/hotel-ota-sync-lab
dotnet add tests/HotelOtaSync.UnitTests/HotelOtaSync.UnitTests.csproj \
  reference src/HotelOtaSync.Api/HotelOtaSync.Api.csproj
```

- [ ] **Step 2: Write failing tests**

`tests/HotelOtaSync.UnitTests/Auth/AdminOptionsValidatorTests.cs`:

```csharp
using HotelOtaSync.Api.Auth;
using Microsoft.Extensions.Hosting;

namespace HotelOtaSync.UnitTests.Auth;

public class AdminOptionsValidatorTests
{
    private static AdminOptionsValidator Validator(string envName) =>
        new(new StubHostEnv(envName));

    [Fact]
    public void Validate_ProductionEnvWithDevSentinelToken_Fails()
    {
        var result = Validator(Environments.Production)
            .Validate(name: null, options: new AdminOptions { Token = "INSECURE-DEV-ONLY-FOO-1234567890" });

        Assert.True(result.Failed);
        Assert.Contains("INSECURE-DEV-ONLY", result.FailureMessage);
    }

    [Fact]
    public void Validate_DevelopmentEnvWithDevSentinelToken_Succeeds()
    {
        var result = Validator(Environments.Development)
            .Validate(name: null, options: new AdminOptions { Token = "INSECURE-DEV-ONLY-FOO-1234567890" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_AnyEnvWithCustomToken_Succeeds()
    {
        var prod = Validator(Environments.Production)
            .Validate(name: null, options: new AdminOptions { Token = "real-rotated-token-please" });
        var dev = Validator(Environments.Development)
            .Validate(name: null, options: new AdminOptions { Token = "real-rotated-token-please" });

        Assert.True(prod.Succeeded);
        Assert.True(dev.Succeeded);
    }

    private sealed class StubHostEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
```

- [ ] **Step 3: Verify RED**

```bash
dotnet build tests/HotelOtaSync.UnitTests/HotelOtaSync.UnitTests.csproj 2>&1 | tail -10
```
Expected: build errors — `AdminOptions`, `AdminOptionsValidator` types do not exist.

- [ ] **Step 4: Implement AdminOptions**

`src/HotelOtaSync.Api/Auth/AdminOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace HotelOtaSync.Api.Auth;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// Bearer token required by every /admin/* endpoint. Bound from the
    /// "Admin:Token" configuration key. Validated on startup; see
    /// AdminOptionsValidator for the production-environment guard.
    [Required, MinLength(16)]
    public string Token { get; init; } = "";
}
```

- [ ] **Step 5: Implement AdminOptionsValidator**

`src/HotelOtaSync.Api/Auth/AdminOptionsValidator.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HotelOtaSync.Api.Auth;

/// Defense-in-depth boot-time guard: refuses startup when the host environment
/// is not Development AND the configured admin token still contains the
/// "INSECURE-DEV-ONLY" sentinel. Pairs with the loud-bad default in
/// docker-compose.yml so anyone shipping this past dev has to override the
/// token explicitly — the dev/prod gap is enforced in code, not just prose.
public sealed class AdminOptionsValidator(IHostEnvironment env) : IValidateOptions<AdminOptions>
{
    internal const string DevSentinel = "INSECURE-DEV-ONLY";

    public ValidateOptionsResult Validate(string? name, AdminOptions options)
    {
        if (!env.IsDevelopment() && options.Token.Contains(DevSentinel, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"Refusing to start in '{env.EnvironmentName}' environment with the dev " +
                $"admin token (contains '{DevSentinel}'). Set Admin:Token (e.g. via the " +
                "ADMIN_TOKEN env var) to a real rotated value.");
        }
        return ValidateOptionsResult.Success;
    }
}
```

- [ ] **Step 6: Verify GREEN**

```bash
dotnet test tests/HotelOtaSync.UnitTests/HotelOtaSync.UnitTests.csproj \
  --filter "FullyQualifiedName~AdminOptionsValidatorTests" --nologo --verbosity quiet 2>&1 | tail -5
```
Expected: 3/3 pass.

- [ ] **Step 7: Format + commit**

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
git add src/HotelOtaSync.Api/Auth/ \
        tests/HotelOtaSync.UnitTests/Auth/AdminOptionsValidatorTests.cs \
        tests/HotelOtaSync.UnitTests/HotelOtaSync.UnitTests.csproj
git commit -m "feat: AdminOptions + production-env validator (refuses dev token in prod)"
```

---

## Task 3: `AdminTokenFilter`

**Files:**
- Create: `src/HotelOtaSync.Api/Auth/AdminTokenFilter.cs`
- Create: `tests/HotelOtaSync.UnitTests/Auth/AdminTokenFilterTests.cs`

- [ ] **Step 1: Write failing tests**

`tests/HotelOtaSync.UnitTests/Auth/AdminTokenFilterTests.cs`:

```csharp
using HotelOtaSync.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace HotelOtaSync.UnitTests.Auth;

public class AdminTokenFilterTests
{
    private const string Token = "real-rotated-token-please";

    private static AdminTokenFilter Filter() =>
        new(Options.Create(new AdminOptions { Token = Token }));

    private static EndpointFilterInvocationContext ContextWith(string? authHeader)
    {
        var http = new DefaultHttpContext();
        if (authHeader is not null) http.Request.Headers.Authorization = authHeader;
        return new DefaultEndpointFilterInvocationContext(http);
    }

    [Fact]
    public async Task Invoke_ValidBearer_CallsNext()
    {
        var nextCalled = false;
        var result = await Filter().InvokeAsync(
            ContextWith($"Bearer {Token}"),
            _ => { nextCalled = true; return ValueTask.FromResult<object?>(Results.Ok()); });

        Assert.True(nextCalled);
        Assert.IsAssignableFrom<IResult>(result);
    }

    [Fact]
    public async Task Invoke_MissingHeader_Returns401()
    {
        var result = await Filter().InvokeAsync(
            ContextWith(null),
            _ => throw new InvalidOperationException("next must not be called"));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }

    [Fact]
    public async Task Invoke_MismatchedToken_Returns401()
    {
        var result = await Filter().InvokeAsync(
            ContextWith("Bearer wrong-token-value-here"),
            _ => throw new InvalidOperationException("next must not be called"));

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
    }
}
```

- [ ] **Step 2: Verify RED**

```bash
dotnet build tests/HotelOtaSync.UnitTests/HotelOtaSync.UnitTests.csproj 2>&1 | tail -5
```
Expected: build error — `AdminTokenFilter` does not exist.

- [ ] **Step 3: Implement AdminTokenFilter**

`src/HotelOtaSync.Api/Auth/AdminTokenFilter.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace HotelOtaSync.Api.Auth;

/// IEndpointFilter attached to the /admin route group. Validates the
/// Authorization: Bearer header against AdminOptions.Token using a constant-
/// time compare so timing analysis can't shrink the search space.
public sealed class AdminTokenFilter(IOptions<AdminOptions> options) : IEndpointFilter
{
    private readonly byte[] _expected = Encoding.UTF8.GetBytes(options.Value.Token);

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var header = context.HttpContext.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        if (!header.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Missing or malformed Authorization header.");
        }

        var presented = Encoding.UTF8.GetBytes(header[prefix.Length..]);

        if (presented.Length != _expected.Length ||
            !CryptographicOperations.FixedTimeEquals(presented, _expected))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid admin token.");
        }

        return await next(context);
    }
}
```

- [ ] **Step 4: Verify GREEN**

```bash
dotnet test tests/HotelOtaSync.UnitTests/HotelOtaSync.UnitTests.csproj \
  --filter "FullyQualifiedName~AdminTokenFilterTests" --nologo --verbosity quiet 2>&1 | tail -5
```
Expected: 3/3 pass.

- [ ] **Step 5: Format + commit**

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
git add src/HotelOtaSync.Api/Auth/AdminTokenFilter.cs \
        tests/HotelOtaSync.UnitTests/Auth/AdminTokenFilterTests.cs
git commit -m "feat: AdminTokenFilter (Bearer endpoint filter, constant-time compare)"
```

---

## Task 4: `ChannelExceptionMapper`

Small enough that an integration test (Task 11) covers it indirectly — no separate unit test.

**Files:**
- Create: `src/HotelOtaSync.Api/Errors/ChannelExceptionMapper.cs`

- [ ] **Step 1: Implement**

`src/HotelOtaSync.Api/Errors/ChannelExceptionMapper.cs`:

```csharp
using HotelOtaSync.Application.Channels;
using Microsoft.AspNetCore.Http;

namespace HotelOtaSync.Api.Errors;

/// Maps ChannelException.Kind to HTTP ProblemDetails. Used by both the admin
/// refresh endpoint and the global exception handler (Program.cs) so every
/// surface gets the same channel-failure semantics.
internal static class ChannelExceptionMapper
{
    public static IResult ToResult(ChannelException ex) => ex.Kind switch
    {
        ChannelFailureKind.BadRequest          => Problem(StatusCodes.Status400BadRequest,  ex),
        ChannelFailureKind.UpstreamServerError => Problem(StatusCodes.Status502BadGateway,  ex),
        ChannelFailureKind.Transport           => Problem(StatusCodes.Status502BadGateway,  ex),
        ChannelFailureKind.ProtocolError       => Problem(StatusCodes.Status502BadGateway,  ex),
        ChannelFailureKind.CircuitOpen         => Problem(StatusCodes.Status503ServiceUnavailable, ex),
        _                                      => Problem(StatusCodes.Status500InternalServerError, ex),
    };

    private static IResult Problem(int status, ChannelException ex) =>
        Results.Problem(
            statusCode: status,
            title: $"Channel '{ex.Channel}' failed ({ex.Kind})",
            detail: ex.Message);
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/HotelOtaSync.Api/HotelOtaSync.Api.csproj 2>&1 | tail -5
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Format + commit**

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
git add src/HotelOtaSync.Api/Errors/
git commit -m "feat: ChannelExceptionMapper (Kind -> HTTP ProblemDetails)"
```

---

## Task 5: `HealthEndpoints`

**Files:**
- Create: `src/HotelOtaSync.Api/Endpoints/HealthEndpoints.cs`

- [ ] **Step 1: Implement**

`src/HotelOtaSync.Api/Endpoints/HealthEndpoints.cs`:

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StackExchange.Redis;

namespace HotelOtaSync.Api.Endpoints;

internal static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (IConnectionMultiplexer redis, CancellationToken ct) =>
        {
            try
            {
                var db = redis.GetDatabase();
                var latency = await db.PingAsync();
                return Results.Ok(new { status = "ok", redisPingMs = latency.TotalMilliseconds });
            }
            catch (Exception ex)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Redis unreachable",
                    detail: ex.Message);
            }
        });
        return app;
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/HotelOtaSync.Api/HotelOtaSync.Api.csproj 2>&1 | tail -5
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Format + commit**

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
git add src/HotelOtaSync.Api/Endpoints/HealthEndpoints.cs
git commit -m "feat: GET /health (Redis ping)"
```

---

## Task 6: `RatesEndpoints` (`GET /properties/{hotelCode}/rates`)

**Files:**
- Create: `src/HotelOtaSync.Api/Endpoints/RatesEndpoints.cs`

- [ ] **Step 1: Implement**

`src/HotelOtaSync.Api/Endpoints/RatesEndpoints.cs`:

```csharp
using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HotelOtaSync.Api.Endpoints;

internal static class RatesEndpoints
{
    public static IEndpointRouteBuilder MapRatesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/properties/{hotelCode}/rates", async (
            string hotelCode,
            DateOnly from,
            DateOnly to,
            GetCachedRatesQuery query,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(hotelCode))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "hotelCode is required.");

            if (from >= to)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Query 'from' must be strictly before 'to' (half-open range).");

            var stay = new DateRange(from, to);
            var byChannel = await query.ExecuteAsync(hotelCode, stay, ct);

            return Results.Ok(new
            {
                channels = byChannel.ToDictionary(
                    kvp => kvp.Key.Value,
                    kvp => kvp.Value)
            });
        });

        return app;
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/HotelOtaSync.Api/HotelOtaSync.Api.csproj 2>&1 | tail -5
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Format + commit**

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
git add src/HotelOtaSync.Api/Endpoints/RatesEndpoints.cs
git commit -m "feat: GET /properties/{hotelCode}/rates (multi-channel envelope)"
```

---

## Task 7: `AdminEndpoints` (`POST /admin/channels/{channel}/refresh`)

**Files:**
- Create: `src/HotelOtaSync.Api/Endpoints/AdminEndpoints.cs`

`ChannelCode` parsing is inlined here as a small lookup (BlueWave/SkyTrip/GreenLeaf only). If a third channel adapter ever needs the same lookup, extract.

- [ ] **Step 1: Implement**

`src/HotelOtaSync.Api/Endpoints/AdminEndpoints.cs`:

```csharp
using HotelOtaSync.Api.Auth;
using HotelOtaSync.Api.Errors;
using HotelOtaSync.Application.Channels;
using HotelOtaSync.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HotelOtaSync.Api.Endpoints;

internal static class AdminEndpoints
{
    private static readonly Dictionary<string, ChannelCode> ByRouteSegment =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["bluewave"]  = ChannelCode.BlueWave,
            ["skytrip"]   = ChannelCode.SkyTrip,
            ["greenleaf"] = ChannelCode.GreenLeaf,
        };

    public sealed record RefreshRequest(string HotelCode, DateOnly From, DateOnly To);

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").AddEndpointFilter<AdminTokenFilter>();

        admin.MapPost("/channels/{channel}/refresh", async (
            string channel,
            RefreshRequest body,
            ChannelRateRefresher refresher,
            CancellationToken ct) =>
        {
            if (!ByRouteSegment.TryGetValue(channel, out var channelCode))
                return Results.Problem(statusCode: StatusCodes.Status404NotFound,
                    title: $"Unknown channel route segment '{channel}'.");

            if (string.IsNullOrWhiteSpace(body?.HotelCode))
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "hotelCode is required.");

            if (body.From >= body.To)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    title: "Body 'from' must be strictly before 'to' (half-open range).");

            try
            {
                var pointsCached = await refresher.RefreshAsync(
                    channelCode, body.HotelCode, new DateRange(body.From, body.To), ct);
                return Results.Ok(new { pointsCached });
            }
            catch (ChannelException ex)
            {
                return ChannelExceptionMapper.ToResult(ex);
            }
        });

        return app;
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/HotelOtaSync.Api/HotelOtaSync.Api.csproj 2>&1 | tail -5
```
Expected: 0 warnings, 0 errors.

- [ ] **Step 3: Format + commit**

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
git add src/HotelOtaSync.Api/Endpoints/AdminEndpoints.cs
git commit -m "feat: POST /admin/channels/{channel}/refresh (Bearer-protected)"
```

---

## Task 8: `Program.cs` rewrite + DI wiring + appsettings + assembly marker

This is the assembly task — Api first runs end to end after this. Three files modified/created together.

**Files:**
- Modify: `src/HotelOtaSync.Api/Program.cs`
- Modify: `src/HotelOtaSync.Api/appsettings.json`
- Modify: `src/HotelOtaSync.Api/appsettings.Development.json`
- Create: `src/HotelOtaSync.Api/ApiAssemblyMarker.cs`

- [ ] **Step 1: Create assembly marker**

`src/HotelOtaSync.Api/ApiAssemblyMarker.cs`:

```csharp
namespace HotelOtaSync.Api;

/// Public type used as the WebApplicationFactory<T> generic parameter in
/// integration tests. We can't use Program directly: top-level statements
/// place Program in the global namespace, and MockOta.BlueWave already
/// declares `public partial class Program;` there too — referring to
/// `Program` from the IntegrationTests project (which references both
/// hosts) would be ambiguous. This marker lives in HotelOtaSync.Api's
/// own namespace and is found by WebApplicationFactory's assembly probing.
public sealed class ApiAssemblyMarker;
```

- [ ] **Step 2: Rewrite Program.cs**

`src/HotelOtaSync.Api/Program.cs` (full content — overwrite):

```csharp
using HotelOtaSync.Api.Auth;
using HotelOtaSync.Api.Endpoints;
using HotelOtaSync.Application.Channels;
using HotelOtaSync.Infrastructure.Cache;
using HotelOtaSync.Infrastructure.Channels.BlueWave;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ---- options + validators ----
builder.Services.AddOptions<AdminOptions>()
    .Bind(builder.Configuration.GetSection(AdminOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<AdminOptions>, AdminOptionsValidator>();

// ---- channel adapters + cache ----
builder.Services.AddBlueWaveChannel(builder.Configuration);
builder.Services.AddRedisRateCache(builder.Configuration);

// ---- application use cases ----
builder.Services.AddSingleton<ChannelRateRefresher>();   // stateless, safe as singleton
builder.Services.AddSingleton<GetCachedRatesQuery>();    // stateless

// ---- problem details + endpoint filter ----
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<AdminTokenFilter>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapHealthEndpoints();
app.MapRatesEndpoints();
app.MapAdminEndpoints();

app.Run();

// Note: do NOT add `public partial class Program {}` here — that would
// re-introduce the global-namespace ambiguity with MockOta.BlueWave.
// Integration tests use `HotelOtaSync.Api.ApiAssemblyMarker` instead.
```

- [ ] **Step 3: Update appsettings.json (production-ish defaults; no real secrets)**

`src/HotelOtaSync.Api/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Channels": {
    "BlueWave": {
      "BaseUrl": "http://mock-bluewave:8080/",
      "PerAttemptTimeout": "00:00:02",
      "TotalRequestTimeout": "00:00:10",
      "MaxRetryAttempts": "2",
      "RetryBaseDelay": "00:00:00.200",
      "CircuitFailureRatio": "0.5",
      "CircuitMinimumThroughput": "20",
      "CircuitSamplingDuration": "00:00:30",
      "CircuitBreakDuration": "00:00:30"
    }
  },
  "Redis": {
    "ConnectionString": "redis:6379",
    "SnapshotTtl": "00:05:00"
  },
  "Admin": {
    "Token": ""
  }
}
```

- [ ] **Step 4: Update appsettings.Development.json (loud-bad dev token + localhost overrides)**

`src/HotelOtaSync.Api/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Channels": {
    "BlueWave": {
      "BaseUrl": "http://localhost:5101/"
    }
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Admin": {
    "Token": "INSECURE-DEV-ONLY-DO-NOT-USE-IN-PRODUCTION-12345678"
  }
}
```

- [ ] **Step 5: Build + boot smoke test**

Boot the Api locally with the dev settings (no compose yet — uses localhost URLs that may or may not have services attached; we just verify it starts).

```bash
cd /Users/muho/hoonjungdev/hotel-ota-sync-lab
dotnet build src/HotelOtaSync.Api/HotelOtaSync.Api.csproj 2>&1 | tail -5
```
Expected: 0 warnings, 0 errors.

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/HotelOtaSync.Api/HotelOtaSync.Api.csproj --no-build 2>&1 &
sleep 5
curl -s http://localhost:5000/health || echo "(health may 503 if redis is down — that's ok for this smoke step)"
kill %1 2>/dev/null; wait 2>/dev/null
```
Expected: process starts without exception (validators pass), `curl /health` either returns 200 or 503 (depending on whether Redis is running locally). Either is fine for this step — the goal is just to confirm the host doesn't crash on startup.

- [ ] **Step 6: Boot guard verification — production env must reject dev token**

```bash
cd /Users/muho/hoonjungdev/hotel-ota-sync-lab
ASPNETCORE_ENVIRONMENT=Production \
  Admin__Token=INSECURE-DEV-ONLY-DO-NOT-USE-IN-PRODUCTION-12345678 \
  Redis__ConnectionString=localhost:6379 \
  Channels__BlueWave__BaseUrl=http://localhost:5101/ \
  dotnet run --project src/HotelOtaSync.Api/HotelOtaSync.Api.csproj --no-build 2>&1 | head -20
```
Expected: process exits with `OptionsValidationException` mentioning "Refusing to start in 'Production' environment with the dev admin token". Confirms the guard fires.

- [ ] **Step 7: Format + commit**

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
git add src/HotelOtaSync.Api/Program.cs \
        src/HotelOtaSync.Api/ApiAssemblyMarker.cs \
        src/HotelOtaSync.Api/appsettings.json \
        src/HotelOtaSync.Api/appsettings.Development.json
git commit -m "feat: wire Program.cs (DI, ProblemDetails, endpoint groups, ApiAssemblyMarker)"
```

---

## Task 9: `Dockerfile`

**Files:**
- Create: `src/HotelOtaSync.Api/Dockerfile`

Mirror `src/MockOta.BlueWave/Dockerfile` exactly so the team has one pattern.

- [ ] **Step 1: Implement**

`src/HotelOtaSync.Api/Dockerfile`:

```dockerfile
# Build context = solution root (so all csproj files are visible for restore caching).
# Built from deploy/docker-compose.yml as: build: { context: .., dockerfile: src/HotelOtaSync.Api/Dockerfile }

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Copy only project files first to leverage Docker layer cache on dotnet restore.
COPY HotelOtaSync.sln ./
COPY src/HotelOtaSync.Domain/HotelOtaSync.Domain.csproj         src/HotelOtaSync.Domain/
COPY src/HotelOtaSync.Application/HotelOtaSync.Application.csproj src/HotelOtaSync.Application/
COPY src/HotelOtaSync.Infrastructure/HotelOtaSync.Infrastructure.csproj src/HotelOtaSync.Infrastructure/
COPY src/HotelOtaSync.Api/HotelOtaSync.Api.csproj               src/HotelOtaSync.Api/
RUN dotnet restore src/HotelOtaSync.Api/HotelOtaSync.Api.csproj

COPY src/HotelOtaSync.Domain/         src/HotelOtaSync.Domain/
COPY src/HotelOtaSync.Application/    src/HotelOtaSync.Application/
COPY src/HotelOtaSync.Infrastructure/ src/HotelOtaSync.Infrastructure/
COPY src/HotelOtaSync.Api/            src/HotelOtaSync.Api/
RUN dotnet publish src/HotelOtaSync.Api/HotelOtaSync.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=5 \
  CMD wget -qO- http://localhost:8080/health >/dev/null 2>&1 || exit 1

ENTRYPOINT ["dotnet", "HotelOtaSync.Api.dll"]
```

- [ ] **Step 2: Local docker build verify**

```bash
cd /Users/muho/hoonjungdev/hotel-ota-sync-lab
docker build -f src/HotelOtaSync.Api/Dockerfile -t hotel-ota-sync-api:dev . 2>&1 | tail -10
```
Expected: `Successfully tagged hotel-ota-sync-api:dev`. If it fails on missing files, fix the COPY layout.

- [ ] **Step 3: Commit**

```bash
git add src/HotelOtaSync.Api/Dockerfile
git commit -m "build: Dockerfile for HotelOtaSync.Api (multi-stage, mirrors MockOta.BlueWave)"
```

---

## Task 10: `ApiTestFixture` (composes Redis + BlueWave + WebApplicationFactory)

**Files:**
- Create: `tests/HotelOtaSync.IntegrationTests/Api/ApiTestFixture.cs`

Reuses the existing `RedisTestFixture` and `BlueWaveTestFixture` (don't duplicate). The combined fixture wires the Api against the test Redis and the in-memory BlueWave handler, exposes `HttpClient` for the Api.

- [ ] **Step 1: Implement**

`tests/HotelOtaSync.IntegrationTests/Api/ApiTestFixture.cs`:

```csharp
using HotelOtaSync.IntegrationTests.Cache;
using HotelOtaSync.IntegrationTests.Channels.BlueWave;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace HotelOtaSync.IntegrationTests.Api;

/// xUnit collection fixture: one Redis container + one BlueWave host +
/// one WebApplicationFactory<HotelOtaSync.Api.Program>, shared across the
/// integration test class. Each test still partitions data by GUID-suffixed
/// hotel codes so cross-talk is impossible.
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
        // BlueWaveTestFixture is sync ctor; nothing to await.

        _factory = new WebApplicationFactory<HotelOtaSync.Api.ApiAssemblyMarker>()
            .WithWebHostBuilder(host =>
            {
                host.UseEnvironment("Development");

                host.ConfigureAppConfiguration((_, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Channels:BlueWave:BaseUrl"]            = "http://blue.invalid/",
                        ["Channels:BlueWave:PerAttemptTimeout"]  = "00:00:02",
                        ["Channels:BlueWave:TotalRequestTimeout"] = "00:00:10",
                        ["Channels:BlueWave:MaxRetryAttempts"]   = "2",
                        ["Channels:BlueWave:RetryBaseDelay"]     = "00:00:00.010",
                        ["Channels:BlueWave:CircuitFailureRatio"] = "0.99",
                        ["Channels:BlueWave:CircuitMinimumThroughput"] = "100",
                        ["Channels:BlueWave:CircuitSamplingDuration"]  = "00:00:30",
                        ["Channels:BlueWave:CircuitBreakDuration"]     = "00:00:30",
                        ["Redis:ConnectionString"]   = ((ConnectionMultiplexer)_redis.Multiplexer).Configuration,
                        ["Redis:SnapshotTtl"]        = "00:05:00",
                        ["Admin:Token"]              = AdminToken,
                    });
                });

                host.ConfigureTestServices(services =>
                {
                    // Replace BlueWave's outbound HttpClient handler with the in-
                    // memory MockOta.BlueWave server, mirroring BlueWaveAdapterFactory.
                    services.AddHttpClient<HotelOtaSync.Infrastructure.Channels.BlueWave.BlueWaveClient>()
                        .ConfigurePrimaryHttpMessageHandler(() => _bluewave.CreateHandler());
                });
            });

        // Force the host to materialise.
        _ = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        _bluewave.Dispose();
        await _redis.DisposeAsync();
    }
}
```

> Note: `WebApplicationFactory<HotelOtaSync.Api.ApiAssemblyMarker>` is the disambiguator — see Task 8 Step 1. The marker type lives in the `HotelOtaSync.Api` assembly, which is what `WebApplicationFactory` actually uses to locate the entry point (it doesn't need the type to *be* `Program`; any public type from the target assembly works).

- [ ] **Step 2: Build**

```bash
dotnet build tests/HotelOtaSync.IntegrationTests/HotelOtaSync.IntegrationTests.csproj 2>&1 | tail -5
```
Expected: 0 errors. (Warnings about `Program` ambiguity should not occur once the namespace-qualified form is used.)

- [ ] **Step 3: Commit**

```bash
git add tests/HotelOtaSync.IntegrationTests/Api/ApiTestFixture.cs
git commit -m "test: ApiTestFixture composes Redis + BlueWave + WebApplicationFactory"
```

---

## Task 11: `RatesEndpointIntegrationTests` (4 end-to-end tests)

**Files:**
- Create: `tests/HotelOtaSync.IntegrationTests/Api/RatesEndpointIntegrationTests.cs`

TDD note: production code is now in place, so a normally-written test will pass on first run. To prove the full Api ↔ Redis ↔ BlueWave pipeline actually executed (same RED-probe pattern as `RedisRateSnapshotCacheIntegrationTests`), the FIRST test below uses `Assert.Equal(99, ...)` deliberately, then flips to GREEN.

- [ ] **Step 1: Write the 4 tests with RED probe on test #1**

`tests/HotelOtaSync.IntegrationTests/Api/RatesEndpointIntegrationTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace HotelOtaSync.IntegrationTests.Api;

[Collection("ApiIntegration")]
public class RatesEndpointIntegrationTests : IClassFixture<ApiTestFixture>
{
    private readonly ApiTestFixture _fixture;
    public RatesEndpointIntegrationTests(ApiTestFixture fixture) => _fixture = fixture;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpRequestMessage AdminRefresh(string channel, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/admin/channels/{channel}/refresh")
        {
            Content = JsonContent.Create(body)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiTestFixture.AdminToken);
        return req;
    }

    [Fact]
    public async Task RefreshThenGet_ServesFromRedis_NoOutboundOtaCallOnGet()
    {
        var hotel = $"HOTEL-{Guid.NewGuid():N}";

        var refresh = await _fixture.Client.SendAsync(AdminRefresh("bluewave", new
        {
            hotelCode = hotel,
            from = "2026-06-01",
            to = "2026-06-03",
        }));
        refresh.EnsureSuccessStatusCode();
        var refreshBody = await refresh.Content.ReadFromJsonAsync<JsonElement>();
        var pointsCached = refreshBody.GetProperty("pointsCached").GetInt32();

        var get = await _fixture.Client.GetAsync(
            $"/properties/{hotel}/rates?from=2026-06-01&to=2026-06-03");
        get.EnsureSuccessStatusCode();
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();

        var blue = body.GetProperty("channels").GetProperty("BlueWave").EnumerateArray().ToList();
        // RED probe — replace 99 with `pointsCached` after confirming the
        // pipeline ran (curl-equivalent via HttpClient → Api → Refresher →
        // BlueWave mock → Redis → GetCachedRatesQuery → response).
        Assert.Equal(99, blue.Count);
    }

    [Fact]
    public async Task Get_OnUnrefreshedHotel_AllRegisteredChannelsEmpty()
    {
        var hotel = $"HOTEL-{Guid.NewGuid():N}";
        var get = await _fixture.Client.GetAsync(
            $"/properties/{hotel}/rates?from=2026-06-01&to=2026-06-03");

        get.EnsureSuccessStatusCode();
        var body = await get.Content.ReadFromJsonAsync<JsonElement>();
        var channels = body.GetProperty("channels");

        Assert.True(channels.TryGetProperty("BlueWave", out var blue));
        Assert.Empty(blue.EnumerateArray());
    }

    [Fact]
    public async Task AdminRefresh_WithoutBearer_Returns401()
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/admin/channels/bluewave/refresh")
        {
            Content = JsonContent.Create(new { hotelCode = "X", from = "2026-06-01", to = "2026-06-02" })
        };
        var resp = await _fixture.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AdminRefresh_UnknownChannelSegment_Returns404()
    {
        var req = AdminRefresh("not-a-real-channel", new
        {
            hotelCode = "X",
            from = "2026-06-01",
            to = "2026-06-02",
        });
        var resp = await _fixture.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
```

- [ ] **Step 2: Verify RED on test #1, GREEN on the others**

```bash
dotnet test tests/HotelOtaSync.IntegrationTests/HotelOtaSync.IntegrationTests.csproj \
  --filter "FullyQualifiedName~RatesEndpointIntegrationTests" --nologo --verbosity normal 2>&1 | tail -30
```
Expected: 3/4 pass. The first test fails with `Assert.Equal() Failure: Expected: 99, Actual: <some real number, e.g. 8>`. The "Actual" value confirms the entire admin refresh → BlueWave mock → Redis → GET pipeline ran.

- [ ] **Step 3: Flip RED probe to GREEN**

In `RatesEndpointIntegrationTests.cs`, replace:

```csharp
Assert.Equal(99, blue.Count);
```

with:

```csharp
Assert.Equal(pointsCached, blue.Count);
Assert.All(blue, p => Assert.NotNull(p.GetProperty("stayDate").GetString()));
Assert.Contains(blue, p => p.GetProperty("roomTypeCode").GetString() == "DLX");
```

- [ ] **Step 4: Verify all GREEN**

```bash
dotnet test tests/HotelOtaSync.IntegrationTests/HotelOtaSync.IntegrationTests.csproj \
  --filter "FullyQualifiedName~RatesEndpointIntegrationTests" --nologo --verbosity quiet 2>&1 | tail -5
```
Expected: 4/4 pass.

- [ ] **Step 5: Format + commit**

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
git add tests/HotelOtaSync.IntegrationTests/Api/RatesEndpointIntegrationTests.cs
git commit -m "test: RatesEndpointIntegrationTests (4 e2e tests through ApiTestFixture)"
```

---

## Task 12: docker-compose `api` service

**Files:**
- Modify: `deploy/docker-compose.yml`

- [ ] **Step 1: Add the `api` service block**

Append to the `services:` map in `deploy/docker-compose.yml` (above the existing `volumes:` block):

```yaml
  api:
    build:
      context: ..
      dockerfile: src/HotelOtaSync.Api/Dockerfile
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      Channels__BlueWave__BaseUrl: http://mock-bluewave:8080/
      Channels__BlueWave__PerAttemptTimeout: "00:00:02"
      Channels__BlueWave__TotalRequestTimeout: "00:00:10"
      Channels__BlueWave__MaxRetryAttempts: "2"
      Redis__ConnectionString: redis:6379
      Redis__SnapshotTtl: "00:05:00"
      Admin__Token: ${ADMIN_TOKEN:-INSECURE-DEV-ONLY-DO-NOT-USE-IN-PRODUCTION-12345678}
    ports:
      - "5100:8080"
    depends_on:
      redis:
        condition: service_healthy
      mock-bluewave:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "wget -qO- http://localhost:8080/health >/dev/null 2>&1 || exit 1"]
      interval: 10s
      timeout: 3s
      retries: 5
      start_period: 15s
```

- [ ] **Step 2: Compose up smoke test (the W4 demo)**

```bash
cd /Users/muho/hoonjungdev/hotel-ota-sync-lab
docker compose -f deploy/docker-compose.yml up -d --build 2>&1 | tail -10
sleep 20
docker compose -f deploy/docker-compose.yml ps
```
Expected: all 4 services (`postgres`, `redis`, `mock-bluewave`, `api`) status `Up (healthy)`.

```bash
TOKEN=INSECURE-DEV-ONLY-DO-NOT-USE-IN-PRODUCTION-12345678
curl -s -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"hotelCode":"HOTEL-1","from":"2026-06-01","to":"2026-06-03"}' \
  http://localhost:5100/admin/channels/bluewave/refresh
```
Expected: `{"pointsCached": <N>}` with N > 0.

```bash
curl -s 'http://localhost:5100/properties/HOTEL-1/rates?from=2026-06-01&to=2026-06-03' | head -c 500
```
Expected: JSON with `{"channels":{"BlueWave":[{...}, ...]}}`. **W4 demo passes.**

```bash
docker compose -f deploy/docker-compose.yml down
```

- [ ] **Step 3: Commit**

```bash
git add deploy/docker-compose.yml
git commit -m "build: docker-compose api service (W4 demo wiring)"
```

---

## Task 13: README runbook

**Files:**
- Modify: `README.md`

Add one section: "Running the W4 demo" with the three-command sequence and the token-override note. Place it after the existing "Quick start" section (or wherever current README structure puts the closest equivalent — `Read` the file first to see).

- [ ] **Step 1: Read current README to find insertion point**

```bash
cat README.md | head -80
```

- [ ] **Step 2: Add runbook paragraph**

Insert (location: after the existing usage / quick-start section, before any "Architecture" or detailed sections):

```markdown
### Running the W4 demo

```bash
docker compose -f deploy/docker-compose.yml up -d --build

TOKEN=INSECURE-DEV-ONLY-DO-NOT-USE-IN-PRODUCTION-12345678
curl -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"hotelCode":"HOTEL-1","from":"2026-06-01","to":"2026-06-03"}' \
  http://localhost:5100/admin/channels/bluewave/refresh
# → {"pointsCached": N}

curl 'http://localhost:5100/properties/HOTEL-1/rates?from=2026-06-01&to=2026-06-03'
# → {"channels":{"BlueWave":[ ... rate points ... ]}}
```

The default `Admin__Token` in `deploy/docker-compose.yml` is intentionally
invalid-looking. The Api refuses to start in non-Development environments
with this sentinel token; override with the `ADMIN_TOKEN` env var (or a
`.env` file in the compose context) for any non-toy use.
```

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: README W4 demo runbook + admin token override note"
```

---

## Task 14: CLAUDE.md Phase Marker + final verification

**Files:**
- Modify: `CLAUDE.md` (Phase Marker section, lines around 72–76)

- [ ] **Step 1: Update Phase Marker**

Replace the current Phase Marker block (currently W4 in progress) with:

```markdown
## Phase Marker

> **Current: W4 — complete.** `HotelOtaSync.Api` activated with `GET /properties/{hotelCode}/rates` (multi-channel envelope, Redis-served, 0 outbound OTA calls), `POST /admin/channels/{channel}/refresh` (Bearer-protected, `ChannelRateRefresher` driven), `GET /health` (Redis ping). Compose now spins up postgres + redis + mock-bluewave + api healthy. Defense in depth on admin token: compose default `INSECURE-DEV-ONLY-DO-NOT-USE-IN-PRODUCTION-12345678`, `IValidateOptions` refuses startup when `ASPNETCORE_ENVIRONMENT != Development` AND token contains the sentinel. 48 tests: 31 unit (22 carry + 9 new), 12 integration (5 BlueWave + 3 Redis Testcontainers + 4 Api e2e), 5 contract.
>
> **Next: W5** — `Sync Worker` `BackgroundService` with `PullArisJob` (cron 1 min). Will reuse `ChannelRateRefresher` and (likely) `GetCachedRatesQuery` for staleness checks.
```

- [ ] **Step 2: Run full verification**

```bash
cd /Users/muho/hoonjungdev/hotel-ota-sync-lab
dotnet build 2>&1 | tail -5
```
Expected: 0 warnings, 0 errors.

```bash
dotnet test --nologo --verbosity quiet 2>&1 | tail -10
```
Expected: ~48/48 pass (exact count: 22 + 3 + 3 + 3 + new = ~31 unit, ~12 integration, 5 contract).

```bash
dotnet format --verify-no-changes 2>&1 | tail -3
```
Expected: clean.

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: Phase Marker — W4 complete; W5 next"
```

---

## Task 15: Push + open PR

- [ ] **Step 1: Push branch**

```bash
cd /Users/muho/hoonjungdev/hotel-ota-sync-lab
git push -u origin feat/w4-api-endpoints
```

- [ ] **Step 2: Open PR with self-validating Test plan checklist**

```bash
gh pr create --title "W4: Api endpoints + compose api service (W4 demo passes)" --body "$(cat <<'EOF'
## Summary
- Activates HotelOtaSync.Api end to end. Closes W4: \`docker compose up\` → \`POST /admin/channels/bluewave/refresh\` → \`GET /properties/{hotelCode}/rates\` returns multi-channel envelope from Redis with **zero outbound OTA calls** on the GET path.
- Adds Application read use case \`GetCachedRatesQuery\` (multi-channel cache fan-out, symmetric with Part 1's \`ChannelRateRefresher\`).
- Bearer admin auth via \`IEndpointFilter\` on the \`/admin\` route group; constant-time token compare; \`IValidateOptions\` refuses startup when \`ASPNETCORE_ENVIRONMENT != Development\` AND token contains the \`INSECURE-DEV-ONLY\` sentinel.
- ChannelException → ProblemDetails mapper (BadRequest 400 / UpstreamServerError|Transport|ProtocolError 502 / CircuitOpen 503).
- New \`api\` service in docker-compose (port 5100), depends on \`redis\` and \`mock-bluewave\` healthy.
- Design + amendments committed earlier on this branch ([\`628949f\`](docs/design/2026-05-09-w4-api-endpoints.md), \`6814150\`); implementation plan at \`docs/plans/2026-05-09-w4-api-endpoints-plan.md\`.

## Files
- \`src/HotelOtaSync.Application/Channels/GetCachedRatesQuery.cs\` (new)
- \`src/HotelOtaSync.Api/{Auth,Endpoints,Errors}/*\` + \`Program.cs\` rewrite (new + modified)
- \`src/HotelOtaSync.Api/Dockerfile\` (new, mirrors MockOta.BlueWave)
- \`src/HotelOtaSync.Api/appsettings*.json\` (Channels/Redis/Admin defaults; loud-bad dev token in Development)
- \`tests/HotelOtaSync.UnitTests/{Channels,Auth}/\` (9 unit tests)
- \`tests/HotelOtaSync.IntegrationTests/Api/\` (ApiTestFixture + 4 e2e tests)
- \`deploy/docker-compose.yml\` (api service)
- \`README.md\` (W4 demo runbook + token override note)
- \`CLAUDE.md\` (Phase Marker → W4 complete)

## Test plan
- [ ] \`dotnet test\` — ~48/48 pass (22 carry-over unit + 9 new unit + 8 integration carry-over + 4 new e2e + 5 contract)
- [ ] \`dotnet format --verify-no-changes\` — clean
- [ ] \`dotnet build\` — 0 warnings
- [ ] \`docker compose up -d --build\` — all 4 services healthy in < 90s
- [ ] curl admin refresh (with Bearer) → \`{"pointsCached": N>0}\`
- [ ] curl GET rates → \`{"channels":{"BlueWave":[...]}}\` non-empty
- [ ] Boot guard verified locally: \`ASPNETCORE_ENVIRONMENT=Production\` + dev token → \`OptionsValidationException\` at startup
- [ ] CI green on ubuntu-latest

## Phase Marker
\`Current: W4 — complete.\` Test count 35 → ~48; W4 demo passes; W5 next (Sync Worker BackgroundService).

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: After CI green, /review (or skip with reason in body), tick the Test plan boxes, then squash merge**

Per project convention. The PR is large enough to warrant `/review` — don't skip.

```bash
# After CI green and (optional) /review:
gh pr edit <PR#> --body  # tick verified Test plan boxes
gh pr merge <PR#> --squash --delete-branch
git checkout main && git pull --ff-only && git fetch --prune
```

---

## Verification Targets (W4 done = these all pass)

1. `docker compose up -d --build` → 4 services healthy in < 90s
2. `POST /admin/channels/bluewave/refresh` (Bearer) → 200 + `{"pointsCached": N>0}`
3. `GET /properties/{hotelCode}/rates?from=...&to=...` after the refresh → 200 + `{"channels":{"BlueWave":[...non-empty...]}}`
4. Same GET *without* a prior refresh → 200 + `{"channels":{"BlueWave":[]}}` (empty, not 404)
5. POST without Bearer → 401 ProblemDetails
6. POST with `{channel}` not in BlueWave/SkyTrip/GreenLeaf → 404 ProblemDetails
7. Boot guard: `ASPNETCORE_ENVIRONMENT=Production` + sentinel token → startup fails with `OptionsValidationException`
8. Total tests ~48 green; build 0 warnings; format clean

These map directly to Plan §6's W4 go/no-go checkpoint and §8 verification list (subset relevant to W4).
