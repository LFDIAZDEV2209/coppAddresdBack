# Verification Report — api-gateway-yarp

**Change**: api-gateway-yarp
**Mode**: Strict TDD (runner: `dotnet test`)
**Date**: 2026-08-24
**Verdict**: READY-WITH-CAVEATS

## Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 43 |
| Tasks checked in tasks.md | 6 |
| Tasks unchecked in tasks.md | 37 |
| Tasks implemented on disk (verified) | ~36 of the unchecked are implemented on disk |
| Tasks manual/Docker-dependent | 2.4, 3.7, 4.5 (automated in integ), 5.4 (unit only), 8.2, 9.1, 9.2, 9.4, 9.6, 10.4 |

> The persisted `tasks.md` is STALE: apply phase marked only Stream C (7.1,7.2,7.3,8.1,10.1,10.2) as `[x]`. The gateway Stream A (Phases 1-5), frontend (Phase 6) and corrective fixes were implemented on disk and verified here, but their checkboxes were never marked (apply noted ownership limits preventing tasks.md edits). Verify/archive must reconcile these stale checkboxes against apply-progress + this report.

## Build & Tests Execution

**Build**: ✅ Passed — `dotnet build CoppAddresd.slnx` → 0 errors, 13 warnings (pre-existing NU1504/NU1510/MSB3277, unrelated).

```text
Build succeeded. 13 Warning(s) 0 Error(s)
```

**Tests** (per project):

| Project | Result |
|---------|--------|
| CoppAddresd.Gateway.UnitTests | ✅ 14/14 passed |
| CoppAddresd.Gateway.IntegrationTests | ✅ 8/8 passed |
| CoppAddresd.UnitTests | ✅ 286/286 passed |
| CoppAddresd.IntegrationTests | ✅ 10/10 passed |
| CoppAddresd.Telemedicine.UnitTests | ✅ 134/134 passed |
| CoppAddresd.Telemedicine.IntegrationTests | ❌ 0/25 passed (all fail — requires PostgreSQL/`COP_TEST_DB_CONNECTION`, not set) |

**Coverage**: ➖ Not available (no coverage tool configured).

**Frontend**: `tsc --noEmit` → exit 0 (no type errors). No stale env-var/port references in code (only internal skill doc `self-hosting.md`, non-code).
**Mobile**: `npm run build` → exit 0 (only pre-existing chunk-size warning). Vite proxy → `http://localhost:5080`; no stale 5122/5123/5130 refs.

## Spec Compliance Matrix

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| REQ-GW-001 (puerto único 5080) | Arranque | launchSettings/appsettings `Urls: http://localhost:5080`; Gateway.IntegrationTests via `WebApplicationFactory` | ✅ COMPLIANT (config) |
| REQ-GW-002 (ruteo por prefijo) | Login OTP / Chat stream | `GatewayRoutingTests.AuthRoute_ForwardsToAuthCluster`, `.ApiRoute_ForwardsToApiCluster`, `.TelemedicineRoute_ForwardsToTelemedicineCluster`, `.TelemedicineRoute_WinsOverApiCatchAll` | ✅ COMPLIANT |
| REQ-GW-003 (preservar Authorization/Cookie) | Bearer JWT | `ForwardsAuthorizationInternalKeyAndCookieHeaders` | ✅ COMPLIANT |
| REQ-GW-004 (preservar cookie refresh) | Refresh con cookie | Same header test; `ResponseHeadersCopy` default (no transforms) | ✅ COMPLIANT (config) |
| REQ-GW-005 (CORS centralizado) | Preflight | CORS policy in Program.cs (AllowCredentials + WithExposedHeaders); backends whitelist 5080 | ✅ COMPLIANT (config); no preflight test → ⚠️ PARTIAL |
| REQ-GW-006 (bloqueo internals) | Internal sin header / con clave | `InternalKeyMiddlewareTests` (11) + `InternalEndpoint_WithoutKey_Returns404BeforeProxy` + `InternalEndpoint_WithValidKey_ForwardsToAuthCluster` | ✅ COMPLIANT |
| REQ-GW-007 (SSE sin cortes) | Chat stream 60 s | `ChatStream_FlowsIncrementally_WithoutBuffering` (non-buffering, ~500ms) | ⚠️ PARTIAL (non-buffering automated; 60 s duration NOT tested) |
| REQ-GW-008 (webhook Twilio HMAC) | Webhook Twilio aceptado | (none — no Twilio RequestValidator test) | ❌ UNTESTED |
| REQ-GW-009 (health check no auth) | Health check | `HealthResponseWriterTests` (3: all healthy 200 / degraded 503) | ⚠️ PARTIAL (unit only; no /health endpoint E2E) |
| REQ-GW-010 (frontend env única) | Env var única | env.ts `apiUrl`; grep sin refs; tsc pass | ✅ COMPLIANT (static) |
| REQ-GW-011 (móvil → gateway) | Móvil en dev | vite.config proxy /api→5080; capacitor server.url; build pass | ✅ COMPLIANT (static) |
| REQ-GW-012 (doc migración AWS) | ADR split documentado | `docs/architecture/gateway.md` (mapping, SSE split ALB/CloudFront, JWT authorizer, usage plans+keys, cookie same-domain) | ✅ COMPLIANT |

