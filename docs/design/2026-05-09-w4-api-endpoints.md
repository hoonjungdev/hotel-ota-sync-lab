# W4 — `HotelOtaSync.Api` endpoints + docker-compose `api` service

> Status: design approved 2026-05-09. Implementation lands on `feat/w4-api-endpoints`.
> Plan reference: `/Users/muho/.claude/plans/users-muho-documents-roadmap-md-users-m-replicated-naur.md` §6 W4, §8.2.

## Goal

Close out W4 by adding the user-facing rate query endpoint, the admin refresh endpoint, and a docker-compose `api` service so the **W4 demo** runs end to end:

```
docker compose up
  → POST /admin/channels/bluewave/refresh   (seeds the cache)
  → GET  /properties/{hotelCode}/rates       (Redis-served, no OTA call)
```

This is the go/no-go checkpoint Plan §6 calls out.

## Decisions (from brainstorming)

| # | Question | Decision | Why |
|---|---|---|---|
| Q1 | GET endpoint scope (channel handling) | **B** — multi-channel envelope `{"channels":{"BlueWave":[...],"SkyTrip":[]}}` | Case-study narrative ("decoupling user traffic from N OTAs") shows up in the response shape itself. Empty SkyTrip slot is a visible placeholder for W6. |
| Q2 | Cache miss / hotel-not-exist policy | **A** — always 200, every registered channel always present (empty array on miss); no hotel registry lookup | Postgres property-table read is out of W4 scope; predictable response shape; no special-case 404 logic. |
| Q3 | Admin endpoint shape | Bearer token (`Admin:Token` config, ≥16 chars), JSON body `{hotelCode, from, to}`, response `{pointsCached: N}`, `ChannelException`-driven 4xx/5xx | Most RESTful, 1:1 maps to existing `ChannelException.Kind` from W3. |
| approach | Where does multi-channel fan-out live? | **Application-layer use case** (`GetCachedRatesQuery`) | Symmetry with `ChannelRateRefresher` (Part 1). Reusable by W5 worker. Endpoint stays an HTTP-only adapter. |

## Architecture

```
Web layer (HotelOtaSync.Api)                                    Application
  Program.cs                                                      Channels/
    DI + middleware + endpoint groups                               GetCachedRatesQuery.cs   ← NEW (read use case)
  Endpoints/
    RatesEndpoints.cs        GET /properties/{hotelCode}/rates    Existing (unchanged):
    AdminEndpoints.cs        POST /admin/channels/{channel}/refresh ChannelRateRefresher
    HealthEndpoints.cs       GET /health (Redis ping only)         IRateSnapshotCache
  Auth/                                                             IChannelClient + ChannelCode
    AdminTokenFilter.cs      IEndpointFilter on /admin group
    AdminOptions.cs          [Required, MinLength(16)] Token
  Errors/
    ChannelExceptionMapper.cs   ChannelException → ProblemDetails
  Dockerfile                 multi-stage SDK→ASP.NET runtime

Infrastructure / Domain      unchanged from W4 Part 1
```

Endpoint organisation: `app.MapGroup("/admin").AddEndpointFilter<AdminTokenFilter>()` — Minimal API standard pattern, scales as W5/W6 add more `/admin` endpoints. Extension methods (`MapRatesEndpoints`, `MapAdminEndpoints`, `MapHealthEndpoints`) keep `Program.cs` slim.

## Components

### `GetCachedRatesQuery` (Application — new)

```csharp
public sealed class GetCachedRatesQuery(
    IEnumerable<IChannelClient> channels,
    IRateSnapshotCache cache)
{
    /// Always emits an entry per registered channel — empty array on cache miss.
    /// Fan-out is parallel via Task.WhenAll; cache hits are independent per channel.
    public Task<IReadOnlyDictionary<ChannelCode, IReadOnlyList<RatePoint>>> ExecuteAsync(
        string hotelCode, DateRange stay, CancellationToken ct);
}
```

