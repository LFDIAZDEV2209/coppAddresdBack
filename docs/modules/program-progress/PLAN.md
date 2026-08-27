# PLAN — Program Progress Module

**Module**: 83-week patient program progress (gamified clinical journey).
**Repo**: `coppAddresdBack` (backend) + `antares-paciente` (mobile) + `erp` (admin).
**Status**: Planning. No code, schema, or migrations written yet.

---

## 1. Purpose

Replace the fully mocked `antares-paciente/src/data/program.ts` with a backend-driven
program that:

- Defines a **reusable weekly template** (weekday → task types), applied across all 83 weeks.
- Resolves **dated content** (nutrition plans, routines, podcasts) through existing
  Wellness `*_assignments` so clinicians can change content without redefining weeks.
- Tracks **per-patient completion, XP, streaks, freeze/rescue** as a clinical-grade ledger.
- Preserves the mobile UI's **6 task types and existing XP/level/streak visuals** without
  forcing a UI redesign.
- **Decouples gamification (XP/Level) from clinical outcomes** (Transformation Score,
  Health Score). Gamification never drives clinical claims.

This document is the master plan; the functional/technical detail lives in
[`SPEC.md`](./SPEC.md) and the dependency-ordered work list in
[`TASKS.md`](./TASKS.md).

## 2. Decision summary