**Compliance summary**: 8/12 fully compliant, 2 partial (REQ-GW-007 60s, REQ-GW-009 E2E), 1 untested (REQ-GW-008), 1 config-compliant-but-notest (REQ-GW-005 preflight).

## Scenario Coverage

| Scenario | Cobertura | Clasificación |
|----------|-----------|---------------|
| S-01 Login OTP vía gateway | Routing auth cluster automated; full OTP+cookie flow needs real Auth/Twilio | MANUAL |
| S-02 Refresh cookie rotación | Cookie/header forwarding automated; rotation + corrupt-cookie 401 E2E manual | MANUAL |
| S-03 Chat stream SSE 60 s | SSE non-buffering automated (500ms); 60 s duration NOT covered | AUTOMATED ⚠️ |
| S-04 Webhook Twilio HMAC | No test (no Twilio RequestValidator) | NOT_COVERED |
| S-05 Internal sin header → 404 | Automated (integration + unit) | AUTOMATED |
| S-06 Frontend env única | Static (env.ts + tsc + grep) | MANUAL |
| S-07 Móvil proxy gateway | Static (vite/capacitor config + build) | MANUAL |
| S-08 Health check gateway | Unit (writer logic); no /health endpoint E2E | AUTOMATED ⚠️ |
| S-09 CORS preflight | No test | NOT_COVERED |
| S-10 Móvil + cookie Capacitor prod | No test (needs device) | NOT_COVERED |

## TDD Compliance (Strict TDD)

| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | ⚠️ | apply-progress #321/#322 report RED/GREEN/tests written; no formal "TDD Cycle Evidence" table found |
| All tasks have tests | ❌ | 14 unit + 8 integration for gateway; E2E (9.1-9.6) not written |
| RED confirmed (tests exist) | ✅ | 14 unit + 8 integration test files exist and compile |
| GREEN confirmed (tests pass) | ✅ | Gateway unit 14/14, integration 8/8 pass on execution |
| Triangulation adequate | ✅ | Middleware 11 cases, health 3 cases, integration 8 contract cases — good variance |
| Safety Net | ⚠️ | Modified existing tests fixed in #323 (pre-existing compile errors); no shared-state regression |

**TDD Compliance**: Gateway core (Phases 1-5) follows TDD (tests written, green). E2E suite (Phase 9) incomplete — scenarios S-04, S-09, S-10 have no covering test.

## Coherence (Design)