Iterates `channels.Select(c => c.Code).Distinct()`, calls `cache.GetAsync` per channel concurrently, assembles the dictionary. The "always emits an entry per registered channel" invariant is the *public contract*: clients can assume the response keys reflect the configured channel set.

### `AdminTokenFilter` (Api)

`IEndpointFilter` attached to the `/admin` route group.

- Reads `Authorization: Bearer <token>` header.
- Compares to `IOptions<AdminOptions>.Value.Token` via `CryptographicOperations.FixedTimeEquals` (timing-attack safe).
- Missing/invalid → `Results.Problem(statusCode: 401, ...)`.

### `AdminOptions`

```csharp
public sealed class AdminOptions
{
    [Required, MinLength(16)]
    public string Token { get; init; } = "";
}
```

Bound from `"Admin"` config section. Validated on startup with the same `OptionsValidationException` pattern Part 1 settled on for `RedisRateCacheOptions`. A misconfigured deployment fails fast at boot, not on first admin call.

### `ChannelExceptionMapper`

Centralises the `ChannelException` → HTTP mapping so both endpoints (and any future `/admin/*` that calls a channel adapter) get consistent ProblemDetails responses.

## Data flow

### `GET /properties/{hotelCode}/rates?from=2026-06-01&to=2026-06-08`

1. Parse `from` / `to` as `DateOnly`. Validate `from < to` (else 400).
2. `GetCachedRatesQuery.ExecuteAsync(hotelCode, new DateRange(from, to), ct)`.
3. Project to JSON envelope and return 200:
   ```json
   {
     "channels": {
       "BlueWave": [
         {
           "stayDate": "2026-06-01",
           "roomTypeCode": "DLX",
           "ratePlanCode": "BAR",
           "available": 5,
           "total": { "amount": 100, "currency": "USD" }
         }
       ],
       "SkyTrip": []
     }
   }
   ```
4. **Outbound OTA calls = 0.** Redis is the only data source.

### `POST /admin/channels/{channel}/refresh`

```http
POST /admin/channels/bluewave/refresh
Authorization: Bearer <token>
Content-Type: application/json

{ "hotelCode": "HOTEL-1", "from": "2026-06-01", "to": "2026-06-08" }
```

1. `AdminTokenFilter` validates Bearer token (group filter — automatic).
2. Route param `{channel}` parsed to `ChannelCode` via an explicit lookup table: `{ "bluewave" → ChannelCode.BlueWave, "skytrip" → ChannelCode.SkyTrip, "greenleaf" → ChannelCode.GreenLeaf }` (case-insensitive). `ChannelCode` is `record struct(string Value)` with no built-in parser, so the lookup lives in the endpoint or a small `ChannelCodeRouteResolver` helper. Unknown channel → 404.
3. Body deserialised. Validation:
   - `hotelCode`: non-empty
   - `from`, `to`: valid `DateOnly`, `from < to`
   - All failures → 400 ProblemDetails.
4. `ChannelRateRefresher.RefreshAsync(channel, hotelCode, new DateRange(from, to), ct)` — Refresher takes flat `(channel, hotelCode, stay, ct)` parameters, not an `AvailabilityQuery` wrapper. The wrapper exists only for `IChannelClient.PullAvailabilityAsync` further down the call stack (Refresher constructs it internally).
5. 200 + `{"pointsCached": N}` on success. `ChannelException` → mapper.

TTL is **not** in the body — it comes from `RedisRateCacheOptions.SnapshotTtl` config (default 5 min, set in Part 1).

### `GET /health`

Pings Redis (`IConnectionMultiplexer.GetDatabase().PingAsync()`). Returns 200 if reachable, 503 otherwise. Used as the docker-compose `healthcheck` target.

Not checking BlueWave availability: BlueWave being down is a degraded-but-functional state for the user-facing rate query (Redis still serves cached data). Wiring BlueWave health into this would couple liveness to a partner that the cache exists *to decouple from*.