| # | Decision | Choice | Why |
|---|----------|--------|-----|
| 1 | Schema location | `app.` (same as Wellness) | Convention: each mobile-facing module owns `app.`. Auth in `auth.`, audit in `audit.`. |
| 2 | Per-week config | **Reusable weekly template**, not 83 templates | The 83 weeks share structure; only content varies. Weekly template is a single row set, applied by `program_enrollments`. |
| 3 | Per-day task resolution | Template decides *which* task types run on which weekday; content (nutrition/routine/podcast) is resolved at runtime from active dated `*_assignments` and `media_progressions`. | Clinicians can swap content (new plan, new routine, new podcast) without re-authoring the template or re-enrolling the patient. |
| 4 | Idempotency | Unique constraint `(enrollment_id, local_date, task_code)` on `app.task_completions`; partial unique on `xp_ledger(source_ref_type, source_ref_id, reason)`. | Mobile retries are common on flaky cellular networks. Double-tap must converge. |
| 5 | Concurrency | Handler locks the enrollment row (`SELECT ... FOR UPDATE`) inside `CreateExecutionStrategy().ExecuteAsync(...)` before awarding XP/streak. | Same pattern as `WellnessRepository.AddPlanWithAssignmentAsync` — NpgsqlRetryingExecutionStrategy requires this wrapper. |
| 6 | Timezone | Patient-local. `program_enrollments.timezone` (default `America/Bogota`). All streak/weekday math in patient-local. | Streak must not break when the patient flies from Bogotá to NYC. |
| 7 | Gamification vs clinical | Two separate metrics. XP/Level are gamification; Transformation/Health Score come from real `app.clinical_measurements` (existing). UI labels them explicitly. | Clinical safety: gamification must not be presented as clinical outcome. |
| 8 | Clinician approval | Adaptation recommendations have a state machine `Pending → Approved/Rejected → Applied` (or `Superseded`). Critical changes (template swap, level jump) always require approval. Routine/nutrition content refresh is auto-applied when the rule allows. | Patient safety overrides operational convenience. |
| 9 | Punishment mechanics | **None.** No hearts, no XP revocation on streak break, no "you lost health" copy. Streak freezes are positive-only tokens. | Clinic policy + clinical safety. |
| 10 | Mobile ↔ backend | Read-then-write integration in 5 slices, with mock fallback during partial rollout. UI never breaks if API is down (demo continuity). | `antares-paciente` is mobile-first demo today; integration must be additive. |
| 11 | Podcast chapters | Deferred. Only `media_id` is referenced. No `chapters` / `takeaways` tables in MVP. | Documented in SPEC §11 as Phase 3+ future work. |
| 12 | Score weights | Health Score dimension weights are configurable in `app.health_score_weights`; seeder ships defaults (0.30 / 0.30 / 0.20 / 0.10 / 0.10). Application validates `SUM(weight) = 1.0000` on write. | Clinicians may tune without a migration; a single-row catalog is small and stable. |
| 13 | Score engine | Scores are computed **on read** with a stored history; no cron / queue in MVP (the backend has no generic scheduler). Manual `POST /api/v1/program/scores/calculate` is the only out-of-band trigger; a background job will be added in a later phase alongside the AI weekly assessment. | Matches the existing `IJobDispatcher` scope (admin bulk only). Stale-until-read is acceptable for an adherence indicator; documented in SPEC §13.3. |
| 14 | Baselines & authorship | `app.clinical_baselines` requires a clinician `set_by` (`auth.users.id` with a clinical role); patient self-set is rejected. `target_value` is clinician-validated, never inferred. | Clinical safety: baselines are the only input that defines what "improvement" means; must be clinician-authored. |
| 15 | Scores are indicators, not diagnosis | Health / Transformation Scores are adherence / evolution indicators. UI labels them as program indicators. Scores never reduce XP, never break streaks, never feed punishment mechanics. | Same rationale as decision 9 (no punishment) and SPEC §6.15 (no clinical claim from gamification). |
| 16 | XP rules catalog (P1.5) | XP awarding is data-driven from `app.xp_rules` (SPEC §14): a rule `Active` within `valid_from..valid_until` wins (`points = base_xp ?? template points`, multiplier, topes `max_per_day`/`max_per_week`); no rule / inactive / expired → fallback to the current behavior (template points, no limits). Edits are **prospective only** (never rewrite `xp_ledger` history). Seeder ships 11 defaults; admin API `GET/PUT /api/v1/program/xp-rules` under `Program.Edit`. | Admins tune rewards and anti-fraud limits at runtime instead of by migration; the awarding path stays untouched for patients (same idempotent transaction). The `STREAK_*` rows (100/200/500/1500) are seeded consistent with the implementation; since "Paso 4" (SPEC §16) they have a live call site (milestone engine) and are awarded once per enrollment. |
| 17 | Clinical XP (P1.5, SPEC §15) | XP clínica se dispara SOLO en `POST /scores/calculate` (el GET nunca otorga XP): evolución favorable ≥ umbral (default 5%) → revisión `pending` que decide un clínico (`CLINICAL_SIGNIFICANT`, no cuenta en totales hasta aprobarse); 1%..umbral → auto `CLINICAL_IMPROVE`; estable → auto `CLINICAL_STABLE`; desfavorable → **0 XP, nunca penaliza**; todas favorables → `CLINICAL_WEEKLY_ALL_UP`. Idempotencia por el dedupe parcial `(source_ref_type, source_ref_id, reason)` (`clinical_period` + `health_scores.id`); totales excluyen las filas `requires_validation` sin `validated_by`. Decisiones clínicas bajo `Program.Adapt` + rol clínico (guardia AC-22). | La gamificación nunca debe premiar inventos: la XP clínica nace de mediciones reales contra líneas base de autoría clínica, y las mejorías grandes pasan por validación profesional antes de contar. Sin doble premio por período (dedupe en BD) y sin castigo por empeoramiento (decisión 9/12). |
| 18 | Streak multiplier x2 (P1.5, SPEC §16, "Paso 4") | Los hitos de racha (7/11/22/50 días) otorgan su XP del catálogo **una única vez por inscripción** (`source_ref_type='streak_milestone'`, dedupe parcial del libro mayor); los hitos 11/22/50 además **activan un multiplicador x2 del paciente** (24h/48h/72h) que se **sobrescribe** al alcanzar un hito nuevo (se extiende desde ahora) y aplica a **TODA** la XP mientras está vigente (tareas, bonus de día, hitos y clínica — sin excepciones). Vencido → se trata como 1.0 con **reset lazy** en el próximo otorgamiento; `xp_ledger.multiplier_used` registra el multiplicador efectivo (regla × paciente); el snapshot expone `multiplierActive`/`multiplierEndsAt`/`multiplierRemainingHours`. | Referencia ADRED del módulo: el día 7 premia constancia; los hitos 11/22/50 aceleran el avance durante 24h/48h/72h. El multiplicador es del paciente (no del catálogo), es un refuerzo temporal de la gamificación — nunca una métrica clínica (decisiones 7/15). La activación queda protegida por el mismo dedupe del hito (un hito repetido no re-activa). |
| 19 | Configurable streak threshold & essential tasks (P1.5, SPEC §17, "Paso 5") | `app.program_templates` gana `streak_min_tasks` (SMALLINT NOT NULL default 1) y `essential_task_codes` (jsonb, default `[]`); el seed `default-83w` fija 1 y `["nut","ejercicio","nutribiotico"]` **solo en create** (idempotente: re-runs no pisan config existente). Un día **cumple el umbral** si `tasks_done >= streak_min_tasks` (default 1 → comportamiento previo); el día perfecto sigue siendo "todas las tareas" (bonus + concesión de congelamientos, sin cambios). El **rescate con congelamiento exige ≥1 tarea esencial** el día perdido (regla ADRED, AC-31/AC-32): sin tarea esencial el congelamiento NO se consume (queda en inventario) y la racha se rompe. Lista esencial vacía = sin restricción (compatibilidad previa). El snapshot expone `streakMinTasks`/`essentialTaskCodes` (aditivos). | Umbral ajustable sin migración (el seeder es la vía de config hasta la edición ERP B7); el rescate exige esfuerzo esencial real (referencia ADRED) adaptado a la economía de congelamientos del módulo (cadencia de concesión intacta: 1 por 7 perfectos, tope 3). |
| 20 | Granular nutrition XP (P1.5, SPEC §18, "Paso 6") | La nutrición gana XP granular **ADITIVA** a la tarea `nut` existente: `POST /api/v1/program/nutrition/log` registra la comida/hidratación (`des`/`alm`/`mer`/`cen`/`agua`) en `app.habit_checks` (único por paciente+plantilla+fecha; duplicado → `409 HABIT_ALREADY_LOGGED`) y otorga `NUTRITION_MEAL_COMPLETE` (10, tope 4/día) / `NUTRITION_HYDRATION` (5, tope 1/día) por el camino del catálogo (multiplicador del paciente incluido). Los premios semanales (`NUTRITION_WEEK_85` +75 por adherencia ≥85%; `NUTRITION_RECOVERY` +50 por +20pp vs el período anterior) se disparan SOLO en `POST /scores/calculate` con la MISMA fuente de adherencia que la dimensión `nutrition` del Health Score (reuso de `GetNutritionLogAsync`, sin duplicación) y el dedupe `('nutrition_period', health_scores.id, reason)`. `NUTRITION_PHOTO` queda **diferido** (el log de este paso es directo, sin análisis de foto). La tarea `nut` NO se auto-completa desde el log; su flujo queda intacto. | La XP granular recompensa el detalle diario (acción completa + registro por comida) sin romper el contrato de puntos del programa; el semanal recompensa el hábito (85%) y la recuperación (+20pp) con la misma fuente que ya alimenta el Health Score. El doble premio (tarea 150 + comidas hasta 40/día) es visible (rule_code) y tuneable vía el catálogo. |