| Decision | Followed? | Notes |
|----------|-----------|-------|
| YARP standalone `CoppAddresd.Gateway` | ✅ Yes | csproj net10.0, Yarp 2.3.0 + Microsoft.Extensions.Http, no ProjectReference |
| Middleware X-Internal-Key antes de MapReverseProxy | ✅ Yes | `app.UseMiddleware<InternalKeyMiddleware>()` before UseCors/MapReverseProxy; fail-closed; FixedTimeEquals; exact 404 problem+json |
| SSE con ActivityTimeout + HttpClient.Timeout | ⚠️ Partial | `appsettings.json` has both (Timeout 00:05:00 + ActivityTimeout 00:10:00); **Example/Development templates omit ActivityTimeout** |
| Route priorities | ✅ Yes (fixed) | Corrected: telemedRoute=300, apiRoute=200, authRoute=100 (matches user-specified; design.md stale) |
| Frontend colapsa a NEXT_PUBLIC_GATEWAY_URL | ✅ Yes | env.ts single apiUrl; .env/.env.example single line |
| Móvil proxy /api→5080 + Capacitor | ✅ Yes | vite.config proxy; capacitor server.url placeholder |
| docker-compose gateway | ✅ Yes | build context root, ports 5080:5080, healthcheck curl, InternalKey env, service-name network |
| Doc AWS split SSE | ✅ Yes | gateway.md comprehensive |

## Issues Found

**CRITICAL**: None (code-level).

**WARNING**:
- `tasks.md` is stale — 37 implementation tasks unchecked despite being implemented+verified on disk (1.1-1.7, 2.1-2.3, 3.1-3.6, 4.1-4.4, 5.1-5.3, 6.1-6.7). Blocks archive until reconciled (stale-checkbox repair with apply-progress proof) or apply re-runs.
- REQ-GW-007 / S-03: the 60-second SSE duration is NOT tested (integration is ~500ms). The `ActivityTimeout` config exists only in gitignored `appsettings.json`; versioned templates (Example.json, Development.json) omit it — a fresh dev clone copying Example.json would lose SSE ActivityTimeout.
- Telemedicine.IntegrationTests 0/25 pass without `COP_TEST_DB_CONNECTION` (env, pre-existing, unrelated to this change — but `dotnet test` full is not green in this env).

**SUGGESTION**:
- REQ-GW-008 / S-04 (Twilio HMAC webhook) has no covering test (needs real Twilio secret) — document as manual E2E.
- REQ-GW-009 / S-08: add an integration test for `GET /health` (WebApplicationFactory already wired).
- REQ-GW-005 / S-09: add a CORS preflight (OPTIONS) integration test.
- `docs/architecture/gateway.md` and `design.md` still show the pre-fix priority table (api=300/telemed=200); the corrected values (telemed=300/api=200) are in the applied config and gateway.md was updated — verify design.md not left stale.

## Manual / Docker-dependent tasks (caveats, not defects)

- 2.4 (smoke `/health` 200), 8.2 (`docker-compose up gateway`) → require running gateway/Docker.
- 3.7 (OPTIONS preflight), 9.6 (CORS preflight E2E) → require gateway + frontend running.
- 9.1/9.2 (login/refresh cookie E2E), 9.4 (Twilio webhook) → require real Auth + Twilio.
- 5.4 (gateway /health integration) → unit-covered; E2E pending.
- 10.4 (`dotnet test` complete) → blocked by Telemedicine.IntegrationTests env requirement.

## Verdict

**READY-WITH-CAVEATS** — Gateway implementation (Phases 1-8, 10) is complete and correct on disk, builds with 0 errors, gateway unit + integration tests pass (22/22), frontend typechecks, mobile builds. Core REQs (001-006, 010-012) are COMPLIANT. Caveats: (1) stale tasks.md checkboxes must be reconciled before archive; (2) SSE 60 s, Twilio webhook, CORS preflight, /health E2E lack automated coverage (manual/Docker/env-dependent); (3) full `dotnet test` requires PostgreSQL for Telemedicine.IntegrationTests.
