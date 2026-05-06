# Hotel OTA Sync Lab

> Upwork 포트폴리오 1호 — .NET 10 기반 채널 매니저-스타일 호텔 OTA 동기화 데모.

## Plan (Single source of truth)

전체 플랜: `/Users/muho/.claude/plans/users-muho-documents-roadmap-md-users-m-replicated-naur.md`

상세 결정·일정·산출물 정의는 항상 위 파일을 본다. 이 CLAUDE.md는 그 요약과 "절대 규칙"만.

## Identity

- **Goal**: Upwork 진입을 위한 첫 포트폴리오 산출물. Specialized Profile #1 ("Travel & Booking Systems Backend")의 첫 항목.
- **Tagline**: *"Channel-Manager-Style OTA Sync Lab — Decoupling User Traffic from Partner OTAs"*
- **Differentiator vs F-lab project**: F-lab `hotel-booking-platform`은 Java/Spring + MSA + 호텔↔고객 예약. 본 프로젝트는 .NET 10 + 모노리스 + 호텔(공급) ↔ 다중 OTA(채널). 의도적으로 다른 축.

## Hard Rules (절대 규칙)

1. **회사 IP 분리**: 트립토파즈에서 본 코드/스키마/네이밍/설정을 한 줄도 재사용 금지. 도메인 지식만 머리에서 인출. 케이스 스터디·README에 회사명·동료명·실제 파트너사명 일체 미기재.
2. **근무시간 회피**: 09–18 KST 본업 시간엔 본 저장소 작업·커밋 금지. 점심·저녁·주말만.
3. **개인 환경 분리**: GitHub `hoonjungdev`, 이메일 `hoonjungdev@gmail.com`(2026-05 해외 외주 진입에 맞춰 신원 단일화), 개인 머신에서만. 회사 계정·머신·네트워크 미접속.
4. **가짜 OTA 명명**: 모든 외부 채널은 가공명만 — `BlueWave Reservations`(XML), `SkyTrip Connect`(JSON), `GreenLeaf API`(stretch, 트랜잭션 미지원). 실제 OTA명(Booking.com/Expedia/Agoda)은 README의 "Real-world analog" 한 문장 외 미사용.

## Tech Stack (확정)

- **.NET 10 (LTS), C# 14** — Minimal API (Property API), MVC Controller (Mock OTA, XML 명시성)
- **PostgreSQL 16 + Dapper** (EF Core 미사용 — raw SQL 강점 노출 의도)
- **Redis 7** (StackExchange.Redis)
- **`BackgroundService`/`IHostedService`** — Quartz/Hangfire 미사용 (의도적 단순성, ADR로 명시)
- **`HttpClientFactory` + Polly** — retry, circuit breaker, timeout
- **Serilog → Elasticsearch + Kibana**
- **xUnit + WebApplicationFactory + Testcontainers**
- **Docker / docker compose v2**
- **GitHub Actions** (build + test + Trivy; 배포 없음)

## Architecture Spine

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

- 클린 아키텍처 4 레이어: `Domain` → `Application` → `Infrastructure` → `Web/Worker`. DIP로 어댑터는 `Infrastructure`에서만.
- `IChannelClient.Capabilities` 플래그로 `SupportsAtomicMultiRoom` 등 OTA별 능력 차이를 코드 레벨에서 인지.
- **트래픽 분리**: 사용자 요금 조회는 Redis만. Worker가 ARI를 pull/push. 사용자 → 외부 OTA 호출 0.

## Non-goals (의도적으로 안 하는 것)

- MSA / 분산 트랜잭션 (F-lab 프로젝트의 영역)
- 실제 Booking.com·Expedia sandbox 신청 (시간 낭비)
- 결제 처리, 다국어, 복잡한 권한 체계 (단순 admin 토큰 1개)
- CD (배포 자동화) — 포트폴리오 목적엔 과잉

## Repo Layout

```
src/  HotelOtaSync.{Domain,Application,Infrastructure,Api,Worker} + MockOta.{BlueWave,SkyTrip}
tests/  HotelOtaSync.{UnitTests,IntegrationTests,Contract}
deploy/  docker-compose.yml
loadtest/  rate-search.js (k6)
docs/  case-study.md, benchmark.md, architecture.png, adr/
```

## Phase Marker

> **Current: W3 — not started.** (W2 complete: `MockOta.BlueWave` v1 — `OTA_HotelAvailRQ/RS` + `OTA_HotelResNotifRQ/RS` 엔드포인트, fault injection 미들웨어(헤더 오버라이드 + seeded RNG), Dockerfile, compose 통합, Contract 테스트 5건 통과.)
> W3 task: `IChannelClient` 추상 + `BlueWaveClient` 어댑터 + Polly 재시도/회로차단 + 단위/통합 테스트. Plan §6 마일스톤 표 참조.

매주 첫 커밋 시 위 한 줄을 갱신할 것.

## How to Resume (새 세션에서)

1. `git status` + 가장 최근 커밋 3개 확인
2. Plan §6 마일스톤 표에서 현재 W? 항목 확인
3. 위 "Phase Marker"의 다음 작업으로 진입
4. 09–18 KST 시간대면 작업 보류 (점심·저녁·주말로 미룸)

## Verification Targets (W8 종료 시)

- `docker compose up` 90초 내 healthy
- E2E 시나리오 (rate search + reservation + 멱등성) 모두 통과
- k6 100 RPS 60s, 에러율 0%, P95 < 80ms
- OCP 시연 PR (`add-greenleaf-adapter`): 기존 채널 디렉터리 변경 라인 0
- 영문 케이스 스터디 600단어 ±10% — Upwork 포트폴리오 1호 등록