## 3. Architecture (one screen)

```
   ERP (admin)                  Backend (CoppAddresd.Api)                Mobile (antares-paciente)
+----------------+   HTTPS    +-----------------------------+    HTTPS   +---------------------+
|  Templates     | ---------> | /api/v1/program/templates   | <--------  |  ProgramPage        |
|  Enrollments   |            | /api/v1/program/enrollments |            |  src/data/program.ts|
|  Adaptations   |            | /api/v1/program/adaptations |            |  (mock fallback)    |
|  Weekly tmpl   |            +-----------------------------+            +---------------------+
+----------------+                         |
                                            |   MediatR commands/queries
                                            v
                                  +-----------------------------+
                                  | Application/Features/        |
                                  |   ProgramProgress/           |
                                  |     Commands/                |
                                  |     Queries/                 |
                                  |     Validators/              |
                                  |     DTOs/                    |
                                  +-----------------------------+
                                            |
                                            v
                                  +-----------------------------+        +----------------------+
                                  | Domain/Entities/             |        |  app.* (Wellness)    |
                                  |   ProgramEnrollment          | -----> |  nutrition_plans     |
                                  |   ProgramWeek                |        |  exercise_routines   |
                                  |   TaskCompletion             |        |  *_assignments       |
                                  |   XpLedger                   |        |  media_items         |
                                  |   StreakState                |        +----------------------+
                                  |   AdaptationRecommendation   |
                                  +-----------------------------+
                                            |
                                            v
                                  +-----------------------------+
                                  | Infrastructure/Repositories  |
                                  |   ProgramRepository (PG)     |
                                  |   AppDbContext               |
                                  |   EF Configurations          |
                                  |   Migrations                 |
                                  +-----------------------------+
```

