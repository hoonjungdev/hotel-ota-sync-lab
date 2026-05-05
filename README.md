# Hotel OTA Sync Lab

> A channel-manager-style demo that decouples user-facing rate search from upstream OTA traffic.

**Status**: Work in progress (W1: scaffolding). Targeting first live demo at week 4 and a published case study at week 8.

## What this is

A reference backend that hotels can use to keep room Availability, Rates, and Inventory (ARI) in sync with multiple Online Travel Agencies — even when those OTAs disagree on protocol, reliability, and transactional semantics.

**Real-world analog**: similar problem space to integrations with Booking.com Connectivity, Expedia EQC, and Agoda YCS — but every OTA in this repo is a fictional simulator (`BlueWave`, `SkyTrip`, `GreenLeaf`) so nothing here ships proprietary code.

## Why it exists

Production OTA integrations often die on three rocks:

1. **Heterogeneous contracts** — XML vs JSON, push vs pull, per-OTA quirks.
2. **Unreliable upstreams** — partner outages cascade into user-facing failures.
3. **Transactional gaps** — multi-room atomicity is not universally supported.

This lab demonstrates a single architecture that addresses all three with measurable outcomes.

## Architecture

```
User → Property API → Redis (rate cache) → Postgres (truth)
                          ▲
                          │
            Sync Worker (BackgroundService)
                          │
                          ▼
                IChannelClient adapter
                          │
              ┌───────────┴───────────┐
        Mock BlueWave (XML)    Mock SkyTrip (JSON)
```

Diagram: see [`docs/architecture.png`](docs/architecture.png) (lands W7).

### Key decisions

- **Adapter pattern** around `IChannelClient`: a new OTA = one new class.
- **Background sync + Redis snapshot**: user reads never hit OTAs directly.
- **Capability flags** drive feature gating (e.g. `SupportsAtomicMultiRoom`).
- **Polly** for retries + circuit breaker; idempotent reservation ingestion.
- **Plain `BackgroundService`**, not Quartz/Hangfire — see [`docs/adr/`](docs/adr/).

## Stack

.NET 10 (LTS), ASP.NET Core (Minimal API + MVC), Dapper, PostgreSQL 16, Redis 7, Polly, Serilog → Elasticsearch + Kibana, xUnit + Testcontainers, Docker Compose v2.

## Quick start

> Requires Docker Compose v2 and .NET 10 SDK.

```bash
git clone https://github.com/hoonjungdev/hotel-ota-sync-lab
cd hotel-ota-sync-lab
docker compose -f deploy/docker-compose.yml up -d   # Postgres + Redis
dotnet build
```

The end-to-end demo (rate search, reservation ingestion, idempotency, fault injection) lands at week 4. The full case study with benchmark numbers ships at week 8 — see [`docs/case-study.md`](docs/case-study.md).

## Layout

```
src/
  HotelOtaSync.{Domain,Application,Infrastructure,Api,Worker}
  MockOta.{BlueWave,SkyTrip}
tests/
  HotelOtaSync.{UnitTests,IntegrationTests,Contract}
deploy/        docker-compose.yml
loadtest/      k6 scripts
docs/          case-study.md, benchmark.md, architecture.png, adr/
```

## License

MIT