## Error handling

| Source | HTTP |
|---|---|
| Bad query / body | 400 ProblemDetails |
| Missing / wrong admin token | 401 ProblemDetails |
| Unknown `{channel}` route param | 404 |
| `ChannelException(BadRequest)` (e.g., unregistered channel in Refresher) | 400 |
| `ChannelException(UpstreamServerError)` (Polly retries exhausted) | 502 |
| `ChannelException(CircuitOpen)` | 503 |
| Anything else | 500 (default `UseExceptionHandler`) |

Wired with `builder.Services.AddProblemDetails()` + `app.UseExceptionHandler()`. All error responses are RFC 7807.

## Testing strategy

| Level | Test file | What it pins |
|---|---|---|
| Unit | `GetCachedRatesQueryTests` | Empty cache → all registered channels return empty arrays. Partial cache → only filled channels show data. Channel enumeration deduplicates if DI registers the same code twice. (~3 tests) |
| Unit | `AdminTokenFilterTests` | Valid token passes through; missing header → 401; mismatched token → 401. (~3 tests) |
| Integration | `RatesEndpointIntegrationTests` (uses `WebApplicationFactory<HotelOtaSync.Api.Program>` + existing `RedisTestFixture` + existing `BlueWaveTestFixture`) | GET happy path returns `{channels:{BlueWave:[...],SkyTrip:[]}}` after seeding cache. GET on unrefreshed hotel → all channels empty. POST without Bearer → 401. POST end-to-end: admin refresh → BlueWave mock pull → Redis populated → GET sees it. (~4 tests) |

Expected total: 35 → ~45.

`Program` partial class issue (already noted in `BlueWaveTestFixture`): `HotelOtaSync.Api` and `MockOta.BlueWave` both declare global `Program`. New fixture references `HotelOtaSync.Api.Program` explicitly to disambiguate.

## Compose service

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
    Admin__Token: ${ADMIN_TOKEN:-dev-token-change-me-please-1234}
  ports:
    - "5100:8080"
  depends_on:
    redis: { condition: service_healthy }
    mock-bluewave: { condition: service_healthy }
  healthcheck:
    test: ["CMD-SHELL", "wget -qO- http://localhost:8080/health >/dev/null 2>&1 || exit 1"]
    interval: 10s
    timeout: 3s
    retries: 5
    start_period: 15s
```

`HotelOtaSync.Api/Dockerfile`: multi-stage, mirrors `MockOta.BlueWave/Dockerfile` pattern. SDK image restores + publishes; runtime image (ASP.NET) runs the published output.

Postgres is **not** wired into `api` — Plan W4 doesn't need it. Will land in W5 when the Worker arrives.

## Out of scope (explicit)

- Postgres property-table reads (Q2 decision A — deferred to W5+)
- README W4 demo runbook (lands separately or in this PR's last commit; not part of the design)
- OpenAPI / Swagger UI (no Plan requirement; scope creep)
- HTTPS / certs (compose internal HTTP only; demo)
- Rate limiting / CORS (no Plan requirement)

## PR shape (anticipated)

Single PR `feat/w4-api-endpoints` covering all of the above. Estimated diff: ~600–800 LOC including tests + Dockerfile + compose entry + design doc. Demo verification:

```bash
docker compose up -d
TOKEN=dev-token-change-me-please-1234
curl -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"hotelCode":"HOTEL-1","from":"2026-06-01","to":"2026-06-08"}' \
  http://localhost:5100/admin/channels/bluewave/refresh
# → {"pointsCached":...}
curl 'http://localhost:5100/properties/HOTEL-1/rates?from=2026-06-01&to=2026-06-08'
# → {"channels":{"BlueWave":[...],"SkyTrip":[]}}
```

This is the W4 go/no-go checkpoint per Plan §6.