## 4. Phasing

| Phase | Scope | Deliverables | Exit criteria |
|-------|-------|--------------|---------------|
| **P0 — Plan (now)** | This doc + SPEC + TASKS | 3 markdown files approved | Reviewer sign-off |
| **P1 — MVP** | Read snapshot, complete tasks, basic streak | Schema (11 new tables in `app.` — see SPEC §3), domain/application/infra/API, seeder (1 default 83-week template), patient endpoints, mobile Slice 1+2 | Patient sees live XP/streak; clinician sees enrollment list; idempotent completion proven in tests |
| **P1.5 — Score engine + XP rules catalog + Clinical XP + Streak multiplier + Streak threshold & essentials + Granular nutrition XP** | Health & Transformation Score engine + data-driven XP rules catalog + XP clínica con validación profesional + multiplicador x2 por hito de racha + umbral de racha configurable y tareas esenciales + nutrición granular por comida/hidratación | Score engine: 4 new tables (`app.health_score_weights`, `app.clinical_baselines`, `app.health_scores`, `app.transformation_scores` — see SPEC §13.1); calculator services (health + transformation); `GET /api/v1/program/scores` + `POST /api/v1/program/scores/calculate`; AC-19..AC-22; mobile Evolución tab wired with mock fallback. XP rules catalog: `app.xp_rules` (1 new table, SPEC §14.1) + `xp_ledger.rule_code`; 11 seeded rules; award-path resolution with precedence, multiplier and `max_per_day`/`max_per_week` anti-fraud limits; admin `GET/PUT /api/v1/program/xp-rules` (prospective only); AC-23/AC-24 (manual per current workflow). Clinical XP: `app.clinical_xp_reviews` (1 new table) + `xp_ledger.validated_by`/`validated_at`; 4 seeded `clinical` rules; award engine on `/scores/calculate` (auto `CLINICAL_IMPROVE`/`CLINICAL_STABLE`/`CLINICAL_WEEKLY_ALL_UP` + reviews `pending` for significant); validation flow `GET /xp-rules/clinical-pending` + `POST .../{id}/decide` (`Program.Adapt` + rol clínico); pending awards excluded from XP totals; AC-25..AC-27 (manual). **Streak multiplier (Paso 4)**: `streak_states.multiplier_active`/`multiplier_ends_at` + `xp_ledger.multiplier_used` (SPEC §16); milestone engine awards `STREAK_7/11/22/50` once per enrollment and activates x2 (24h/48h/72h, overwrite-on-new-milestone, lazy expiry reset); multiplier applies to all XP while active; snapshot exposes `multiplierActive`/`multiplierEndsAt`/`multiplierRemainingHours`; AC-28..AC-30 (manual). **Streak threshold & essentials (Paso 5)**: `program_templates.streak_min_tasks` + `essential_task_codes` (SPEC §17); threshold-based streak maintenance (`tasks_done >= streak_min_tasks`, default 1); rescue-with-freeze requires an essential task (AC-31/AC-32; freeze kept in inventory when not consumable); snapshot exposes `streakMinTasks`/`essentialTaskCodes` (additive); migration `AddProgramProgressStreakConfig`; AC-09 updated. **Granular nutrition XP (Paso 6, SPEC §18)**: `app.habit_templates` + `app.habit_checks` (2 new tables) + 4 seeded `nutrition` rules (`NUTRITION_MEAL_COMPLETE` 10×4/día, `NUTRITION_HYDRATION` 5×1/día, `NUTRITION_WEEK_85` 75, `NUTRITION_RECOVERY` 50) + 5 `habit_templates` (des/alm/mer/cen + agua); `POST /api/v1/program/nutrition/log` (log idempotente de comida/hidratación, XP granular **aditiva** a la tarea `nut` existente — la tarea NO se auto-completa —, `409 HABIT_ALREADY_LOGGED` en duplicados, `422 INVALID_DATE` en fecha futura); premios semanales evaluados SOLO en `POST /scores/calculate` (`NUTRITION_WEEK_85` +75 por adherencia ≥85%, `NUTRITION_RECOVERY` +50 por +20pp vs el período anterior, dedupe `('nutrition_period', health_scores.id, reason)`, multiplicador del paciente aplicado); `NUTRITION_PHOTO` **diferido** (sin análisis de foto en este paso); migración `AddProgramProgressNutritionXp`; AC-33..AC-36 (manual) | Real Health / Transformation scores in the mobile Evolución tab; clinical baselines are clinician-authored; integration tests cover bands, no-data, and authorship; XP awarding resolves the catalog with limits enforced and a clear fallback; admin can tune rules without a migration; significant clinical improvements require professional approval before counting toward XP; streak milestones award their catalog XP once and activate the x2 window per the ADRED reference; the streak threshold is configurable per template and freezes only rescue days with essential effort (AC-31/AC-32); meals/hydration are logged granularly with additive XP (AC-33/AC-34) and weekly nutrition adherence awards fire only on `/scores/calculate` (AC-35/AC-36) |
| **P2 — Adaptation path** | Clinician approval workflow, content refresh, podcast rotation, ERP screens | `app.media_progressions` (1 new table, plus `adaptation_recommendations` already in P1 — see SPEC §3.11), auto-bridge to Wellness `*_assignments`, ERP UI for templates + adaptations queue | Clinician can approve an adaptation and the next week reflects new content; mobile Slice 3+4 |
| **P3 — Enhancements** | Streak reconciliation, bulk enrollment, CSV export, i18n, score pre-warm | Nightly reconcile job over `app.xp_ledger` → `app.streak_states`; bulk enrollment via `IJobDispatcher`; CSV export endpoint; EN strings on mobile; optional cron that pre-warms `app.health_scores` / `app.transformation_scores` (deferred from P1.5) | All P2 acceptance scenarios pass; locale parity in EN/ES |
| **Deferred** | Podcast chapters/takeaways, rewards marketplace, social leagues | Not designed in MVP. Re-evaluate after P3 stabilization. | — |

## 5. What's out of scope (MVP)

- Podcast chapter/takeaway tables.
- Rewards marketplace or chest economy (the `NEXT_CHEST_DAYS` mock is replaced by a single derived `nextMilestoneDays` field; no `chest` table in MVP).
- Social/leagues/cross-patient comparisons.
- Push notification triggers (handled by existing `app.device_tokens`).
- Multi-language UI strings on the mobile side (existing Spanish strings preserved; English added in P3).
- **NOT out of scope**: the Health & Transformation Score engine (P1.5, SPEC §13). The engine is in scope; only its cron / queue pre-warm is deferred to P3.

## 6. Cross-cutting concerns

- **Audit**: `created_by` / `updated_by` columns on every mutable table → `auth.users(id)` (FK enforced by SQL, no EF navigation; same pattern as `app.patient_profiles`). The existing `AuditTriggerInterceptor` (GUC-based actor propagation) covers DML audit automatically.
- **Authorization**: JWT scope is the existing `ICurrentContext`. Patient endpoints scope by `UserId`; clinician/admin endpoints require new permissions seeded in `auth.permissions` (codes listed in SPEC §6).
- **Soft delete**: not used. Wellness uses hard delete with `status` enum (`Active=1/Draft=2/Archived=3`). Same convention here.
- **Validation**: FluentValidation on every command; validator tests required.
- **Error handling**: `GlobalExceptionHandlerMiddleware` shape (already in place). 404 for cross-patient reads (anti-IDOR); 409 only for true conflicts (template weekday collision).
- **Logging**: `ILogger<T>` injected in handlers. No PII in logs (no vitals values, no patient names).
- **Performance**: `ix_*` indexes documented in SPEC §4.1. All list endpoints paginated (max pageSize = 100). Hot read paths (`GET /program/me`) use single round-trip projections; no N+1.

## 7. Risks

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Auth Service JWT does not yet include `patient_id` claim | Medium | Blocks enrollment resolution | Resolve via `app.patient_profiles.user_id` lookup (1 query, cached). Open question documented. |
| Mobile offline writes diverging from backend ledger | Medium | XP loss or duplicate rewards | Server is source of truth; client retries idempotently. UI shows `lastSyncFailed` badge if queue diverges. |
| Clinician edits template while patients are mid-week | Low | Mid-week UI inconsistency | Tasks for the current week are *snapshotted* at `program_weeks.created_at` reads; template edits apply on next week boundary by default. Documented in SPEC §3. |
| Patient crosses timezone at midnight | Low | Streak false-positive break | All math in patient-local TZ, enrolled at onboarding. |
| Time pressure to ship P1 means skipping idempotency tests | Medium | Production data corruption under flaky network | TASKS.md blocks P1 acceptance on concurrency/idempotency test passes. |
| Multiple concurrent `POST /tasks/complete` for same task | Low | Double XP | Row lock + unique constraint; verified by integration test. |
| Score engine on-read latency grows with patient history | Medium | Slow `GET /program/scores` for long-tenured patients (1000+ `task_completions` / year) | Compute-on-read reads **only the period window** (≤7 days for Health, 1 program week for Transformation); the calculator pulls a single windowed query per data source. Persisted `app.health_scores` / `app.transformation_scores` rows (indexed on `patient_id, period_end DESC`) are returned on cache hit and short-circuit the math. Documented in SPEC §13.3. |
| Stale-until-read scores look "out of date" on dashboards | Medium | Confusion when a patient sees a 3-day-old score in the mobile Evolución tab | Each persisted row carries `calculated_at`; the mobile label includes the calculated-at date. When the cron / pre-warm lands in P3, the freshness window tightens. AC-18 (stale-snapshot tolerance) extends to the score endpoint with an `X-Score-Stale` header. |
| Clinician misconfigures `health_score_weights` so `SUM(weight) ≠ 1` | Low | Health Score drifts to a wrong magnitude | Application-layer invariant on write; `POST /program/scores/calculate` re-validates before computing. The seeder is the only path that runs without a transaction in dev; production deploys always run the seeder first. |
| Patient self-sets a clinical baseline via a future patient-facing endpoint | Low | Clinical safety incident (a patient defines "improvement" for themselves) | `app.clinical_baselines.set_by` is a hard `NOT NULL` FK to `auth.users(id)`; the Application layer (AC-22) rejects any write whose `set_by` is not a clinician. No patient-facing endpoint is added in P1.5. |
| Misconfigured XP rule silently falls back to template points (inactive/expired/missing) | Medium | Admin thinks a tuned rule is live but patients get default points; anti-fraud limits not applied | **Admin visibility** (SPEC §14.4/14.5): `GET /xp-rules` always shows `active`/`valid_from`/`valid_until`; the award path documents the fallback and `xp_ledger.rule_code` is NULL on fallback rows so provenance exposes which awards bypassed the catalog. Seeder never overwrites admin edits (DO NOTHING). |
| Clinical XP double-awarded across repeated `/scores/calculate` for the same period | Low | Patients farm significant/improvement XP by recomputing scores | The partial unique `(source_ref_type, source_ref_id, reason)` on `xp_ledger` with `source_ref_type = 'clinical_period'` and `source_ref_id = health_scores.id` allows exactly one award per rule per period; the review queue is unique per `(patient, health_score, metric)`. Violations are skipped, never retried (SPEC §15.3, AC-25). |
| An unfavorable clinical evolution is presented as a punishable event | Medium | Trust/clinical-safety incident (gamification punishing health) | Hard rule documented (SPEC §15.3, AC-27): unfavorable indicators award **0 XP**, never reduce XP, never break streaks, never feed punishment mechanics (decisions 9/12/15). No review row is created for unfavorable changes. |
| Pending significant awards leak into XP totals before professional approval | Low | Patient sees XP/level that was not clinically validated | Totals exclude `xp_ledger` rows whose rule `requires_validation = true` and `validated_by IS NULL` (SPEC §15.5, AC-26); only approval writes `validated_by`/`validated_at` and makes the award count. |
| Milestone XP / multiplier double-applied when a streak rebuild re-reaches a milestone | Low | Patients farm milestone XP or re-activate the x2 window by breaking and rebuilding streaks | The milestone XP is guarded by the same partial unique as the rest of the ledger (`source_ref_type='streak_milestone'`, `source_ref_id = streak_states.enrollment_id`, `reason='STREAK_{days}'`): a repeated milestone neither re-awards nor re-activates (SPEC §16, B.3, AC-28). The enrollment row lock (`FOR UPDATE`) serializes concurrent completions. |
| Multiplier still applied after the window expires (stale `multiplier_ends_at`) | Low | XP awarded with x2 beyond the advertised window | Expiry is evaluated against the server clock at award time inside the award transaction; an expired multiplier is treated as 1.0 and lazily reset (`multiplier_active=1.0`, `multiplier_ends_at=null`) via `ExecuteUpdate` (SPEC §16, C.1, AC-30). The snapshot read path never writes and shows 1.0/null/0 once expired (SPEC §16, D). |
| Raising `streak_min_tasks` after patients are enrolled | Medium | Active patients would break streaks that previously held (a 2-task day stops maintaining the streak) and freeze-rescue tightens (only days with ≥1 essential task stay rescuable) | Config is template-level and **prospective by nature** (read at completion time from the enrollment's template): raising the threshold applies from the next qualifying day for all active patients of that template. The seeder never overwrites an existing template's config (idempotent), so tuning is explicit. Advise clinicians to tune per cohort, not mid-program; the snapshot exposes `streakMinTasks`/`essentialTaskCodes` (additive) so the mobile renders the new requirement without a breaking change (SPEC §17, AC-31/AC-32). |
| Granular nutrition double-dips with the `nut` task (a patient completes the daily `nut` task AND logs 4 meals → 150 + up to 40 XP/day from the granular awards) | Low (by design) | The module over-rewards nutrition relative to other tasks; the Health Score nutrition dimension may inflate if the same meals are double-counted | **Documented as the chosen design** (SPEC §18.5, decision 24): the granular XP is **additive** — the task rewards the completed action, the logs reward the per-meal detail. Each `xp_ledger` row carries `rule_code` (provenance), so the double-award is visible and **tunable via `xp_rules`** (lower `base_xp`, raise `max_per_day`, or deactivate `NUTRITION_*`) without a migration (prospective only, SPEC §14.4). The Health Score dimension consumes `habit_checks` only — no double counting of the task points (the dimension never reads `task_completions` for nutrition). |
| Granular weekly award fires on stale adherence (habit_checks written for a period already evaluated) | Low | A re-run of `/scores/calculate` for an old period could award week/ recovery XP from historical data | The awards use the **same period window as the Health Score** (`period_start..period_end`, SPEC §13.2) and are idempotent per `('nutrition_period', health_scores.id, reason)` (one award per rule and period; re-runs skip — AC-35/AC-36). `periodEndLocalDate` future dates are rejected (`422 INVALID_PERIOD`); `habit_checks` are patient-local dated, so past-period logs are legitimate history, not replay. |

## 8. Milestones & verification

| Milestone | Date / Trigger | Verification |
|-----------|----------------|--------------|
| P0 plan approved | After review of PLAN+SPEC+TASKS | Reviewer sign-off |
| P1 schema migration applied | After T-01..T-05 tasks | `dotnet ef database update` succeeds; `SELECT COUNT(*) FROM app.program_enrollments` returns expected seeded count |
| P1 API live | After T-06..T-10 tasks | `dotnet test` green; Pact/mobile contract test green |
| Mobile Slice 1+2 shipped | After T-14, T-15 tasks | Patient completes a task in mobile demo, refreshes, XP persists |
| **P1.5 score engine live** | After T-35..T-41 tasks (B5-S) | `GET /program/scores` returns real Health + Transformation scores matching the mobile mock shapes; AC-19..AC-22 pass; mobile Evolución tab wired with mock fallback |
| **P1.5 XP rules catalog live** | After T-42..T-45 tasks (B5-R) | `GET /api/v1/program/xp-rules` returns the 11 seeded rules; `PUT` edits are prospective; AC-23/AC-24 pass manually per the current workflow (no automated test run); migration `AddXpRulesCatalog` generated (not applied) |
| **P1.5 Clinical XP live** | After T-46..T-49 tasks (B5-C) | `POST /scores/calculate` produces auto-awards (`CLINICAL_IMPROVE`/`CLINICAL_STABLE`/`CLINICAL_WEEKLY_ALL_UP`) and `pending` reviews for significant improvements; clinician approves via `POST /xp-rules/clinical-pending/{id}/decide` and the XP counts; AC-25..AC-27 pass manually per the current workflow; migration `AddProgramProgressClinicalXp` generated (not applied) |
| **P1.5 Streak multiplier live** | After T-50..T-53 tasks (B5-M) | Day-11 streak awards `STREAK_11` once and activates x2 for 24h (snapshot shows `multiplierActive=2.0`); awards during the window carry `multiplier_used`; an expired window resets to 1.0 lazily; AC-28..AC-30 pass manually per the current workflow; migration `AddProgramProgressMultiplier` generated (not applied) |
| **P1.5 Streak threshold & essentials live** | After T-54..T-57 tasks (B5-T) | Template `default-83w` seeds `streak_min_tasks=1` + `essential_task_codes=["nut","ejercicio","nutribiotico"]`; a below-threshold day no longer maintains the streak; a freeze only rescues days with ≥1 essential task (AC-31/AC-32, freeze kept when not consumable); snapshot exposes `streakMinTasks`/`essentialTaskCodes`; migration `AddProgramProgressStreakConfig` generated (not applied) |
| **P1.5 Granular nutrition XP live** | After T-58..T-61 tasks (B5-N) | `POST /nutrition/log` logs meals/hydration with additive XP (`NUTRITION_MEAL_COMPLETE` 10×4/día, `NUTRITION_HYDRATION` 5×1/día, `409 HABIT_ALREADY_LOGGED` on duplicates); `POST /scores/calculate` awards `NUTRITION_WEEK_85` (+75, adherence ≥85%) and `NUTRITION_RECOVERY` (+50, +20pp vs prior period) once per period (dedupe `nutrition_period`); seeder ships 4 nutrition rules + 5 habit templates; AC-33..AC-36 pass manually per the current workflow; migration `AddProgramProgressNutritionXp` generated (not applied) |
| P2 adaptation workflow live | After T-16..T-20 tasks | Clinician approves recommendation; mobile reflects new content within 1 week boundary |
| P3 enhancements | After P2 stabilization | All P2 acceptance scenarios pass; optional score pre-warm cron landed |

## 9. Source of truth and related docs

| Doc | Role |
|-----|------|
| [`SPEC.md`](./SPEC.md) | Functional + technical specification (data model, domain rules, API contract, frontend mapping, tests) |
| [`TASKS.md`](./TASKS.md) | Dependency-ordered implementation backlog with stable IDs and acceptance criteria |
| `coppAddresdBack/AGENTS.md` | Repo commands, schema conventions, gotchas |
| `coppAddresdBack/docs/modules/patients/PLAN.md` | Reference for doc structure and module-plan conventions |
| `antares-paciente/AGENTS.md` | Mobile commands and Ionic-first rules |
| `antares-paciente/src/data/program.ts` | Mock data this module replaces |
| `antares-paciente/src/pages/ProgramPage.tsx` | Mobile UI being wired to backend (Evolución tab consumes `GET /program/scores`, SPEC §13) |

## 10. Open questions

- [ ] Does the Auth Service JWT carry a `patient_id` claim, or must we resolve it from `ICurrentContext.UserId` → `app.patient_profiles.user_id`?
- [ ] Does `app.media_items.Month`/`Day` (existing columns) overlap with `weekly_day_templates.weekday` semantics? Likely keep both; clarify before P2 `media_progressions` work.
- [ ] Should clinician approval be required for *any* routine change, or only difficulty changes? (Lean: only difficulty/level, auto-apply routine swap within same category.)
- [ ] Streak freeze grant rate: do we grant a freeze every X perfect days, or only on enrollment-level milestones? (Default: 1 freeze granted every 7 perfect days, cap 3.)
- [ ] When does `program_weeks.current_week_number` advance — at midnight in patient TZ on day 7, or on first task of the new week? (Default: at midnight rollover; documented.)
