# SPEC — Program Progress Module

> Functional + technical specification for the 83-week patient program.
> Source of truth: [`PLAN.md`](./PLAN.md) (master plan) + this file (contract)
> + [`TASKS.md`](./TASKS.md) (work order). All three must agree.

---

## 0. Decision summary & implementation contract

| # | Decision | Choice |
|---|----------|--------|
| 1 | Schema | `app.` (same as Wellness; mobile-facing module) |
| 2 | Per-week config | **Reusable weekly template** (catalog rows in `app.weekly_day_templates`), snapshotted per `program_week` at week start |
| 3 | Per-day task resolution | Template decides *which* task types run on which weekday. Content (nutrition/routine/podcast) is resolved at runtime against the patient's active `NutritionPlanAssignment` / `RoutineAssignment` / `MediaItem` |
| 4 | Task catalog | Same 6 task codes the mobile uses: `podcast`, `vitals`, `nut`, `ejercicio`, `nutribiotico`, `emocional` |
| 5 | Base points | Stored in `app.weekly_day_templates.points` (default 80/120/150/150/80/120 — matches mobile mock) |
| 6 | Day bonus | +50 when *all scheduled tasks for the weekday are completed*; computed on the last completion of the day (idempotent) |
| 7 | Idempotency | Unique `(enrollment_id, local_date, task_code)` on `task_completions`; partial unique `(source_ref_type, source_ref_id, reason)` on `xp_ledger`; `client_request_id` column carries the mobile retry key |
| 8 | Concurrency | `program_enrollments` row locked `FOR UPDATE` inside `CreateExecutionStrategy().ExecuteAsync(...)` before awarding XP/streak (same pattern as `WellnessRepository.AddPlanWithAssignmentAsync`) |
| 9 | Timezone | Patient-local (`program_enrollments.timezone`, IANA, default `America/Bogota`). All streak/weekday math in patient-local. |
| 10 | Gamification vs clinical | Two separate metrics. XP/Level are gamification. Transformation/Health Score come from real `app.clinical_measurements` (existing). UI labels them explicitly. |
| 11 | Clinician approval | `adaptation_recommendations` state machine `Pending → Approved|Rejected → Applied|Superseded`. Difficulty / level / template swaps require approval. Routine / nutrition content refresh is auto-applied when the rule allows. |
| 12 | Punishment | None. No XP revocation, no hearts, no negative copy. Freezes are positive-only tokens. |
| 13 | Mobile ↔ backend | Read-then-write, 5 slices. Mock fallback during partial rollout; UI never breaks if API is down. |
| 14 | Snapshot policy | `program_weeks.tasks_snapshot` is a `jsonb` copy of the template's `weekly_day_templates` taken at `week_start_date_local`. Template edits mid-week do not affect the current week. |
| 15 | FK strategy | Explicit nullable FKs (`nutrition_plan_id`, `exercise_routine_id`, `media_id`, `vital_signs_batch_id`, `nutribiotic_product_id`, `emotional_record_id`) on `task_completions`; one per task code. No polymorphic FKs. |
| 16 | Score weights | Health Score dimension weights live in `app.health_score_weights` (one row per dimension, unique by `dimension`); a seeder inserts the defaults `adherence=0.30, clinical=0.30, nutrition=0.20, psychology=0.10, exercise=0.10`. Sum must equal `1.0000`; the Application layer validates this on write. |
| 17 | Score engine | Scores are **computed on read** with a stored history: `GET /api/v1/program/scores` recomputes and persists an `app.health_scores` / `app.transformation_scores` row for the current period if missing or stale (the stored `period_end` is before today in patient-local time). No cron / queue infrastructure exists in MVP — see §13.3. |
| 18 | Baselines & authorship | `app.clinical_baselines` rows require a clinician `set_by` (`auth.users.id`); a patient can never self-set a baseline. `target_value` requires clinical validation (clinician-set, never inferred). |
| 19 | Scores are indicators, not diagnosis | Health and Transformation Scores are adherence / evolution indicators only. UI labels them as program indicators. Scores never reduce XP, never break streaks, and never feed punishment mechanics. PHI (mood, barriers, notes) is excluded from audit payloads (existing rule, §8.5). |
| 20 | XP rules catalog | XP awarding is data-driven from `app.xp_rules` (SPEC §14, P1.5): a rule `Active` within `valid_from..valid_until` wins over the default behavior (points = `base_xp ?? <existing source>`, multiplier, topes `max_per_day`/`max_per_week`); no rule or inactive/expired rule falls back to the current behavior (template points, no limits). Edits are **prospective only** — they never rewrite `xp_ledger` history. Seeder ships 24 default rules (11 adherencia/racha + 4 clínicas + 4 nutrición + 5 racha nutribiótico, SPEC §14.2/§15.2/§18.1/§19.2); admin API `GET/PUT /api/v1/program/xp-rules` (permission `Program.Edit`). |
| 21 | Clinical XP (P1.5, SPEC §15) | XP clínica se dispara SOLO en `POST /api/v1/program/scores/calculate` (el `GET /scores` nunca otorga XP): mejoría favorable ≥ umbral (default 5%) → revisión `pending` que decide un clínico (`CLINICAL_SIGNIFICANT`, no cuenta en totales hasta aprobarse); mejoría 1%..umbral → auto `CLINICAL_IMPROVE`; estable → auto `CLINICAL_STABLE`; desfavorable → **0 XP, nunca penaliza**; todas favorables → `CLINICAL_WEEKLY_ALL_UP`. Idempotencia por el dedupe parcial `(source_ref_type, source_ref_id, reason)` con `clinical_period` + `health_scores.id`. Los totales de XP excluyen las filas `requires_validation` sin `validated_by`. Decisiones clínicas bajo `Program.Adapt` + rol clínico (AC-22). |
| 22 | Streak multiplier x2 (P1.5, SPEC §16, "Paso 4") | Los hitos de racha (7/11/22/50 días) otorgan su XP del catálogo **una única vez por inscripción** (dedupe parcial del libro mayor, `source_ref_type='streak_milestone'`); los hitos 11/22/50 además **activan un multiplicador x2 del paciente** (24h/48h/72h) que se SOBRESCRIBE al alcanzar un hito nuevo (se extiende desde ahora) y aplica a **TODA** la XP mientras está vigente (tareas, bonus de día, hitos y clínica — sin excepciones). Vencido → se trata como 1.0 con reset lazy en el próximo otorgamiento; `xp_ledger.multiplier_used` registra el multiplicador efectivo (regla × paciente); el snapshot expone `multiplierActive`/`multiplierEndsAt`/`multiplierRemainingHours`. |
| 23 | Configurable streak threshold & essential tasks (P1.5, SPEC §17, "Paso 5") | `app.program_templates` gana `streak_min_tasks` (SMALLINT NOT NULL default 1) y `essential_task_codes` (jsonb, default `[]`); el seed `default-83w` fija 1 y `["nut","ejercicio","nutribiotico"]` **solo en create** (idempotente: re-runs no pisan config existente). Un día "cumple el umbral" si `tasks_done >= streak_min_tasks` (default 1 → comportamiento previo); el día perfecto sigue siendo "todas las tareas" (bonus + concesión de congelamientos, sin cambios). El rescate con congelamiento exige ≥1 tarea esencial el día perdido (regla ADRED, AC-31/AC-32): sin tarea esencial el congelamiento NO se consume (queda en inventario) y la racha se rompe. El snapshot expone `streakMinTasks`/`essentialTaskCodes` (aditivos). La cadencia de concesión (1 por 7 días perfectos, tope 3) no cambia. | Umbral ajustable sin migración (el seeder es la vía de config hasta que exista la edición ERP B7); el rescate exige esfuerzo esencial real (referencia ADRED) adaptado a la economía de congelamientos del módulo. |
| 24 | Granular nutrition XP (P1.5, SPEC §18, "Paso 6") | La nutrición gana XP granular **aditiva** a la tarea `nut` existente: `POST /api/v1/program/nutrition/log` registra la comida/hidratación (`des`/`alm`/`mer`/`cen`/`agua`) en `app.habit_checks` (único por paciente+plantilla+fecha; duplicado → `409 HABIT_ALREADY_LOGGED`) y otorga `NUTRITION_MEAL_COMPLETE` (10, tope 4/día) / `NUTRITION_HYDRATION` (5, tope 1/día) por el camino del catálogo. Los premios semanales (`NUTRITION_WEEK_85` +75 por adherencia ≥85%, `NUTRITION_RECOVERY` +50 por +20pp vs el período anterior) se disparan SOLO en `POST /scores/calculate`, con la MISMA fuente de adherencia que la dimensión `nutrition` del Health Score (reuso, no duplicación) y el dedupe `('nutrition_period', health_scores.id, reason)`. `NUTRITION_PHOTO` queda **diferido** (el backend de este paso registra el log directo, sin análisis de foto). La tarea `nut` NO se auto-completa desde el log; su flujo queda intacto. | La XP granular recompensa el detalle diario (acción + registro) sin romper el contrato de puntos del programa; el doble premio (tarea 150 + comidas hasta 40/día) es visible y se tunea vía `xp_rules`. El semanal recompensa el hábito (85%) y la recuperación (+20pp) con la misma fuente que ya alimenta el Health Score. |
| 25 | Nutriobiótico streak (P1.5, SPEC §19, "Paso 7a") | La tarea `nutribiotico` mantiene su **PROPIA racha consecutiva** (`app.streak_states.nb_current_streak`/`nb_longest_streak`/`nb_last_completed_date`), independiente de la racha general y de los congelamientos (decisión de producto): un **día perdido la rompe** (se reinicia a 1 en la próxima completación) y **NO la protegen los congelamientos** (AC-38). Los hitos de la corrida (7/14/30/60/90 días → `NB_STREAK_7/14/30/60/90`, 50/100/250/500/1000 XP, categoría `nutriobiotic`, topes 1/día y 1/semana) se otorgan en el camino de completación de la tarea (solo primera escritura, dentro de la transacción FOR UPDATE) con el multiplicador del paciente (SPEC §16) y el dedupe parcial `('nb_milestone', task_completions.id, reason)` — **cada corrida nueva re-otorga su hito al alcanzarlo** (AC-39); el mismo hito dentro de la misma semana se omite (tope 1/semana). La XP base de la tarea `TASK_NUTRIBIOTICO` y la racha general NO cambian. El snapshot expone `nbStreak`/`nbLongestStreak`/`nbNextMilestone` (aditivos). | La racha propia premia el hábito diario del nutribiótico (el pilar de producto de CoppAddresd) sin acoplarse a la racha general del programa: un paciente puede perder la racha general por un día sin tareas y aun así mantener su compromiso con el nutribiótico. El re-otorgamiento por corrida (AC-39) recompensa cada ciclo de constancia (referencia ADRED adaptada); los topes 1/día y 1/semana del catálogo limitan el farm. |
| 26 | Gamified notifications (P1.5, SPEC §20, "Paso 7b") | Nuevo log `app.notifications` + push FCM reutilizando el camino EXISTENTE del módulo de notificaciones (`app.device_tokens` + `IFcmClient`). Servicio **best-effort** dentro de los flujos de otorgamiento (tras la escritura de la XP, SOLO en la primera concesión, nunca en replay): un fallo de envío/persistencia NUNCA rompe la transacción de XP (AC-42). Anti-spam configurable (`Program:Notifications`): máx. 2 por tipo por día local + máx. 6 totales por día local; límite alcanzado → se omite en silencio (AC-41). Horario de silencio 22:00–07:00 local (la prioridad `critical` lo ignora). Disparadores: hito de racha (`milestone_reached`, high), hito nutribiótico (`nb_milestone`, high), subida de nivel (`level_up`, high, comparando el nivel antes/después del día) y día perfecto (`day_complete`, normal). `multiplier_expiring`, "racha en riesgo a fin de día" y evaluación semanal → **FUTURO** (necesitan scheduler; no hay cron en MVP). Endpoints del centro de notificaciones: `GET /api/v1/program/notifications` (paginado, `readAt` + `unreadCount`) y `POST /api/v1/program/notifications/{id}/read` (paciente-propio, anti-IDOR 404). | La gamificación debe reconocer los logros en el momento en que ocurren, pero el backend no tiene colas/cron (patrón documentado): la notificación se dispara transaccionalmente dentro del flujo de otorgamiento con semántica best-effort (el log persiste aunque FCM falle, AC-42) y los límites anti-spam protegen al paciente del ruido. Lo que necesita timing (vencimiento del x2, racha en riesgo, semanal) queda documentado como trabajo futuro con scheduler. |
| 27 | Weakness detection (P1.5, SPEC §21, "Paso 7c") | Nuevo log `app.weaknesses` + motor determinista de reglas (ADRED-inspired, sin ML) que detecta debilidades del paciente (adherencia nutricional, glucosa/% grasa, motivación, adherencia semanal, nutribiótico, ejercicio) y las persiste con dedupe por estado abierto (AC-43: no duplica mientras exista una `open`/`acknowledged`/`in_intervention` con el mismo código). La detección corre SOLO en `POST /scores/calculate`, una vez por recálculo (AC-45), después de puntajes + XP clínica + premios semanales. Cola clínica `GET /api/v1/program/weaknesses/open` (`Program.Adapt`) y transiciones `POST .../{id}/status` (`acknowledged`/`in_intervention`/`resolved`/`dismissed`) con guardia clínica AC-22 (un paciente → 403, AC-44); el paciente ve las suyas en `GET /api/v1/program/weaknesses` (`Program.View`). Umbrales clínicos marcados `REQUIRES_CLINICAL_VALIDATION`. La narrativa semanal LLM ("AI weekly assessment") queda DOCUMENTADA como contrato FUTURO (P3): requiere un endpoint nuevo en el ai-service + scheduler o disparo manual (SPEC §21.5); el backend de este paso NO llama IA (no existe el endpoint). | El motor convierte la misma data que ya alimenta los puntajes (SPEC §13/§18) en hallazgos accionables para el clínico, sin ML ni cron: reglas deterministas con dedupe idempotente dentro del recálculo manual existente. El LLM narrativo se difiere porque requiere un endpoint dedicado en el ai-service (no inventar llamadas que no existen). |

**Implementation contract**: any change that violates decisions 1, 3, 4, 5, 6, 7, 8, 9, 11, 14, 15, 16, 17, 18, 19 is a breaking change to the spec and must be discussed in a SPEC.md PR.

---

## 1. Scope and non-goals

### In scope (MVP / Phase 1)

- Reusable weekly template (1 default 83-week seeded template).
- Patient enrollment and per-week snapshot.
- Task completion with idempotent XP + streak.
- Day bonus and freeze grants.
- Adaptation recommendation queue (Pending → Approved/Rejected → Applied).
- Mobile snapshot endpoint + task completion endpoint + calendar/path endpoints.
- ERP template CRUD + enrollment list + adaptation review.
- **Health & Transformation Score engine** (P1.5, see §13): `app.health_score_weights` + `app.clinical_baselines` + `app.health_scores` + `app.transformation_scores`; on-read computation with stored history; `GET /api/v1/program/scores` and `POST /api/v1/program/scores/calculate`.
- **Gamified notifications** (P1.5, see §20): `app.notifications` log + push FCM transaccional best-effort en los flujos de otorgamiento (hitos de racha / nutribiótico, subida de nivel, día perfecto) + centro de notificaciones del móvil (`GET /api/v1/program/notifications`).
- **Weakness detection** (P1.5, see §21): `app.weaknesses` log + motor determinista de reglas (ADRED-inspired) disparado en `POST /scores/calculate` + cola clínica (`GET /api/v1/program/weaknesses/open`) y transiciones de estado (`POST .../{id}/status`) con guardia clínica AC-22; el paciente ve sus debilidades (`GET /api/v1/program/weaknesses`). La narrativa semanal LLM es FUTURO (contract in §21.5, no implementada).

### Out of scope (deferred)

- Podcast chapters / takeaways tables.
- Rewards marketplace, chests, cosmetic economy.
- Social leagues / cross-patient comparisons.
- **Timed/push-scheduled notifications** (multiplier expiring, streak at risk before midnight, weekly assessment) — require a scheduler; the backend has no cron/queue in MVP (§13.3). The transactional gamified notifications live in §20.
- **AI weekly assessment (LLM narrative)** — FUTURO (P3, §21.5): el contrato queda documentado pero NO implementado; requiere un endpoint nuevo en el ai-service (no existe) + scheduler o disparo manual. El motor determinista de debilidades (§21) SÍ está en scope.
- Multi-language UI strings on the mobile side (ES preserved; EN in P3).
- Bulk enrollment jobs, CSV export (P3).

---

## 2. Terminology and aggregate boundaries

| Term | Definition | Aggregate root |
|------|------------|----------------|
| **ProgramTemplate** | A reusable 83-week (configurable) program definition. Has many `WeeklyDayTemplate` rows. | `app.program_templates` |
| **WeeklyDayTemplate** | One row per `(template, weekday, task_code)` defining that a task type appears on that weekday with given base points. | `app.weekly_day_templates` |
| **ProgramEnrollment** | A patient's enrollment in one template. Owns streak state, XP balance, current week pointer, timezone. | `app.program_enrollments` |
| **ProgramWeek** | A specific week number (1..83) within an enrollment. Has its own `tasks_snapshot` (jsonb), `status` (Locked/Active/Completed). | `app.program_weeks` |
| **DailyCheckIn** | Per-day summary rollup (mood, barriers, total_points, is_perfect_day). | `app.daily_checkins` |
| **TaskCompletion** | One row per completed scheduled task. Owns the idempotency key and the resolved content FKs. | `app.task_completions` |
| **XpLedgerEntry** | Append-only XP movement. Reasons: `TaskCompletion`, `DailyBonus`, `AdaptationCorrection`. | `app.xp_ledger` |
| **StreakState** | Per-enrollment streak counters and freeze inventory. | `app.streak_states` |
| **StreakFreeze** | Audit row for each freeze granted or consumed. | `app.streak_freezes` |
| **AdaptationRecommendation** | A clinician-visible proposed change (difficulty, template swap, content refresh). | `app.adaptation_recommendations` |
| **MediaProgression** (P2) | Optional rotation table binding a MediaItem to a `(template, weekday)` window. | `app.media_progressions` |

**Aggregate rules**

- One transaction = one aggregate. Completing a task writes `task_completions` + (sometimes) `xp_ledger` + `daily_checkins` + `streak_freezes`/`streak_states` inside one `FOR UPDATE` on `program_enrollments`.
- Cross-aggregate reads are projections; never lock more than one enrollment at a time.
- The `xp_ledger.balance_after` is denormalized for cheap UI reads but must reconcile to `SUM(amount)` over the enrollment.

---

## 3. Relational schema (PostgreSQL `app` schema)

All tables live in `app.` (per repo convention). All `created_by`/`updated_by` are nullable `uuid` with a SQL-level FK to `auth.users(id)` (no EF navigation — same pattern as `app.patient_profiles`). All timestamps are `timestamptz`. Audit is handled by the existing `AuditTriggerInterceptor` (GUC actor propagation).

### 3.1 `app.program_templates`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `code` | `varchar(40)` | UNIQUE | `default-83w` seeded; clinician may add more |
| `name` | `varchar(120)` | NOT NULL | |
| `description` | `text` | NULL | |
| `total_weeks` | `int` | NOT NULL CHECK (`> 0`) | Default 83 |
| `status` | `varchar(20)` | NOT NULL DEFAULT `'Draft'` | `Draft` / `Active` / `Archived` (mirrors Wellness convention) |
| `version` | `int` | NOT NULL DEFAULT 1 | Incremented on every publish |
| `streak_min_tasks` | `smallint` | NOT NULL DEFAULT `1`, CHECK (`>= 1`) | Minimum completed tasks per patient-local day to maintain the streak (SPEC §17, B) |
| `essential_task_codes` | `jsonb` | NOT NULL DEFAULT `'[]'` | Task codes that count as "essential" for the freeze rescue rule (SPEC §17, C) |
| `created_by` | `uuid` | NULL, FK `auth.users(id)` | |
| `updated_by` | `uuid` | NULL, FK `auth.users(id)` | |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |
| `published_at` | `timestamptz` | NULL | Set when status moves to `Active` |

Indexes: `ix_program_templates_status`, `ix_program_templates_code` (unique already covers lookup).

### 3.2 `app.weekly_day_templates`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | |
| `template_id` | `uuid` | NOT NULL, FK `app.program_templates(id)` ON DELETE CASCADE | |
| `weekday` | `smallint` | NOT NULL CHECK (`weekday BETWEEN 1 AND 7`) | 1 = Monday … 7 = Sunday (matches NutritionPlanDay index) |
| `task_code` | `varchar(20)` | NOT NULL | `podcast`/`vitals`/`nut`/`ejercicio`/`nutribiotico`/`emocional` |
| `points` | `int` | NOT NULL CHECK (`>= 0`) | Base points awarded for this task |
| `sort_order` | `int` | NOT NULL DEFAULT 0 | UI ordering within the weekday |
| `media_id` | `uuid` | NULL, FK `app.media_items(id)` ON DELETE RESTRICT | Template-level podcast/content fallback used by P1 resolution (§4.4) |
| `created_by` | `uuid` | NULL, FK `auth.users(id)` | |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |

Indexes: `ix_weekly_day_templates_template_id`, `ix_weekly_day_templates_media_id`, `uq_weekly_day_templates_template_weekday_task` UNIQUE (`template_id, weekday, task_code`).

### 3.3 `app.program_enrollments`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | |
| `patient_id` | `uuid` | NOT NULL, FK `app.patient_profiles(id)` ON DELETE RESTRICT | |
| `template_id` | `uuid` | NOT NULL, FK `app.program_templates(id)` ON DELETE RESTRICT | |
| `timezone` | `varchar(64)` | NOT NULL DEFAULT `'America/Bogota'` | IANA |
| `status` | `varchar(20)` | NOT NULL DEFAULT `'Active'` | `Active` / `Paused` / `Completed` / `Withdrawn` |
| `started_at` | `timestamptz` | NOT NULL DEFAULT `now()` | The first server-side timestamp at enrollment creation |
| `start_local_date` | `date` | NOT NULL | The patient-local Monday of week 1 |
| `current_week_number` | `int` | NOT NULL DEFAULT 1 CHECK (`>= 1`) | Advanced by the daily tick or by completing a perfect week |
| `completed_at` | `timestamptz` | NULL | Set when `current_week_number = total_weeks` and last week's status moves to Completed |
| `paused_at` | `timestamptz` | NULL | |
| `withdrawn_at` | `timestamptz` | NULL | |
| `created_by` | `uuid` | NULL, FK `auth.users(id)` | Clinician who enrolled |
| `updated_by` | `uuid` | NULL, FK `auth.users(id)` | |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `ix_program_enrollments_patient_id`, `ix_program_enrollments_template_id`, `ix_program_enrollments_status`, `uq_program_enrollments_patient_active` UNIQUE (`patient_id`) WHERE `status = 'Active'` (one active enrollment per patient), `uq_program_enrollments_id_patient` UNIQUE (`id, patient_id`) (alternate key backing the composite emotional_records FK).

### 3.4 `app.program_weeks`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | |
| `enrollment_id` | `uuid` | NOT NULL, FK `app.program_enrollments(id)` ON DELETE RESTRICT | |
| `week_number` | `int` | NOT NULL CHECK (`>= 1`) | |
| `status` | `varchar(20)` | NOT NULL DEFAULT `'Locked'` | `Locked` / `Active` / `Completed` |
| `week_start_date_local` | `date` | NOT NULL | Monday in patient TZ |
| `week_end_date_local` | `date` | NOT NULL | Sunday in patient TZ |
| `tasks_snapshot` | `jsonb` | NOT NULL | Frozen list of `{weekday, task_code, points, sort_order}` taken at week start |
| `template_version_at_start` | `int` | NOT NULL | The `program_templates.version` active when the snapshot was taken |
| `activated_at` | `timestamptz` | NULL | When status moved to `Active` |
| `completed_at` | `timestamptz` | NULL | When status moved to `Completed` |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `ix_program_weeks_enrollment_id`, `uq_program_weeks_enrollment_week` UNIQUE (`enrollment_id, week_number`), `ix_program_weeks_status`, `ix_program_weeks_week_start_date_local`.

### 3.5 `app.daily_checkins`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | |
| `enrollment_id` | `uuid` | NOT NULL, FK `app.program_enrollments(id)` ON DELETE RESTRICT | |
| `program_week_id` | `uuid` | NOT NULL, FK `app.program_weeks(id)` ON DELETE CASCADE | |
| `local_date` | `date` | NOT NULL | Patient-local date |
| `weekday` | `smallint` | NOT NULL CHECK (`1..7`) | Denormalized for cheap filtering |
| `mood_score` | `smallint` | NULL CHECK (`1..5`) | From `emotional` task if completed |
| `barriers` | `varchar(40)` | NULL | One of the mobile `WEEK_BARRIERS` ids; only recorded if patient reports |
| `total_points` | `int` | NOT NULL DEFAULT 0 | Sum of `task_completions.points_awarded` for the day |
| `bonus_awarded` | `int` | NOT NULL DEFAULT 0 | 0 or 50 |
| `is_perfect_day` | `boolean` | NOT NULL DEFAULT false | True iff all scheduled tasks of the day were completed |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `ix_daily_checkins_enrollment_id`, `uq_daily_checkins_enrollment_date` UNIQUE (`enrollment_id, local_date`), `ix_daily_checkins_program_week_id`.

### 3.6 `app.task_completions`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | |
| `enrollment_id` | `uuid` | NOT NULL, FK `app.program_enrollments(id)` ON DELETE RESTRICT | |
| `program_week_id` | `uuid` | NOT NULL, FK `app.program_weeks(id)` ON DELETE CASCADE | |
| `daily_checkin_id` | `uuid` | NOT NULL, FK `app.daily_checkins(id)` ON DELETE CASCADE | |
| `local_date` | `date` | NOT NULL | |
| `weekday` | `smallint` | NOT NULL CHECK (`1..7`) | |
| `task_code` | `varchar(20)` | NOT NULL | |
| `points_awarded` | `int` | NOT NULL | Snapshot from `weekly_day_templates.points` at completion time |
| `client_request_id` | `varchar(64)` | NULL | Mobile retry key (UUID/ULID from client) |
| `completed_at` | `timestamptz` | NOT NULL DEFAULT `now()` | Server clock |
| `client_completed_at` | `timestamptz` | NULL | For audit when offline |
| `source_ref_type` | `varchar(20)` | NOT NULL | One of `manual`, `auto_vitals`, `auto_media`, `auto_nutrition`, `auto_exercise` |
| `content_fingerprint` | `varchar(64)` | NULL | hash(plan_day_id|routine_id|media_id|vitals_id|nutribiotic_id|emotional_id); for content-staleness detection |

**Nullable content FKs (one per task code; only the matching one is set)**:

| Column | Type | Constraint | Used by task_code |
|--------|------|------------|-------------------|
| `nutrition_plan_id` | `uuid` | NULL, FK `app.nutrition_plans(id)` | `nut` |
| `nutrition_plan_day_number` | `smallint` | NULL CHECK (`1..7`) | `nut` |
| `exercise_routine_id` | `uuid` | NULL, FK `app.exercise_routines(id)` | `ejercicio` |
| `media_id` | `uuid` | NULL, FK `app.media_items(id)` | `podcast` |
| `vital_signs_batch_id` | `uuid` | NULL, FK `app.vital_signs(id)` | `vitals` |
| `nutribiotic_product_id` | `uuid` | NULL, FK `erp.products(id)` | `nutribiotico` |
| `emotional_record_id` | `uuid` | NULL, FK new `app.emotional_records(id)` (created in P1, see §3.10) | `emocional` |

Indexes: `ix_task_completions_enrollment_id`, `ix_task_completions_daily_checkin_id`, `ix_task_completions_program_week_id`, `uq_task_completions_enrollment_date_task` UNIQUE (`enrollment_id, local_date, task_code`), `ix_task_completions_client_request_id` (lookup on retry).

### 3.7 `app.xp_ledger`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | |
| `enrollment_id` | `uuid` | NOT NULL, FK `app.program_enrollments(id)` ON DELETE RESTRICT | |
| `amount` | `int` | NOT NULL | Always positive in MVP (no revocation). |
| `reason` | `varchar(30)` | NOT NULL | `TaskCompletion` / `DailyBonus` / `AdaptationCorrection` |
| `source_ref_type` | `varchar(20)` | NULL | Same enum as `task_completions.source_ref_type` |
| `source_ref_id` | `uuid` | NULL | FK context (e.g., `task_completion.id`) |
| `rule_code` | `varchar(60)` | NULL, FK `app.xp_rules(code)` ON DELETE RESTRICT | Provenance: the catalog rule that produced this entry (§14). NULL for entries awarded without a rule (fallback) or predating the catalog. Prospective only: editing a rule never rewrites this column. |
| `balance_after` | `int` | NOT NULL | XP balance for this enrollment after this entry |
| `awarded_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `granted_by` | `uuid` | NULL, FK `auth.users(id)` | NULL for self-completions |
| `validated_by` | `uuid` | NULL, FK `auth.users(id)` (raw SQL, ON DELETE SET NULL) | Clinician who validated the award (SPEC §15) |
| `validated_at` | `timestamptz` | NULL | Set together with `validated_by` |
| `multiplier_used` | `numeric(4,2)` | NULL | Multiplicador EFECTIVO aplicado a la transacción (regla × paciente, SPEC §16, C.3). Null en filas previas a la columna (prospective only); 1.0 sin multiplicador |

Indexes: `ix_xp_ledger_enrollment_id`, `ix_xp_ledger_enrollment_awarded_at`, `uq_xp_ledger_source_dedupe` UNIQUE (`source_ref_type`, `source_ref_id`, `reason`) WHERE `source_ref_id IS NOT NULL`.

### 3.8 `app.streak_states`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `enrollment_id` | `uuid` | PK, FK `app.program_enrollments(id)` ON DELETE RESTRICT | One row per enrollment |
| `current_streak` | `int` | NOT NULL DEFAULT 0 | |
| `longest_streak` | `int` | NOT NULL DEFAULT 0 | |
| `last_active_date` | `date` | NULL | Patient-local date of last day contributing to current streak |
| `freezes_remaining` | `int` | NOT NULL DEFAULT 0 CHECK (`>= 0` AND `<= 3`) | Capped at 3 (PLAN open-question default) |
| `freezes_used_total` | `int` | NOT NULL DEFAULT 0 | Audit only |
| `last_break_date` | `date` | NULL | |
| `multiplier_active` | `numeric(4,2)` | NOT NULL DEFAULT `1.0` CHECK (`>= 1.0`) | Multiplicador x2 del paciente (SPEC §16): `1.0` = sin multiplicador, `2.0` = x2 vigente |
| `multiplier_ends_at` | `timestamptz` | NULL | Expiración del multiplicador vigente; null sin multiplicador (vencido → reset lazy a 1.0 en el próximo otorgamiento) |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `ix_streak_states_last_active_date`.

### 3.9 `app.streak_freezes`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | |
| `enrollment_id` | `uuid` | NOT NULL, FK `app.program_enrollments(id)` ON DELETE RESTRICT | |
| `kind` | `varchar(20)` | NOT NULL | `Granted` / `Consumed` / `Expired` |
| `used_on_local_date` | `date` | NULL | Set only for `Consumed` |
| `granted_at` | `timestamptz` | NULL | Set only for `Granted` |
| `granted_reason` | `varchar(40)` | NOT NULL | `PerfectWeekBonus` / `AdaptationApproval` / `Manual` |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |

Indexes: `ix_streak_freezes_enrollment_id`, `ix_streak_freezes_kind`.

### 3.10 `app.emotional_records` (small new table)

The emotional task writes a 1..5 mood score. We persist it as its own first-class row so clinicians can review history independent of XP. Other content (vitals, nutrition, etc.) is not duplicated — the existing tables are reused.

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | `uuid` | PK |
| `patient_id` | `uuid` | NOT NULL, FK `app.patient_profiles(id)` ON DELETE RESTRICT |
| `program_enrollment_id` | `uuid` | NULL; composite FK `(program_enrollment_id, patient_id)` → `app.program_enrollments(id, patient_id)` ON DELETE RESTRICT, backed by alternate key `uq_program_enrollments_id_patient` | Cross-patient invariant: when set, `patient_id` must match the enrollment's patient — a record can never reference another patient's enrollment |
| `recorded_local_date` | `date` | NOT NULL |
| `mood_score` | `smallint` | NOT NULL CHECK (`1..5`) |
| `barriers` | `varchar(40)` | NULL |
| `notes` | `text` | NULL |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` |

Indexes: `ix_emotional_records_patient_id`, `ix_emotional_records_program_enrollment_id`, `IX_emotional_records_program_enrollment_id_patient_id` (convention FK index), `uq_emotional_records_enrollment_date` UNIQUE (`program_enrollment_id, recorded_local_date`).

### 3.11 `app.adaptation_recommendations`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK | |
| `enrollment_id` | `uuid` | NOT NULL, FK `app.program_enrollments(id)` ON DELETE RESTRICT | |
| `kind` | `varchar(30)` | NOT NULL | `DifficultyChange` / `LevelChange` / `TemplateSwap` / `RoutineContentRefresh` / `NutritionPlanRefresh` / `MediaRotation` |
| `target_entity_type` | `varchar(30)` | NOT NULL | `weekly_day_templates` / `program_enrollments` / `nutrition_plans` / `exercise_routines` / `media_progressions` |
| `target_entity_id` | `uuid` | NOT NULL | |
| `payload` | `jsonb` | NOT NULL | Forward-looking change description (new points, new FKs, valid_from/to, etc.) |
| `reason` | `text` | NOT NULL | Why the engine or clinician proposed this |
| `status` | `varchar(20)` | NOT NULL DEFAULT `'Pending'` | `Pending` / `Approved` / `Rejected` / `Applied` / `Superseded` |
| `requires_approval` | `boolean` | NOT NULL | True for `DifficultyChange`, `LevelChange`, `TemplateSwap`. False for `*ContentRefresh`. |
| `requested_by` | `uuid` | NULL, FK `auth.users(id)` | NULL when auto-generated by the rule engine |
| `decided_by` | `uuid` | NULL, FK `auth.users(id)` | |
| `decided_at` | `timestamptz` | NULL | |
| `applied_at` | `timestamptz` | NULL | |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `ix_adaptation_recommendations_enrollment_id`, `ix_adaptation_recommendations_status`, `ix_adaptation_recommendations_kind`.

### 3.12 `app.media_progressions` (Phase 2)

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | `uuid` | PK |
| `template_id` | `uuid` | NOT NULL, FK `app.program_templates(id)` ON DELETE CASCADE |
| `weekday` | `smallint` | NOT NULL CHECK (`1..7`) |
| `media_id` | `uuid` | NOT NULL, FK `app.media_items(id)` ON DELETE RESTRICT |
| `sort_order` | `int` | NOT NULL DEFAULT 0 |
| `valid_from` | `date` | NOT NULL |
| `valid_to` | `date` | NULL |
| `created_by` | `uuid` | NULL, FK `auth.users(id)` |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` |

Indexes: `ix_media_progressions_template_id`, `ix_media_progressions_media_id`, `ix_media_progressions_valid_from_to` (`template_id, weekday, valid_from`).

### 3.13 Reuse from existing modules

We do **not** duplicate these tables:

| Table (existing) | Used for |
|------------------|----------|
| `app.patient_profiles` | Enrollment FK target; resolves `patient_id` from JWT user |
| `app.nutrition_plans` + `app.nutrition_plan_days` + `app.nutrition_plan_assignments` | `nut` task content |
| `app.exercise_routines` + `app.routine_assignments` | `ejercicio` task content |
| `app.media_items` | `podcast` task content |
| `app.vital_signs` | `vitals` task content |
| `erp.products` | `nutribiotico` task content |
| `auth.users` | FK target for actor columns and resolvers |

---

## 4. Relationship rules

### 4.1 Template-level vs enrollment-level vs runtime-derived

| Concern | Layer | Source of truth |
|---------|-------|-----------------|
| Which task types run on a given weekday | Template | `weekly_day_templates` |
| Base points per task on a given weekday | Template | `weekly_day_templates.points` |
| Patient-local calendar of weeks | Enrollment | `program_enrollments.start_local_date` + `timezone` |
| Frozen task list for a specific week | Week | `program_weeks.tasks_snapshot` (jsonb copy) |
| Active nutrition plan/day for a date | Runtime | `nutrition_plan_assignments` + `nutrition_plan_days` |
| Active exercise routine for a date | Runtime | `routine_assignments` |
| Active podcast media for a date | Runtime | `media_progressions` (P2) or `weekly_day_templates.media_id` fallback (P1) |
| Mood / barrier for a date | Runtime | `emotional_records` |

### 4.2 Plan 7-day mapping

`NutritionPlanDay.days_number` (1..7) maps directly to the weekday index (1..7) of the patient's program week. The mapping formula:

```
plan_day_number = weekday_index (1 = Monday … 7 = Sunday)
```

i.e., the plan day number and the weekday index are the **same integer**. The handler verifies that `NutritionPlan.Days.Any(d => d.DaysNumber == weekday)` exists at completion time; if not, the task is still recorded but the `nutrition_plan_id` FK is null and the UI shows "Plan del día no disponible" — completion does not error.

### 4.3 Routine weekday resolution

Routines are not intrinsically owned by a weekday. The active routine is selected by:

1. Read `routine_assignments` for the patient where `status = 'Active'` and `local_date BETWEEN start_date AND COALESCE(end_date, 'infinity')`.
2. If multiple match, prefer the one whose `start_date` is the most recent.
3. If none, the `ejercicio` task is recorded with `exercise_routine_id = NULL` and the UI shows "Sin rutina asignada".

### 4.4 Direct MediaItem links

The `podcast` task always writes a non-null `media_id`. Resolution:

- P1: `media_id` is taken from `weekly_day_templates.media_id` (a new nullable column on that table). Templates seeded by the system ship with a published podcast per weekday.
- P2: replaced by `media_progressions` lookup against `local_date` (`valid_from <= local_date AND (valid_to IS NULL OR local_date <= valid_to)`); falls back to `weekly_day_templates.media_id`.

### 4.5 Snapshot / versioning behavior

- **Template publish**: bumping `program_templates.version` does NOT rewrite existing `program_weeks.tasks_snapshot` rows. Future weeks pick up the new version automatically when their snapshot is taken.
- **Mid-week edits to the current template**: never affect weeks already in `Active` or `Completed` state. They apply at the next week boundary.
- **Withdrawing an enrollment**: blocks new `task_completions` but does not erase history.
- **Re-enrolling after withdrawal**: a new `program_enrollments` row is created; the patient keeps XP / streak history only on that new row's aggregate (no merge; old enrollment stays queryable for audit).

---

## 5. State machines & invariants

### 5.1 `program_enrollments.status`

```
Active ─▶ Paused ─▶ Active (resume)
       └─▶ Withdrawn (terminal)
       └─▶ Completed (when current_week_number = total_weeks AND last week Completed)
```

An enrollment is created directly in `Active` state (no `Draft`; `status` defaults to `'Active'` at insert time).

Invariants:

- `current_week_number <= total_weeks`.
- `paused_at IS NOT NULL ↔ status = 'Paused'`.
- `withdrawn_at IS NOT NULL ↔ status = 'Withdrawn'`.
- `completed_at IS NOT NULL ↔ status = 'Completed'`.
- While `status NOT IN ('Active')`, `POST /tasks/complete` returns `409 Conflict` with `code = "ENROLLMENT_INACTIVE"`.

### 5.2 `program_weeks.status`

```
Locked ─▶ Active ─▶ Completed
                └─▶ (skip) Completed via perfect-week auto-advance
```

Invariants:

- Exactly one row per `(enrollment_id, week_number)`.
- For any enrollment, at most one `Active` week at a time.
- A week auto-activates when `week_start_date_local <= today_local AND today_local <= week_end_date_local` AND the prior week is `Completed` (or `week_number = 1`).
- A week auto-completes when `today_local > week_end_date_local` AND `daily_checkins.is_perfect_day = true` for all 7 days.

### 5.3 `task_completions`

Invariants:

- One row per `(enrollment_id, local_date, task_code)` (DB-enforced unique).
- `points_awarded` is a snapshot of `weekly_day_templates.points` at the time the row was inserted; it is **never updated**, even if the template changes later.
- `completed_at - client_completed_at <= 14 days` (soft rejection beyond; configurable). Off-by-more-than-14-days offline writes still succeed but `client_completed_at` is preserved for audit.

### 5.4 `xp_ledger`

Invariants:

- `SUM(amount) over enrollment_id = balance_after` of the latest row (read-time check).
- `amount > 0` always (no revocation in MVP).
- Idempotency: never two rows for the same `(source_ref_type, source_ref_id, reason)`.

### 5.5 `streak_states`

Invariants:

- `freezes_remaining BETWEEN 0 AND 3`.
- `current_streak >= 0`.
- `longest_streak >= current_streak`.
- A day that **meets the threshold** (`tasks_done >= streak_min_tasks`, SPEC §17, B) increments `current_streak`. A missed day below the threshold consumes one freeze **only if ≥1 essential task was completed on that day** (SPEC §17, C); otherwise `current_streak` resets to 0, `last_break_date = today_local`, and the freeze stays in inventory (AC-32).

### 5.6 `adaptation_recommendations.status`

```
Pending ─▶ Approved ─▶ Applied (terminal)
        └▶ Rejected (terminal)
        └▶ Superseded (terminal; new Pending replaces it)
```

Invariants:

- Only `Approved` rows are eligible for `Applied`.
- `Applied` requires `applied_at IS NOT NULL` and writes audit row to `activity_logs` (existing audit pipeline).
- `Superseded` is set when a newer `Pending` of the same `kind` and `target_entity_id` is created; the older row's `status` is updated in the same transaction.

---

## 6. Business rules

### 6.1 Task scheduling

- A task is "scheduled" for `(weekday)` iff `weekly_day_templates` has a row for `(template, weekday, task_code)` in the snapshot of the current `program_week`.
- The mobile UI never invents tasks; it renders exactly what the snapshot returns.
- A scheduled task is "due today" iff `local_date` falls inside an `Active` `program_week`.

### 6.2 Completion

- `POST /api/v1/program/tasks/complete` body: `{ enrollmentId, localDate, taskCode, clientRequestId, clientCompletedAt?, moodScore?, barriers?, contentFingerprint? }`.
- Idempotency contract:
  - First call: inserts `task_completions` + matching `xp_ledger` row + updates `daily_checkins` totals + may insert a `DailyBonus` xp_ledger row.
  - Duplicate call with the same `(enrollmentId, localDate, taskCode)`: returns `200` with the **existing** completion (idempotent replay); never awards XP again.
  - Duplicate call with the same `clientRequestId` but different `(localDate, taskCode)`: returns `409 Conflict` with `code = "IDEMPOTENCY_KEY_REUSED"`.
- Completion is rejected (`409 ENROLLMENT_INACTIVE`) when the enrollment is not `Active`.
- Completion is rejected (`409 TASK_NOT_SCHEDULED`) when the task is not in the current week's snapshot.
- Completion is rejected (`422 CONTENT_FINGERPRINT_MISMATCH`) only when the client supplies a fingerprint and it differs from the resolved content. The client may override with `?force=true` (clinician use only — gated by `Program.ForceComplete` permission).

### 6.3 Partial / perfect day

- A day is "perfect" iff `daily_checkins.is_perfect_day = true`, i.e., every scheduled task for that weekday was completed before the day ends.
- `is_perfect_day` is recomputed transactionally each time a `task_completions` row is inserted.
- If the day becomes perfect, the handler inserts one `xp_ledger` row with `reason = 'DailyBonus'`, `amount = 50`. Unique key `('DailyBonus', daily_checkin_id)` prevents double-award.

### 6.4 XP exactly once

- A task_completion row creates exactly one `xp_ledger` row with `reason = 'TaskCompletion'`.
- Replays of the same completion return the existing ledger balance; no new row.
- Reconciliation job (P3) walks `xp_ledger` and recomputes `streak_states` per enrollment; not in MVP.

### 6.5 Bonus

- `50` points iff `is_perfect_day = true`.
- Awarded exactly once per day per enrollment. Idempotency: `('DailyBonus', daily_checkin_id)` unique partial.

### 6.6 Streak / freeze

- A day **meets the threshold** iff `tasks_done >= streak_min_tasks` (SPEC §17, B; default `1` — the seeded template keeps the pre-§17 behavior where ≥1 completed task maintains the streak). The threshold is read from the enrollment's template (`program_templates` via `program_enrollments.template_id`).
- A day that meets the threshold contributes to the streak: the day after a qualifying day, `current_streak += 1` and `last_active_date = today`.
- If `yesterday_local_date != last_active_date` AND `yesterday_local_date < today - 1 day` (i.e., gap > 1 day) AND `freezes_remaining > 0`, the handler consumes one freeze **only if at least one completed task on the missed day is in the template's `essential_task_codes`** (SPEC §17, C — "rescue requires an essential task", ADRED-inspired; AC-31). It inserts `streak_freezes(kind='Consumed', used_on_local_date=yesterday)`, decrements `freezes_remaining`, sets `last_active_date = today`, streak **unchanged**.
- Otherwise (no freeze, or freeze but no essential task on the missed day) `current_streak = 0`, `last_break_date = today`, **no XP change**; an unconsumable freeze stays in inventory (AC-32).
- A perfect day remains "all scheduled tasks completed" and is used unchanged for the day bonus (§6.3/§6.5) and the freeze grant cadence.
- Grant rule: 1 freeze every 7 consecutive perfect days, capped at 3 (configurable in `appsettings.json` under `Program:Streak:FreezeGrantEveryPerfectDays`) — unchanged (SPEC §17, C).

### 6.7 Weekly completion / unlock

- A week auto-completes when all 7 days are `is_perfect_day = true`.
- `current_week_number` advances by 1; the next `Locked` week transitions to `Active`.
- If a week ends without being perfect, it transitions to `Completed` with `is_perfect_week = false` and the patient simply continues; no XP penalty, no streak break (streak breaks per-day rule above, not weekly).

### 6.8 Adaptation recommendations / approval / application

- The rule engine (`ProgramAdaptationEngine`, deterministic, no ML) creates `adaptation_recommendations` rows based on:
  - 2+ missed perfect days in 7 days → `RoutineContentRefresh` (low difficulty change).
  - Mood consistently ≤ 2 for 7 days → `RoutineContentRefresh` (gentler variant).
  - Patient crosses a level threshold (XP > 5000) → `DifficultyChange` (always `requires_approval = true`).
- `kind IN ('DifficultyChange', 'LevelChange', 'TemplateSwap')` ⇒ `requires_approval = true`. Clinician uses `POST /api/v1/program/adaptations/{id}/decide` with `{ decision: 'Approve' | 'Reject', note? }`.
- `kind IN ('RoutineContentRefresh', 'NutritionPlanRefresh', 'MediaRotation')` ⇒ `requires_approval = false` and the row goes straight to `Applied` via `ProgramAdaptationEngine.ApplyAsync` in the same transaction.
- `Applied` rows write audit entries (`activity_logs.action = 'AdaptationApplied'`).

### 6.9 Missing content / inactive media

- If a content FK is null at completion time, the `task_completions` row is still recorded, but the mobile shows a `content_unavailable` badge and no XP is awarded (TBD; default MVP: XP awarded, content ref null — see Open Questions).
- If `media_items.status != 'Published'`, the resolver skips it; the handler falls back to `weekly_day_templates.media_id` default.

### 6.10 Overlapping assignments

- Two active `nutrition_plan_assignments` for the same date: rule prefers the one with the latest `start_date`. A clinician override can mark one as `Superseded`.
- Two active `routine_assignments` for the same date: same rule.

### 6.11 Patient timezone

- All `local_date` values are computed in `program_enrollments.timezone`. The mobile sends its device timezone on enrollment; the server normalizes to IANA and stores it.
- A patient traveling: streak / week math does not change, only the day-rollover moment. The handler does **not** auto-shift `start_local_date`.

### 6.12 Retries / idempotency (re-stated for emphasis)

- Idempotency at two layers:
  - **DB unique** on `task_completions(enrollment_id, local_date, task_code)`.
  - **App-layer** replay-safety: the handler reads first, writes second, returns existing state on conflict.
- `clientRequestId` is stored on `task_completions` for cross-call dedupe; if reused with different `(localDate, taskCode)`, the call returns `409 IDEMPOTENCY_KEY_REUSED`.

### 6.13 Concurrency

- Every state-changing handler runs inside `CreateExecutionStrategy().ExecuteAsync(...)` (same as `WellnessRepository.AddPlanWithAssignmentAsync`).
- The enrollment row is locked `FOR UPDATE` before any write to `task_completions`, `xp_ledger`, `daily_checkins`, `streak_states`, `streak_freezes`.
- Integration test: 50 concurrent `POST /tasks/complete` for the same `(enrollment, local_date, task_code)` resolves to exactly one `task_completions` row and one XP award.

### 6.14 Authorization

- Patient endpoints: `[Authorize]` and `ICurrentContext.UserId` resolves to `app.patient_profiles.id` (cached per request). Cross-patient reads return `404` (anti-IDOR).
- Clinician/admin endpoints require new permissions seeded in `auth.permissions`:
  - `Program.View` — list enrollments, read templates, read adaptations.
  - `Program.Edit` — CRUD templates, weekly day templates.
  - `Program.Enroll` — create / withdraw enrollments.
  - `Program.Adapt` — decide adaptations.
  - `Program.ForceComplete` — override fingerprint / schedule checks.
- Clinicians scope to their assigned patients only (existing `app.patient_professionals` rule, same as `Patients.ViewOwn`).

### 6.15 Clinical safety

- No clinical claim is ever derived from XP / streak. The Transformation/Health Score modules read `app.clinical_measurements` only.
- The mobile UI labels XP/level as gamification ("XP" / "Nivel") and clinical scores as measurements ("Índice de Transformación", "Índice de Salud").
- Adaptation recommendations can **never** lower a patient's safety floor (e.g., reduce protein below a `PlanSafetyRule` threshold) without an explicit clinician approval step.

---

## 7. API contract

Base path: `/api/v1/program`. All endpoints require `[Authorize]`. Standard error shape from `GlobalExceptionHandlerMiddleware` (existing).

### 7.1 `GET /api/v1/program/me/snapshot`

Returns the full program state for the patient's current enrollment, used by the mobile home screen.

**Response 200**

```json
{
  "enrollmentId": "3fa8…",
  "template": {
    "id": "…",
    "code": "default-83w",
    "name": "Programa 83 semanas",
    "totalWeeks": 83,
    "currentWeekNumber": 12,
    "currentWeekStatus": "Active",
    "currentWeekStartDateLocal": "2026-09-21",
    "currentWeekEndDateLocal": "2026-09-27",
    "streakMinTasks": 1,
    "essentialTaskCodes": ["nut","ejercicio","nutribiotico"]
  },
  "todayLocalDate": "2026-09-24",
  "todayTasks": [
    {
      "taskCode": "podcast",
      "title": "Escuchar podcast",
      "short": "Biohacking y metabolismo · 8 min",
      "points": 80,
      "status": "Pending",
      "completedAt": null,
      "content": {
        "mediaId": "…",
        "title": "…",
        "durationSecs": 492,
        "thumbnailUrl": "…"
      }
    },
    {
      "taskCode": "vitals",
      "title": "Medir signos vitales",
      "points": 120,
      "status": "Completed",
      "completedAt": "2026-09-24T11:14:08Z",
      "content": null
    }
  ],
  "todayPoints": 200,
  "todayBonusAvailable": true,
  "todayPointsMax": 750,
  "xp": { "balance": 1620, "level": "Constante", "nextLevelAt": 3000 },
  "streak": {
    "current": 11,
    "longest": 27,
    "freezesRemaining": 2,
    "multiplierActive": 2.0,
    "multiplierEndsAt": "2026-09-25T11:14:08Z",
    "multiplierRemainingHours": 23
  },
  "nextMilestoneDays": 16,
  "calendar": [ /* 7 days, each { localDate, weekday, isPerfectDay, points, status } */ ]
}
```

**Errors**: `404 NO_ACTIVE_ENROLLMENT`, `503` if Wellness/Media lookups fail (then return last cached snapshot with `stale=true`).

### 7.2 `POST /api/v1/program/tasks/complete`

**Request**

```json
{
  "enrollmentId": "3fa8…",
  "localDate": "2026-09-24",
  "taskCode": "podcast",
  "clientRequestId": "ulid-01H…",
  "clientCompletedAt": "2026-09-24T11:14:08Z",
  "moodScore": 4,
  "barriers": null,
  "contentFingerprint": "sha256:…"
}
```

**Response 200** (new completion)

```json
{
  "taskCompletionId": "…",
  "pointsAwarded": 80,
  "xpBalanceAfter": 1700,
  "isPerfectDay": false,
  "dailyBonusAwarded": 0,
  "streakCurrent": 11,
  "freezesRemaining": 2,
  "dayPoints": 280,
  "dayPointsMax": 750
}
```

**Response 200** (idempotent replay): identical body, no new XP awarded.

**Errors**: `409 ENROLLMENT_INACTIVE`, `409 TASK_NOT_SCHEDULED`, `409 IDEMPOTENCY_KEY_REUSED`, `422 CONTENT_FINGERPRINT_MISMATCH`, `422 DATE_OUTSIDE_ACTIVE_WEEK`.

### 7.3 `GET /api/v1/program/calendar?from=2026-09-01&to=2026-09-30`

Returns per-day rollups for the requested window (max 92 days). Used by the path view.

**Response 200**

```json
{
  "from": "2026-09-01",
  "to": "2026-09-30",
  "days": [
    {
      "localDate": "2026-09-01",
      "weekday": 1,
      "weekNumber": 10,
      "isPerfectDay": true,
      "points": 750,
      "bonusAwarded": 50,
      "completedTaskCodes": ["podcast","vitals","nut","ejercicio","nutribiotico","emocional"]
    }
  ],
  "summary": { "perfectDays": 18, "missedDays": 2, "totalXp": 1450 }
}
```

### 7.4 `GET /api/v1/program/path`

Returns the long-run path: list of weeks with `weekNumber`, `status`, `weekStartDateLocal`, `weekEndDateLocal`, `isPerfectWeek`, `points`. Used by the "sendero" view.

**Response 200**

```json
{
  "weeks": [
    { "weekNumber": 1, "status": "Completed", "isPerfectWeek": true, "points": 5250, "weekStartDateLocal": "2026-07-06", "weekEndDateLocal": "2026-07-12" },
    { "weekNumber": 12, "status": "Active", "isPerfectWeek": null, "points": 1820, "weekStartDateLocal": "2026-09-21", "weekEndDateLocal": "2026-09-27" },
    { "weekNumber": 13, "status": "Locked", "isPerfectWeek": null, "points": 0, "weekStartDateLocal": "2026-09-28", "weekEndDateLocal": "2026-10-04" }
  ]
}
```

### 7.5 Patient enrollment (clinician)

- `POST /api/v1/program/enrollments` — body `{ patientId, templateId, timezone, startLocalDate }`. Requires `Program.Enroll`.
- `POST /api/v1/program/enrollments/{id}/pause` — body `{ reason }`. Requires `Program.Enroll`.
- `POST /api/v1/program/enrollments/{id}/resume` — Requires `Program.Enroll`.
- `POST /api/v1/program/enrollments/{id}/withdraw` — body `{ reason }`. Requires `Program.Enroll`.
- `GET /api/v1/program/enrollments?patientId=&status=&page=1&pageSize=20` — Requires `Program.View`.

### 7.6 ERP — Templates

- `GET /api/v1/program/templates` — list. Requires `Program.View`.
- `GET /api/v1/program/templates/{id}` — Requires `Program.View`.
- `POST /api/v1/program/templates` — Requires `Program.Edit`.
- `PUT /api/v1/program/templates/{id}` — Requires `Program.Edit`.
- `POST /api/v1/program/templates/{id}/publish` — bump version, transitions Draft → Active. Requires `Program.Edit`.
- `POST /api/v1/program/templates/{id}/archive` — Requires `Program.Edit`.
- `GET /api/v1/program/templates/{id}/weekday-tasks` — list rows.
- `PUT /api/v1/program/templates/{id}/weekday-tasks` — bulk replace. Requires `Program.Edit`.

### 7.7 Adaptation review (clinician)

- `GET /api/v1/program/adaptations?enrollmentId=&status=&page=1&pageSize=20` — Requires `Program.View`.
- `POST /api/v1/program/adaptations/{id}/decide` — body `{ decision: 'Approve' | 'Reject', note? }`. Requires `Program.Adapt`.
- `GET /api/v1/program/adaptations/{id}` — Requires `Program.View`.

---

## 8. Backend mapping

### 8.1 Project placement

| Concern | Project | Folder |
|---------|---------|--------|
| Entities, enums, value objects | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/`, `Enums/ProgramProgress/` |
| MediatR commands/queries/validators/DTOs | `src/CoppAddresd.Application` | `Features/ProgramProgress/{Commands,Queries,Validators,DTOs}/` |
| EF Core configurations + DbContext updates | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/`, `Persistence/AppDbContext.cs`, `Migrations/` |
| Repository | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` |
| Controllers | `src/CoppAddresd.Api` | `Controllers/ProgramController.cs` (+ subcontrollers if split) |
| Seeder | `src/CoppAddresd.Infrastructure` | `SeedData/ProgramProgressSeeder.cs` |
| Permissions seeding | `src/Services/CoppAddresd.Auth` | new `ProgramProgressPermissions.cs` invoked from the existing Auth seeder |

Conventions mirror Wellness: `ProgramController` at `/api/v1/program`, MediatR commands named `<Verb><Entity>Command` (`CompleteTaskCommand`, `DecideAdaptationCommand`), validators via FluentValidation in `Validators/`, DTOs in `DTOs/`.

### 8.2 AppDbContext

- Extend `AppDbContext` (existing) with `DbSet<>` for the 12 new entities.
- `OnModelCreating` adds `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)` already in place; new `IEntityTypeConfiguration<T>` classes are picked up automatically.
- `npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "public")` is already configured; new migration lands in `public.__EFMigrationsHistory`.

### 8.3 Migration strategy

- One migration per phase boundary to keep reviews reviewable: `AddProgramProgressCore` (P1 schema), `AddProgramProgressAdaptations` (P2), `AddProgramProgressEnhancements` (P3).
- All migrations are **additive**. No column drops, no rename. New tables only.
- `dotnet ef migrations add AddProgramProgressCore --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api --output-dir Migrations`.
- Migrations are applied automatically by the main API at startup (existing behavior).

### 8.4 Seeder strategy

- `ProgramProgressSeeder` runs on startup (idempotent). It seeds:
  - 1 default `program_templates` row (`default-83w`) if missing.
  - The `default-83w` row seeds its streak config (SPEC §17, A): `streak_min_tasks = 1` y `essential_task_codes = ["nut","ejercicio","nutribiotico"]`. Solo en create: si la plantilla ya existe, el seeder no toca su configuración (misma regla idempotente que el resto).
  - 7 rows in `weekly_day_templates` (one per weekday) with the mobile mock points.
  - 7 rows for the 6 task codes that the template ships with (default content placeholders; clinician fills real `media_id` later).
  - The `podcast` rows seed `weekly_day_templates.media_id` with a published `app.media_items` fallback per weekday (template-level default for P1 resolution, §4.4).
  - Permissions: `Program.{View,Edit,Enroll,Adapt,ForceComplete}`.
  - 5 default `app.health_score_weights` rows (`adherence=0.30, clinical=0.30, nutrition=0.20, psychology=0.10, exercise=0.10`) for the Score engine (§13). Idempotent UPSERT by `dimension`.
  - 24 default `app.xp_rules` rows (SPEC §14.2 + §15.2 + §18.1 + §19.2): `TASK_PODCAST`..`TASK_EMOCIONAL`, `DAY_BONUS`, `STREAK_7`, `STREAK_11`, `STREAK_22`, `STREAK_50`, las 4 clínicas `CLINICAL_IMPROVE`, `CLINICAL_SIGNIFICANT`, `CLINICAL_STABLE`, `CLINICAL_WEEKLY_ALL_UP`, las 4 de nutrición `NUTRITION_MEAL_COMPLETE`, `NUTRITION_HYDRATION`, `NUTRITION_WEEK_85`, `NUTRITION_RECOVERY` y las 5 de la racha del nutribiótico `NB_STREAK_7`, `NB_STREAK_14`, `NB_STREAK_30`, `NB_STREAK_60`, `NB_STREAK_90` (SPEC §19.2). Idempotent by `code` (`ON CONFLICT (code) DO NOTHING`): an existing rule is never overwritten, so admin edits (§14.4) survive re-runs.
  - 5 `app.habit_templates` rows (SPEC §18.2, nutrición granular): las 4 comidas del móvil (`des`/`alm`/`mer`/`cen`, categoría `alimentacion`) + hidratación (`agua`, categoría `agua`). Idempotent by `code`; son la fuente de la dimensión `nutrition` del Health Score (§13.4.3) y del log de `POST /nutrition/log`.
- Seeder is **never** destructive (no DELETEs on production data).

### 8.5 Audit & permissions

- Actor: `ICurrentContext.UserId` resolves to `auth.users.id`. `AuditTriggerInterceptor` already writes `activity_logs` rows with the actor for every DML.
- Auditoría del módulo: los triggers de `audit.audit_trigger_function` (adjuntados vía `audit.attach_table_audit`) cubren **todas** las tablas del módulo — `program_templates`, `weekly_day_templates`, `program_enrollments`, `program_weeks`, `daily_checkins`, `task_completions`, `xp_ledger`, `streak_states`, `streak_freezes`, `adaptation_recommendations`, `xp_rules`, `clinical_xp_reviews` (SPEC §15; tabla sin PHI: ids, |Δ%| y estado), `habit_templates` y `habit_checks` (SPEC §18; tabla sin PHI: ids, código de comida y fecha local) y `notifications` (SPEC §20; tabla sin PHI: copy de gamificación y timestamps) — **excepto** `emotional_records`, excluida por diseño: `mood_score`/`barriers`/`notes` son PHI y nunca deben aterrizar en `activity_logs.old_data/new_data`, y **excepto** `weaknesses` (SPEC §21): su `description` puede contener contexto clínico del hallazgo, por lo que su DML tampoco se audita (misma exclusión por diseño). De `daily_checkins` los payloads jsonb excluyen `mood_score` y `barriers`. Los eventos semánticos que el trigger no puede generar (p. ej. `action = 'AdaptationApplied'` al aplicar una adaptación, AC-17) se insertan por SQL parametrizado en la misma transacción que el cambio de estado.
- Authorization: `RequirePermission("Program.View")` etc., using existing pattern from `WellnessController`.
- IDOR: any cross-patient `GET` returns `404`; `POST` endpoints always scope to `enrollmentId` whose `patient_id` matches the caller's `patient_profile_id`.

---

## 9. Frontend migration plan (mobile)

The mobile app keeps the existing UI shapes (XP gauge, level badge, 6 task cards, calendar, freeze chest). No redesign.

### 9.1 Read-then-write slices

| Slice | Mobile change | Backend dependency |
|-------|---------------|--------------------|
| 1 | Replace `src/data/program.ts` constants with `GET /program/me/snapshot` (cached in `AppContext`). Mock fallback if fetch fails. | Snapshot endpoint (P1) |
| 2 | `completeStep(id, pts)` → `POST /program/tasks/complete` (with `clientRequestId`). Optimistic UI; reconcile on response. | Complete endpoint (P1) |
| 3 | Calendar view → `GET /program/calendar`. | Calendar endpoint (P1) |
| 4 | Path/sendero view → `GET /program/path`. | Path endpoint (P1) |
| 5 | Adaptation notifications (toast on next launch) → `GET /program/adaptations?status=Applied`. | Adaptation endpoints (P2) |

### 9.2 Loading / error / offline fallback

- Every read endpoint has a stale-while-revalidate cache in `AppContext`. The mobile continues rendering the last snapshot if the API fails (banner: "Sin conexión — mostrando datos del DD/MM").
- Every write is queued in `localStorage` keyed by `clientRequestId`. On reconnect, queued writes are flushed in order. Idempotency keys prevent double XP.
- The mobile never throws a fatal error to the user on API failure. Demo continuity is the product contract.

### 9.3 What stays mock-only

- `HEALTH_PILLARS`, `TRANSFORM_ROWS` come from existing clinical-measurements endpoint (not in this spec; read separately).
- `EMOTION_FACES`, `WEEK_BARRIERS`, `BARRIER_REPLY` stay local constants.

---

## 10. Testing & acceptance

### 10.1 Layers

| Layer | Scope | Tooling |
|-------|-------|---------|
| Unit | Domain entities, validators, level math, perfect-day logic, streak math, freeze grant/consume, snapshot reader | xUnit + FluentAssertions |
| Repository | EF configurations, schema constraints, unique violations | xUnit + Npgsql + `COP_TEST_DB_CONNECTION` |
| Handler | MediatR commands with mocked `ICurrentContext`, mocked `ProgramRepository` | xUnit |
| Integration | End-to-end against real Postgres: idempotency, concurrency (50 parallel), timezone math, IDOR, audit rows written | xUnit + `tests/CoppAddresd.UnitTests` pattern (existing 7-test suite) |
| API contract | Snapshot, complete, calendar, path, enroll, decide | xUnit + WebApplicationFactory |
| Mobile contract | Snapshot shape matches mobile's existing mock types (TypeScript types generated from OpenAPI) | OpenAPI emitter + Pact broker (Phase 3) |

### 10.2 Mandatory acceptance scenarios

| ID | Scenario | Expected |
|----|----------|----------|
| AC-01 | Complete a `podcast` task on Tuesday of week 1 with `clientRequestId=abc`. | 1 `task_completions` row, 1 `xp_ledger` row, `balance_after = 80`. |
| AC-02 | Replay AC-01 with same `clientRequestId`. | 200 OK, **0 new rows**, balance unchanged. |
| AC-03 | Replay AC-01 with same `enrollmentId, localDate, taskCode` but different `clientRequestId`. | 200 OK with existing completion (DB unique), no new XP. |
| AC-04 | Replay AC-01 with same `clientRequestId` but different `localDate`. | `409 IDEMPOTENCY_KEY_REUSED`. |
| AC-05 | Complete all 6 tasks on the same day. | `is_perfect_day = true`, 1 `DailyBonus` row, balance += 50. |
| AC-06 | Complete all 6 tasks on day D and day D+1. | `current_streak = 2`. |
| AC-07 | Skip day D+1 (no completions). | `current_streak = 0`, `last_break_date = D+1`, no XP change. |
| AC-08 | With 7 perfect days in a row. | `freezes_remaining = 1`, 1 `streak_freezes(kind='Granted')` row. |
| AC-09 | With 7 consecutive perfect days (streak = 7) and 1 freeze, the patient skips day D+8 entirely (no completions). | `current_streak = 0`, `last_break_date = D+8`, `freezes_remaining = 1` (kept in inventory) — a freeze cannot rescue a day with **zero essential tasks** (rescue requires an essential task, SPEC §17 C; supersedes the pre-§17 behavior that consumed the freeze on any missed day). |
| AC-10 | 50 concurrent `POST /tasks/complete` for the same task. | Exactly 1 `task_completions` row, exactly 1 XP award. |
| AC-11 | Patient A's session tries to complete a task on patient B's enrollment. | `404 NOT_FOUND`. |
| AC-12 | Clinician pauses enrollment, patient tries to complete. | `409 ENROLLMENT_INACTIVE`. |
| AC-13 | Patient completes a `nut` task on a day where the plan has no day for that weekday. | `task_completions` row with `nutrition_plan_id = NULL` and `nutrition_plan_day_number = NULL`; UI badge `content_unavailable`; XP awarded (per MVP default). |
| AC-14 | Patient travels from `America/Bogota` to `America/New_York` mid-week. | `local_date` math stays in `America/Bogota`; no streak break from the move. |
| AC-15 | Clinician edits `weekly_day_templates.points` for Tuesday mid-week. | The current week's `tasks_snapshot` is unchanged; next week's snapshot uses the new value. |
| AC-16 | Patient crosses 5000 XP. | An `adaptation_recommendations` row with `kind='DifficultyChange', requires_approval=true` is created; the ERP queue shows it. |
| AC-17 | Clinician approves AC-16. | Row transitions to `Approved`, then `Applied`; `audit.activity_logs` row written. |
| AC-18 | Patient A reads snapshot while backend's Wellness lookup fails. | Returns last cached snapshot with `stale=true` (header `X-Snapshot-Stale: true`); never 5xx to mobile. |
| AC-19 | Patient with 7 days of mixed checkins (3 perfect, 2 partial, 1 freeze-rescued, 1 missed) and known clinical baselines + measurements. | `GET /api/v1/program/scores` returns a Health Score whose 5 dimensions follow the bands in §13.4: `adherence` = `round((3*1.0 + 2*0.6 + 1*0.4 + 1*0.0) / 7 * 100) = 66`; `clinical`, `nutrition`, `psychology`, `exercise` computed per the same section; weighted sum is `round(d1*0.30 + d2*0.30 + d3*0.20 + d4*0.10 + d5*0.10)`; a single `app.health_scores` row is persisted for the period. |
| AC-20 | Patient with 3 baseline metrics (weight favorable -1, BMI favorable -1, glucose favorable -1) and latest measurements showing `|Δ%|` = `18%`, `7%`, `12%` respectively. | `GET /api/v1/program/scores` returns a Transformation Score = `round((100 + 75 + 90) / 3) = 88`, `detail.weight.score = 100`, `detail.bmi.score = 75`, `detail.glucose.score = 90`, `overall_trend = "up"` (or `"stable"` if no `score_previous`). A single `app.transformation_scores` row is persisted for `week_number = enrollment.current_week_number`. |
| AC-21 | Patient with no `app.clinical_baselines`, no checkins, no emotional records, no habit_checks. | Health Score is computed with the **no-data neutral values**: `clinical = 50`, `nutrition = 0`, `psychology = 60`, `exercise = 0`, `adherence = 0`; weighted score is computed and persisted; UI never errors. Transformation Score returns `0` and `detail = {}`. |
| AC-22 | Patient attempts to insert an `app.clinical_baselines` row (e.g., via direct API or a self-service flow). | The Application layer rejects the write: `set_by` is required and must resolve to a clinician `auth.users.id` (a user with a clinical role: `Physician`, `Nutritionist`, `Psychologist`, `ClinicalDirector`, or `Admin`); patient callers receive `403 FORBIDDEN`. `target_value`, when provided, must satisfy `target_value > 0` and reasonable unit bounds. |
| AC-23 | Patient completes the same task a second time on the same day while the active rule `TASK_PODCAST` has `max_per_day = 1` (e.g., a direct second call that bypasses the app-layer idempotency check). | The second completion is rejected by the rule engine with `409 XP_DAILY_LIMIT_REACHED` (count of `app.xp_ledger` rows for `(enrollment, rule_code, local day)` already equals the limit); the day/weekly counters also enforce `max_per_week`. The first award row carries `rule_code = 'TASK_PODCAST'`. |
| AC-24 | Clinician sets `active = false` (or `valid_until` in the past) on rule `TASK_NUT` via `PUT /api/v1/program/xp-rules/TASK_NUT`. | Subsequent `nut` completions fall back to the default behavior: points = `weekly_day_templates.points` (snapshot), no multiplier, no limits, `xp_ledger.rule_code = NULL`. Existing ledger history is untouched (prospective only). |
| AC-25 | Clinician runs `POST /scores/calculate` for a patient whose weight dropped 8% favorably vs baseline (threshold 5%) and whose glucose improved 3%. | 1 `clinical_xp_reviews` row `pending` for weight (no XP yet); `CLINICAL_IMPROVE` +50 awarded for glucose (`source_ref_type = 'clinical_period'`, `source_ref_id = health_scores.id`, `reason = 'CLINICAL_IMPROVE'`); re-running `/calculate` for the same period does NOT double-award (partial unique). |
| AC-26 | Clinician approves the pending review via `POST /xp-rules/clinical-pending/{id}/decide { "approve": true }`. | Review → `approved`; `xp_ledger` gains `CLINICAL_SIGNIFICANT` +100 with `validated_by`/`validated_at` set; the XP total/summary queries now include it. Before the approval (while pending) the review's XP was NOT counted in the totals. Re-deciding → `409 REVIEW_ALREADY_DECIDED`. |
| AC-27 | `/scores/calculate` runs for a patient whose glucose rose 10% unfavorably vs baseline. | **0 XP** awarded for the metric (never penalizes, no streak impact, no punishment mechanics); no review row is created; if the rest of the metrics are favorable the `CLINICAL_WEEKLY_ALL_UP` requires ALL metrics favorable → it is NOT awarded in this period. |
| AC-28 | Patient's streak reaches exactly 11 days (11th consecutive perfect day). | `STREAK_11` XP awarded **once** (`source_ref_type='streak_milestone'`, `source_ref_id=streak_states.enrollment_id`, `reason='STREAK_11'`); `streak_states.multiplier_active = 2.0` with `multiplier_ends_at = now + 24h`; a rebuilt streak that re-reaches day 11 neither re-awards nor re-activates (shared idempotency guard). Milestone 7 awards `STREAK_7` **without** activating a multiplier. |
| AC-29 | Patient completes tasks while the x2 window is active. | Task completions, day bonus and any other award use `total = floor(base × rule.Multiplier × 2.0)`; each `xp_ledger` row records `multiplier_used = rule.Multiplier × 2.0`. |
| AC-30 | The x2 window expires (`multiplier_ends_at` in the past) before the next award. | The next award treats the multiplier as 1.0 and **lazily resets** `streak_states.multiplier_active = 1.0` / `multiplier_ends_at = null` inside the award transaction; the snapshot shows `multiplierActive = 1.0`, `multiplierEndsAt = null`, `multiplierRemainingHours = 0`. |
| AC-31 | Template with `streak_min_tasks = 3`: the patient completes 2 tasks on day D (below threshold). | Day D does **not** maintain the streak. On the next day that meets the threshold, IF the patient has a freeze AND completed at least one essential task (nut/ejercicio/nutribiotico) on day D, the freeze is consumed (`streak_freezes(kind='Consumed', used_on_local_date=D)`) and the streak is preserved (SPEC §17 C). |
| AC-32 | Same setup as AC-31 (day D below threshold) but the patient completed **no** essential task on day D, with a freeze available. | The streak breaks (`current_streak = 0`, `last_break_date = D`) and the freeze is **NOT consumed** — it stays in inventory (`freezes_remaining` unchanged, SPEC §17 C). |
| AC-33 | Patient logs a meal via `POST /program/nutrition/log { mealCode: 'des' }`. | 1 `app.habit_checks` row (único por `(patient, habit_template, local_date)`), 1 `xp_ledger` row `NUTRITION_MEAL_COMPLETE` +10 (rule `NUTRITION_MEAL_COMPLETE`, `source_ref_type='habit_log'`, `source_ref_id=habit_check.id`), max 4/día (una por comida: des/alm/mer/cen); loguear la misma comida otra vez el mismo día → `409 HABIT_ALREADY_LOGGED`, sin doble XP. |
| AC-34 | Patient logs hydration via `POST /program/nutrition/log { mealCode: 'agua' }`. | 1 `xp_ledger` row `NUTRITION_HYDRATION` +5, tope 1/día (regla `NUTRITION_HYDRATION`); duplicado → `409 HABIT_ALREADY_LOGGED`. |
| AC-35 | Clinician runs `POST /scores/calculate` for a period where the patient's nutrition adherence (habit_checks categoría `alimentacion`, misma fuente que la dimensión `nutrition` del Health Score) is ≥ 85%. | 1 `xp_ledger` row `NUTRITION_WEEK_85` +75 (`source_ref_type='nutrition_period'`, `source_ref_id=health_scores.id`, `reason='NUTRITION_WEEK_85'`); re-corrrer `/calculate` para el mismo período NO duplica (dedupe parcial). Sin logs en el período → 0 XP (nunca penaliza). |
| AC-36 | Clinician runs `POST /scores/calculate` and the current period's adherence is ≥ 20 points above the previous period's (the prior `health_scores` row's nutrition dimension). | 1 `xp_ledger` row `NUTRITION_RECOVERY` +50 (mismo dedupe `nutrition_period`); los premios semanales respetan el multiplicador del paciente vigente (SPEC §16, C.4). |
| AC-37 | Patient completes the `nutribiotico` task 7 consecutive patient-local days (each completion is the first write of that day). | `app.streak_states` shows `nb_current_streak = 7`, `nb_longest_streak = 7`, `nb_last_completed_date = día 7`; exactly 1 `xp_ledger` row `NB_STREAK_7` +50 (`source_ref_type='nb_milestone'`, `source_ref_id=task_completions.id` de la completación del día 7, `reason='NB_STREAK_7'`, multiplicador del paciente aplicado). Re-completar el día 7 (replay) NO duplica la XP. La racha general y `TASK_NUTRIBIOTICO` (80 base) quedan intactas. |
| AC-38 | Patient completes `nutribiotico` on day D, skips D+1 (no completions), and completes again on D+2 — with a freeze available. | The NB streak is **not** protected by freezes: on D+2 the new count is `1` (`nb_current_streak` reset; `nb_last_completed_date = D+2`), no freeze is consumed, and no NB milestone is awarded. The general streak may still be rescued by a freeze (SPEC §17, C) — both streaks are independent. |
| AC-39 | Patient completes a 7-day NB run (awards `NB_STREAK_7`), then resets (misses a day), then completes a NEW 7-day run. | The new run re-awards `NB_STREAK_7` once when it reaches day 7 again (new `source_ref_id = task_completions.id`, no dedupe collision): each 7/14/30/60/90 run awards its milestone when reached. Within the SAME calendar week the re-award is capped by `NB_STREAK_7.max_per_week = 1` (the award is skipped, never an error). |
| AC-40 | Patient reaches a streak milestone (7/11/22/50), an NB milestone (7/14/30/60/90), crosses a level threshold, or completes a perfect day — each on its FIRST award (never on replay). | Exactly one `app.notifications` row is inserted with the matching type (`milestone_reached` / `nb_milestone` / `level_up` / `day_complete`) and the FCM push is attempted through the existing device-token path (`app.device_tokens` + `IFcmClient`). The XP award transaction is never affected by the notification outcome (best-effort, SPEC §20, B). |
| AC-41 | Patient already received 2 notifications of the same type (or 6 notifications total) on the same patient-local day; another event of that type fires. | The notification is skipped silently: no `app.notifications` row, no FCM push, only a debug log. The XP award proceeds normally (anti-spam, SPEC §20, B). |
| AC-42 | The FCM send fails (network error, invalid token, FCM disabled) while a milestone/level/perfect-day event fires. | The notification log row IS persisted and the XP award transaction commits normally; the send failure is caught and logged, never propagated (best-effort, SPEC §20, B). |
| AC-43 | Clinician runs `POST /scores/calculate` for a patient with nutrition adherence 58% and weekly adherence 45%. | The rules engine fires `WK_NUT_LOW_ADHERENCE` (medium) and `WK_ADH_LOW_STREAK` (medium) and persists **2 new** `app.weaknesses` rows (`source='ai'`, `status='open'`, `detected_at` set). Re-running `/calculate` for the same period does **NOT** duplicate: the open-status dedupe skips both codes (SPEC §21, C). |
| AC-44 | Clinician transitions a weakness via `POST /api/v1/program/weaknesses/{id}/status { status: 'resolved' }`. | The row moves to `resolved` with `resolved_at` set; the clinician's `auth.users.id` and roles are validated (AC-22 — a patient calling the endpoint receives `403 FORBIDDEN`). Applying the same status again is idempotent (no error). `acknowledged`/`in_intervention`/`dismissed` are also valid transitions; `open` is rejected (a row already starts open). |
| AC-45 | Weakness detection runs once per `POST /scores/calculate` invocation (after scores + clinical XP + weekly nutrition awards) and never on `GET /scores`. | The `CalculateScoresCommandHandler` calls `IWeaknessDetectionService.DetectAndPersistAsync` exactly once per recalculation; `GET /api/v1/program/scores` never detects or persists weaknesses. A patient without an active enrollment gets an empty detection result (no error, no rows). |

### 10.3 DB / concurrency / idempotency / auth tests

- DB constraint tests: directly insert a duplicate `(enrollment_id, local_date, task_code)` row and assert `23505 unique_violation`.
- Concurrency test: spawn N tasks with `Task.WhenAll` that each call `POST /tasks/complete`; assert the DB row count.
- Idempotency: covered by AC-02..AC-04.
- Auth: covered by AC-11, AC-12.

### 10.4 Performance

- `GET /program/me/snapshot` p95 < 200ms with 10k enrollments (single round-trip; no N+1; snapshot fields are precomputed via jsonb on `program_weeks`).
- `POST /tasks/complete` p95 < 150ms under nominal load.

---

## 11. Deferred extensions

These are explicitly **out of MVP** and not designed here. Re-evaluate after P3.

| Item | Why deferred |
|------|--------------|
| Podcast chapters / takeaways | Backend can deliver `media_items` only; mobile can keep the mock `chapters` array locally. No backend table until product needs persistence. |
| Rewards marketplace / chest economy | Mock `NEXT_CHEST_DAYS = 50` becomes a derived `nextMilestoneDays` field. No `chest` table until there's a real marketplace. |
| Social leagues | Cross-patient comparisons raise privacy questions; needs legal review. |
| Timed push notifications (multiplier expiring, streak at risk, weekly assessment) | Need a scheduler; the backend has no cron/queue in MVP (§13.3). The transactional gamified notifications (first-award events) live in SPEC §20. |
| i18n on mobile | ES preserved; EN added in P3. |
| Bulk enrollment jobs, CSV export | Admin scale features; planned in P3. |
| `program_weeks.tasks_snapshot` rolling archive | If storage grows, archive snapshots older than 52 weeks; not in MVP. |
| Reconciliation job | Walk `xp_ledger` and recompute `streak_states` nightly; not in MVP. |

---

## 12. Source-of-truth map

| Doc | Role |
|-----|------|
| [`PLAN.md`](./PLAN.md) | Master plan, phasing, decisions in narrative form |
| [`TASKS.md`](./TASKS.md) | Dependency-ordered implementation backlog |
| `coppAddresdBack/AGENTS.md` | Repo commands, schema conventions, gotchas |
| `coppAddresdBack/docs/modules/patients/PLAN.md` | Reference for module-doc conventions |
| `antares-paciente/src/data/program.ts` | Mock this module replaces |
| `antares-paciente/src/types.ts` (`ProgramTaskId`) | The 6 task codes this spec binds to |
| `antares-paciente/src/pages/ProgramPage.tsx` | Mobile UI being wired to the backend (Evolución tab consumes `GET /program/scores`, §13) |
| `app.clinical_measurements`, `app.measurement_metrics`, `app.unit_of_measures` | Existing Clinical Measurements module — source data for `app.clinical_baselines` and the Health/Transformation score calculator (§13) |

---

## 13. Health & Transformation Score engine (P1.5)

The Health and Transformation Scores are **real, computed** indicators that replace the static mock values in `antares-paciente/src/pages/ProgramPage.tsx` and `src/data/program.ts` (Health 86 / Transformation 87). The engine is the first of a planned reference-system adoption; the design below is a deliberate adaptation (no cron / no ML in MVP, see §13.3).

### 13.1 New tables (4) in `app.`

All conventions match §3 (nullable `created_by`/`updated_by` → `auth.users(id)`, `timestamptz` timestamps, audit via existing `AuditTriggerInterceptor`).

#### 13.1.1 `app.health_score_weights`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `dimension` | `varchar(20)` | UNIQUE NOT NULL | One of `adherence`, `clinical`, `nutrition`, `psychology`, `exercise` |
| `weight` | `numeric(5,4)` | NOT NULL CHECK (`weight >= 0 AND weight <= 1`) | Defaults: 0.30 / 0.30 / 0.20 / 0.10 / 0.10 |
| `description` | `text` | NULL | |
| `created_by` | `uuid` | NULL, FK `auth.users(id)` | |
| `updated_by` | `uuid` | NULL, FK `auth.users(id)` | |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `uq_health_score_weights_dimension` UNIQUE (`dimension`).
Application-layer invariant: `SUM(weight) = 1.0000` (validated on any write). Seeder inserts the 5 default rows idempotently (`ON CONFLICT (dimension) DO NOTHING`).

#### 13.1.2 `app.clinical_baselines`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `patient_id` | `uuid` | NOT NULL, FK `app.patient_profiles(id)` ON DELETE RESTRICT | |
| `metric_id` | `uuid` | NOT NULL, FK `app.measurement_metrics(id)` ON DELETE RESTRICT | e.g., `weight`, `bmi`, `body_fat`, `waist`, `glucose` |
| `value` | `numeric(10,4)` | NOT NULL | The baseline value, in `unit_id` units |
| `unit_id` | `uuid` | NOT NULL, FK `app.unit_of_measures(id)` ON DELETE RESTRICT | |
| `favorable_direction` | `smallint` | NOT NULL CHECK (`favorable_direction IN (-1, 1)`) | `-1` = lower is better (e.g., weight, glucose); `+1` = higher is better (e.g., adherence proxy) |
| `target_value` | `numeric(10,4)` | NULL | Optional clinician target. Requires `target_value > 0` and unit bounds (validation in Application). |
| `measured_at` | `date` | NOT NULL | The patient-local date the baseline reflects |
| `set_by` | `uuid` | NOT NULL, FK `auth.users(id)` (raw SQL; no EF navigation) | Must resolve to a clinician (AC-22). Patient self-set is rejected. |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `uq_clinical_baselines_patient_metric` UNIQUE (`patient_id`, `metric_id`), `ix_clinical_baselines_patient_id`, `ix_clinical_baselines_metric_id`, `ix_clinical_baselines_set_by`.

#### 13.1.3 `app.health_scores`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `patient_id` | `uuid` | NOT NULL, FK `app.patient_profiles(id)` ON DELETE RESTRICT | |
| `score` | `integer` | NOT NULL CHECK (`score BETWEEN 0 AND 100`) | Weighted sum, rounded |
| `score_previous` | `integer` | NULL CHECK (`score_previous BETWEEN 0 AND 100`) | The previous persisted score for the same patient (or NULL on first record) |
| `score_adherence` | `integer` | NOT NULL CHECK (`score_adherence BETWEEN 0 AND 100`) | |
| `score_clinical` | `integer` | NOT NULL CHECK (`score_clinical BETWEEN 0 AND 100`) | |
| `score_nutrition` | `integer` | NOT NULL CHECK (`score_nutrition BETWEEN 0 AND 100`) | |
| `score_psychology` | `integer` | NOT NULL CHECK (`score_psychology BETWEEN 0 AND 100`) | |
| `score_exercise` | `integer` | NOT NULL CHECK (`score_exercise BETWEEN 0 AND 100`) | |
| `trend` | `varchar(10)` | NOT NULL CHECK (`trend IN ('up','down','stable')`) | Derived from `score` vs `score_previous` |
| `period_start` | `date` | NOT NULL | Patient-local, 7-day rolling window or current program week (see §13.2) |
| `period_end` | `date` | NOT NULL | Patient-local |
| `calculated_at` | `timestamptz` | NOT NULL DEFAULT `now()` | Server clock |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `uq_health_scores_patient_period` UNIQUE (`patient_id`, `period_start`, `period_end`), `ix_health_scores_patient_id_period_end` (`patient_id`, `period_end` DESC).

#### 13.1.4 `app.transformation_scores`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `patient_id` | `uuid` | NOT NULL, FK `app.patient_profiles(id)` ON DELETE RESTRICT | |
| `score` | `integer` | NOT NULL CHECK (`score BETWEEN 0 AND 100`) | Average across indicators, rounded |
| `score_previous` | `integer` | NULL CHECK (`score_previous BETWEEN 0 AND 100`) | |
| `week_number` | `integer` | NOT NULL CHECK (`week_number >= 1`) | Snapshot of `program_enrollments.current_week_number` at calculation time |
| `detail` | `jsonb` | NOT NULL DEFAULT `'{}'::jsonb` | `{ metricCode: { baseline, current, unit, delta, delta_pct, favorable, score } }` |
| `overall_trend` | `varchar(10)` | NOT NULL CHECK (`overall_trend IN ('up','down','stable')`) | Derived from `score` vs `score_previous` |
| `calculated_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `ix_transformation_scores_patient_week` (`patient_id`, `week_number` DESC).

### 13.2 Period definition

The score period is computed in the patient's local timezone (`program_enrollments.timezone`).

- **Health Score period** = the last **7 patient-local days** ending on today's patient-local date, **or** the current program week window if today is inside an `Active` `program_week` whose `week_start_date_local` is more than 7 days ago. The handler picks the longer window when the program week extends further back, so the dimension samples stay aligned with the week the patient is on. Concretely: `period_start = min(today_local - 6 days, program_week.week_start_date_local)`, `period_end = today_local` (or `program_week.week_end_date_local` if the week is already past — but recompute uses the latest not-yet-`Completed` week).
- **Transformation Score period** = the current program week; `week_number = program_enrollments.current_week_number`. When the current week is already `Completed`, use the most recent `Completed` week. A new row is written every time `current_week_number` changes or the stored row is stale.
- "Stale" = the stored `period_end` < today in patient-local time.

### 13.3 Compute-on-read (deliberate adaptation)

The reference system runs weekly cron jobs to compute scores. **This backend has no cron / queue infrastructure** (AGENTS.md and the existing `IJobDispatcher` are scoped to admin bulk operations; introducing a generic scheduler is out of scope here). The engine therefore computes on demand and persists:

1. `GET /api/v1/program/scores` invokes the calculator services (`IHealthScoreCalculator`, `ITransformationScoreCalculator`) for the caller's current period.
2. The repository checks for an existing `app.health_scores` / `app.transformation_scores` row at `UNIQUE (patient_id, period_start, period_end)`.
3. If the row is missing or stale (see §13.2), the service **persists a new row** with the freshly computed values. The read path therefore is also a write path; the persisted row is the next call's `score_previous`.
4. The response always includes the freshly computed `current` and the persisted `previous` so the mobile can render the trend without a second round trip.

**Future cron / queue integration (deferred)**: a background job (added in a later phase, e.g., AI weekly assessment) will pre-warm `app.health_scores` and `app.transformation_scores` rows and trigger aggregate trend notifications. When that lands, the on-read path becomes a fallback. Documented here so the design is forward-compatible.

### 13.4 Health Score formula

```
score = round(
    adherence   * w_adherence
  + clinical    * w_clinical
  + nutrition   * w_nutrition
  + psychology  * w_psychology
  + exercise    * w_exercise
)
```

where `w_*` are read from `app.health_score_weights` (defaults 0.30 / 0.30 / 0.20 / 0.10 / 0.10; sum = 1.0000). The score is an `integer` in `[0, 100]`.

#### 13.4.1 `adherence` (0..100)

Sourced from `app.daily_checkins` and `app.streak_freezes` for the period.

| Day status | Weight |
|------------|--------|
| `is_perfect_day = true` | `1.0` |
| Partial: `tasks_done > 0` and not perfect | `0.6` |
| Rescued by a consumed freeze on that date (a `streak_freezes(kind='Consumed', used_on_local_date = day)` row exists) | `0.4` |
| Missed (no completions, no freeze) | `0.0` |

`adherence = round(sum(weights) / day_count * 100)`. `day_count` = number of patient-local days in the period (7 by default).

If there are no `daily_checkins` rows in the period, `adherence = 0`.

#### 13.4.2 `clinical` (0..100)

For each `app.clinical_baselines` row for the patient, find the **latest** `app.clinical_measurements` row in the period for the same `metric_id`. Compare against the baseline:

- `favorable && |pct_change| > 5%` → `100`
- `favorable && |pct_change| > 1%` → `75`
- `|pct_change| <= 1%` → `50`
- `unfavorable && |pct_change| > 1%` → `25`
- `unfavorable && |pct_change| > 5%` → `10`

where `pct_change = (current - baseline) / baseline` and `favorable` follows `favorable_direction` (sign of `pct_change * favorable_direction > 0`).

`clinical = round(avg(score_per_metric) across metrics that have a baseline AND a measurement in the period)`. If there are no baselines **or** no measurements, `clinical = 50` (neutral; same default the reference system uses for "no data").

#### 13.4.3 `nutrition` (0..100)

Sourced from `app.habit_checks` rows for the patient in the period where the linked habit template's `category = 'alimentacion'` (or any meal-type habit):

`nutrition = round(achieved / total * 100)` where `achieved` = habits marked done, `total` = habits scheduled. If no logs exist, `nutrition = 0`.

#### 13.4.4 `psychology` (0..100)

Sourced from `app.emotional_records` in the period. `mood_score ∈ [1, 5]`; rescale:

`psychology = round(avg((mood - 1) / 4 * 100))`. If no `emotional_records` exist, `psychology = 60` (moderate; same default as the reference system).

#### 13.4.5 `exercise` (0..100)

Sourced from `app.task_completions` with `task_code = 'ejercicio'` for the patient in the period.

`exercise = round(days_with_ejercicio / day_count * 100)`. A day counts at most once even with multiple completions (one scheduled `ejercicio` task per active weekday). If no completions exist, `exercise = 0`.

### 13.5 Transformation Score formula

For each `app.clinical_baselines` row for the patient, find the latest `app.clinical_measurement` in the current program week window for the same `metric_id`. Score per indicator:

| `|pct_change|` band | Direction | Score |
|--------------------|-----------|-------|
| `>= 15%` | favorable | `100` |
| `>= 10%` | favorable | `90` |
| `>= 5%` | favorable | `75` |
| `>= 1%` | favorable | `60` |
| `< 1%` (stable) | — | `50` |
| `>= 10%` | unfavorable | `10` |
| `>= 5%` | unfavorable | `25` |
| `>= 2%` | unfavorable | `35` |
| `< 2%` | unfavorable | `45` |

`score = round(avg(per_indicator_score) across indicators with baseline AND measurement)`. If no indicators exist, `score = 0` and `detail = {}`.

`detail` JSONB shape (one entry per indicator):

```json
{
  "weight":  {
    "baseline": 82.5, "current": 78.0, "unit": "kg",
    "delta": -4.5, "delta_pct": -5.45,
    "favorable": true, "score": 75
  },
  "bmi":     { "baseline": 28.4, "current": 26.1, "unit": "kg/m2",
    "delta": -2.3, "delta_pct": -8.10, "favorable": true, "score": 75 },
  "glucose": { "baseline": 110, "current": 95, "unit": "mg/dL",
    "delta": -15, "delta_pct": -13.64, "favorable": true, "score": 90 }
}
```

`overall_trend` is `up` if `score > score_previous`, `down` if `score < score_previous`, else `stable`. When `score_previous IS NULL`, `overall_trend = 'stable'`.

### 13.6 Domain rules

- **Patient-local time**: every comparison uses patient-local dates resolved via `program_enrollments.timezone` (IANA). No UTC math for the period.
- **No cron in MVP**: see §13.3. Manual `POST /program/scores/calculate` is the only out-of-band trigger.
- **Permission for manual recompute**: `Program.Edit` (existing, §6.14). Justification: a clinician is the only role with clinical authority to force a recompute; `Program.Edit` already gates template / weight changes that influence scoring. No new permission code is added.
- **Clinical safety**:
  - Scores are **adherence / evolution indicators**, not diagnosis. UI labels them as program indicators ("Índice de Salud", "Índice de Transformación").
  - Unfavorable indicators **never** reduce XP, **never** break a streak, **never** trigger punishment mechanics (existing rule, §0 decision 12 + §6.15).
  - Baselines require clinician `set_by` (AC-22). `target_value` is clinician-validated and never inferred.
- **PHI**: mood, barriers, and notes are excluded from audit payloads by the existing `emotional_records` exclusion rule (§8.5). Scores themselves are aggregate indicators and may appear in audit / activity_logs.
- **Permission matrix** (§6.14 plus this section):

| Endpoint | Permission | Caller scope |
|----------|------------|--------------|
| `GET /api/v1/program/scores` | `Program.View` | Patient-own (anti-IDOR → `404` on cross-patient); clinician scoped via `app.patient_professionals` (same as `Patients.ViewOwn`) |
| `POST /api/v1/program/scores/calculate` | `Program.Edit` | Clinician / admin only; body: `{ patientId, periodEndLocalDate? }`; `404` for non-owned patients |

### 13.7 API contract

#### 13.7.1 `GET /api/v1/program/scores`

Returns the current Health and Transformation Score for the caller's active enrollment. Computes on read and persists a fresh row if missing or stale (§13.3).

**Response 200**

```json
{
  "health_score": {
    "current": 81,
    "previous": 76,
    "trend": "up",
    "dimensions": {
      "adherence":  66,
      "clinical":   75,
      "nutrition":  80,
      "psychology": 90,
      "exercise":   100
    }
  },
  "transformation_score": {
    "current": 88,
    "previous": 80,
    "trend": "up",
    "week": 12,
    "detail": {
      "weight":  { "baseline": 82.5, "current": 78.0, "unit": "kg",
                   "delta": -4.5, "delta_pct": -5.45, "favorable": true, "score": 75 },
      "bmi":     { "baseline": 28.4, "current": 26.1, "unit": "kg/m2",
                   "delta": -2.3, "delta_pct": -8.10, "favorable": true, "score": 75 },
      "glucose": { "baseline": 110, "current": 95, "unit": "mg/dL",
                   "delta": -15, "delta_pct": -13.64, "favorable": true, "score": 90 }
    }
  }
}
```

**Errors**: `404 NO_ACTIVE_ENROLLMENT` if the caller has no `Active` enrollment; `403 FORBIDDEN` for cross-patient reads; `503` only if a required lookup (baselines / measurements) is unreachable — the endpoint then returns the last persisted `app.health_scores` / `app.transformation_scores` rows with `X-Score-Stale: true` and never `5xx` to the mobile.

#### 13.7.2 `POST /api/v1/program/scores/calculate`

Forces a recompute. Body: `{ "patientId": "<uuid>", "periodEndLocalDate": "2026-09-24" }`. `periodEndLocalDate` is optional (defaults to today in patient-local time).

**Response 200**: same shape as `GET /api/v1/program/scores`. `X-Score-Recalculated: true` header is set so callers can distinguish a fresh compute from a cache hit.

**Errors**: `403 FORBIDDEN` (no `Program.Edit`), `404 NOT_FOUND` (patient not in caller's scope), `422 INVALID_PERIOD` (bad date).

### 13.8 Implementation placement (mirrors §8.1)

| Concern | Project | Folder |
|---------|---------|--------|
| Entities, enums, value objects | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/HealthScoreWeight.cs`, `ClinicalBaseline.cs`, `HealthScore.cs`, `TransformationScore.cs`; enums in `Enums/ProgramProgress/` (`ScoreDimension`, `ScoreTrend`, `FavorableDirection`) |
| Calculator services | `src/CoppAddresd.Application` | `Services/ProgramProgress/{IHealthScoreCalculator,HealthScoreCalculator,ITransformationScoreCalculator,TransformationScoreCalculator}.cs` |
| Query / command handlers | `src/CoppAddresd.Application` | `Features/ProgramProgress/Queries/GetScores/`, `Features/ProgramProgress/Commands/CalculateScores/` |
| DTOs | `src/CoppAddresd.Application` | `Features/ProgramProgress/DTOs/Scores/{HealthScoreDto,TransformationScoreDto,IndicatorDetailDto,ScoresResponseDto}.cs` |
| EF configurations | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/HealthScoreWeightConfiguration.cs` (+ 3 siblings) |
| Repository extension | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` (extend with `GetOrComputeHealthScoreAsync`, `GetOrComputeTransformationScoreAsync`, `ListClinicalBaselinesAsync`, `UpsertClinicalBaselineAsync`) |
| Migration | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddProgramProgressScores.cs` (additive) |
| Seeder | `src/CoppAddresd.Infrastructure` | extend `SeedData/ProgramProgressSeeder.cs` with the 5 default `health_score_weights` rows (idempotent UPSERT) |
| Controllers | `src/CoppAddresd.Api` | extend `Controllers/ProgramController.cs` with two actions (`GetScores`, `CalculateScores`) |

### 13.9 Mobile integration (Phase 1.5 / 2 wire-up)

- New API client methods in `antares-paciente/src/api/program.ts`: `getScores(): Promise<ScoresResponseDto>`, `calculateScores(patientId: string)`.
- Wire `Evolución` tab in `antares-paciente/src/pages/ProgramPage.tsx` to `getScores()`. Keep the existing `HEALTH_PILLARS` / `TRANSFORM_ROWS` shapes intact and **fall back to the existing mock** (Health 86 / Transformation 87) when the API is unreachable (demo continuity, same contract as snapshot fallback §9.2).
- The mobile labels the dimensions explicitly: "Adherencia", "Evolución clínica", "Nutrición", "Bienestar psicológico", "Actividad física"; the indicators `weight / IMC / % grasa / cintura / glucosa / adherencia` render from `transformation_score.detail`.

### 13.10 Acceptance criteria

See §10.2 (AC-19..AC-22). The engine is not a redesign of the mobile UI; it is a data-source swap behind the existing Evolución tab.

---

## 14. XP Rules catalog (P1.5)

The XP rules catalog (`app.xp_rules`) makes XP awarding **data-driven** and adds anti-fraud limits without touching the gamification flow. It is a deliberate P1.5 addition (decision 20): the awarding path stays exactly as specified in §6 (task completion + day bonus, idempotent), but the *points and limits* can be tuned by an administrator at runtime instead of by migration.

### 14.1 New table `app.xp_rules`

All conventions match §3 (`gen_random_uuid()` id, `timestamptz` timestamps, audit via `AuditTriggerInterceptor` — the table is attached to the audit trigger in migration `AddXpRulesCatalog`, §8.5).

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `code` | `varchar(60)` | UNIQUE NOT NULL (alternate key + `uq_xp_rules_code`) | Business key of the UPSERT seeder and of `xp_ledger.rule_code` |
| `name` | `varchar(120)` | NOT NULL | |
| `category` | `varchar(40)` | NOT NULL | `adherence` (tasks + day bonus) / `streak` (streak milestones) |
| `base_xp` | `int` | NULL, CHECK (`>= 0`) | NULL = defer to the existing points source (for `TASK_*`: `weekly_day_templates.points`) |
| `multiplier` | `numeric(4,2)` | NOT NULL DEFAULT `1.00` CHECK (`> 0`) | Applied as `total = floor(base × multiplier)` |
| `max_per_day` | `int` | NULL, CHECK (`>= 0`) | Anti-fraud: max awards per patient-local day |
| `max_per_week` | `int` | NULL, CHECK (`>= 0`) | Anti-fraud: max awards per patient-local week |
| `requires_validation` | `boolean` | NOT NULL DEFAULT `false` | Reserved (no third-party validators in MVP) |
| `active` | `boolean` | NOT NULL DEFAULT `true` | Inactive rules fall back to the default behavior |
| `valid_from` | `date` | NOT NULL DEFAULT `CURRENT_DATE` | Start of validity |
| `valid_until` | `date` | NULL | NULL = open-ended |
| `created_at` | `timestamptz` | NOT NULL DEFAULT `now()` | |
| `updated_at` | `timestamptz` | NULL | |

Indexes: `uq_xp_rules_code` UNIQUE (`code`), `ix_xp_rules_category`, `ix_xp_rules_active`.

### 14.2 Seeded rules (11 + 4 clínicas + 4 nutrición + 5 racha nutribiótico, UPSERT by `code`)

`ProgramProgressSeeder` seeds idempotently (`ON CONFLICT (code) DO NOTHING` — an existing rule is never overwritten, so admin edits survive re-runs). The 4 clinical rules (`CLINICAL_IMPROVE`, `CLINICAL_SIGNIFICANT`, `CLINICAL_STABLE`, `CLINICAL_WEEKLY_ALL_UP`) live in **SPEC §15.2** — they power the clinical XP engine, which fires only on `POST /scores/calculate`. The 4 nutrition rules (`NUTRITION_MEAL_COMPLETE`, `NUTRITION_HYDRATION`, `NUTRITION_WEEK_85`, `NUTRITION_RECOVERY`) live in **SPEC §18.1** — granular nutrition XP ("Paso 6"), also additive to the existing `nut` task. The 5 nutribiotic-streak rules (`NB_STREAK_7`, `NB_STREAK_14`, `NB_STREAK_30`, `NB_STREAK_60`, `NB_STREAK_90`) live in **SPEC §19.2** — "Paso 7a", additive to `TASK_NUTRIBIOTICO`.

| Code | Category | BaseXp | MaxPerDay | MaxPerWeek |
|------|----------|--------|-----------|------------|
| `TASK_PODCAST` | adherence | NULL | 1 | 7 |
| `TASK_VITALS` | adherence | NULL | 1 | 7 |
| `TASK_NUT` | adherence | NULL | 1 | 7 |
| `TASK_EJERCICIO` | adherence | NULL | 1 | 7 |
| `TASK_NUTRIBIOTICO` | adherence | NULL | 1 | 7 |
| `TASK_EMOCIONAL` | adherence | NULL | 1 | 7 |
| `DAY_BONUS` | adherence | 50 | 1 | 7 |
| `STREAK_7` | streak | 100 | 1 | 1 |
| `STREAK_11` | streak | 200 | 1 | 1 |
| `STREAK_22` | streak | 500 | 1 | 1 |
| `STREAK_50` | streak | 1500 | 1 | 1 |
| `NB_STREAK_7` | nutriobiotic | 50 | 1 | 1 |
| `NB_STREAK_14` | nutriobiotic | 100 | 1 | 1 |
| `NB_STREAK_30` | nutriobiotic | 250 | 1 | 1 |
| `NB_STREAK_60` | nutriobiotic | 500 | 1 | 1 |
| `NB_STREAK_90` | nutriobiotic | 1000 | 1 | 1 |

**Consistency note**: the `STREAK_*` rows are seeded as data-driven configuration with their limits; since "Paso 4" (SPEC §16) they have a **live call site**: the milestone engine resolves them generically by code via the same awarding path (§14.3) when `current_streak` reaches the milestone day, and each is awarded **once per enrollment** (idempotent via the `streak_milestone` dedupe). Before §16 they were forward-looking only (no milestone XP was awarded; only `nextMilestoneDays` was derived, §7.1). The 4 nutrition rules are **aditivas**: los logs de `POST /nutrition/log` y los premios semanales de `POST /scores/calculate` SUMA a la XP de la tarea `nut` (SPEC §18, decisión 24; el doble premio se tunea vía el catálogo). The 5 `NB_STREAK_*` rules (SPEC §19, "Paso 7a") are **also aditivas** a la tarea `nutribiotico`: premian los hitos de la racha propia de la tarea (7/14/30/60/90 días) y se otorgan en el camino de completación con el dedupe `('nb_milestone', task_completions.id, reason)` — **cada corrida re-otorga su hito** (AC-39), con topes 1/día y 1/semana.

### 14.3 Award-path resolution (precedence + anti-fraud)

The resolution lives in `ProgramRepository.CompleteTaskCoreAsync` (the single transaction that awards XP, §6.4/§6.5), **before** writing each `xp_ledger` row, inside the `FOR UPDATE` lock on the enrollment:

1. **Resolve** the rule by code (`TASK_<TaskCode>` for completions, `DAY_BONUS` for the perfect-day bonus).
2. **Precedence**: a rule `active = true` AND `valid_from <= today(UTC) <= valid_until` (or `valid_until IS NULL`) **wins**:
   - `points = rule.base_xp ?? <existing source>` — for `TASK_*` (base_xp NULL) the existing source is the snapshot's `weekly_day_templates.points`; for `DAY_BONUS` it is the rule's 50.
   - `total = floor(points × multiplier)`.
   - **Anti-fraud limits**: count existing `xp_ledger` rows for `(enrollment_id, rule_code)` in the patient-local day/week window (local date → UTC range, DST-aware); if `count >= max_per_day` → `409 XP_DAILY_LIMIT_REACHED`; if `count >= max_per_week` → `409 XP_WEEKLY_LIMIT_REACHED` (both mapped by `ExceptionHandlingMiddleware` via `BusinessRuleViolationException`).
3. **Fallback**: no rule, or rule inactive/expired → current behavior unchanged: points from the snapshot, no multiplier, no limits, and `xp_ledger.rule_code = NULL`.
4. **Provenance**: every awarded row writes `xp_ledger.rule_code = <code>` when a rule produced it.

The counters only see rows with a `rule_code` (pre-catalog rows are NULL and never count), and the checks run inside the same transaction as the award, so concurrent completions cannot bypass them.

### 14.4 Prospective-only edits

`PUT /api/v1/program/xp-rules/{code}` (permission `Program.Edit`) edits: `base_xp`, `multiplier`, `max_per_day`, `max_per_week`, `requires_validation`, `active`, `valid_until` (nullable). The identity (`code`, `name`, `category`, `valid_from`) is immutable through this endpoint.

- **Prospective only**: edits never rewrite `xp_ledger` history — `rule_code` is provenance of the award, not a link to be updated. Only future awards are affected.
- **Validation** (FluentValidation + handler): `multiplier > 0`, `base_xp >= 0`, `max_per_day >= 0`, `max_per_week >= 0`, and `valid_until >= valid_from` (422 `INVALID_VALIDITY_WINDOW` otherwise). DB CHECK constraints mirror these invariants.
- Seeder semantics: a seeded rule is only inserted if missing; re-running the seeder after an admin edit keeps the edited values.

### 14.5 Admin API contract

Base `/api/v1/program`, permission `Program.Edit` (existing code, §6.14; no new permission is added).

#### 14.5.1 `GET /api/v1/program/xp-rules`

List the full catalog ordered by `code` (small, stable catalog — no pagination).

**Response 200**

```json
[
  {
    "id": "3fa8…",
    "code": "TASK_PODCAST",
    "name": "Tarea podcast",
    "category": "adherence",
    "baseXp": null,
    "multiplier": 1.0,
    "maxPerDay": 1,
    "maxPerWeek": 7,
    "requiresValidation": false,
    "active": true,
    "validFrom": "2026-08-27",
    "validUntil": null,
    "createdAt": "2026-08-27T04:12:39Z",
    "updatedAt": null
  }
]
```

**Errors**: `403 FORBIDDEN` (no `Program.Edit`), `401` (no token).

#### 14.5.2 `PUT /api/v1/program/xp-rules/{code}`

Body (all fields required, `validUntil` nullable):

```json
{
  "baseXp": 120,
  "multiplier": 1.0,
  "maxPerDay": 1,
  "maxPerWeek": 7,
  "requiresValidation": false,
  "active": true,
  "validUntil": null
}
```

**Response 200**: the updated rule (same shape as the GET item).

**Errors**: `400` validation, `403 FORBIDDEN`, `404 NOT_FOUND` (unknown code), `409` (DB conflict), `422 INVALID_VALIDITY_WINDOW`.

### 14.6 Implementation placement (mirrors §8.1/§13.8)

| Concern | Project | Folder |
|---------|---------|--------|
| Entity | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/XpRule.cs`; rule codes in `Enums/ProgramProgress/XpRuleCodes.cs` |
| EF configuration | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/XpRuleConfiguration.cs`; `xp_ledger.rule_code` in `XpLedgerEntryConfiguration.cs` |
| Migration | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddXpRulesCatalog.cs` (additive; also attaches the `xp_rules` audit trigger) |
| Award-path resolution | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` (`ResolveActiveRuleAsync` / `ResolveXpAwardAsync` used by `CompleteTaskCoreAsync`) |
| Repository contract | `src/CoppAddresd.Application` | `Interfaces/IProgramRepository.cs` (`ListXpRulesAsync`, `GetXpRuleByCodeAsync`, `UpdateXpRuleAsync`) |
| Query / command | `src/CoppAddresd.Application` | `Features/ProgramProgress/Queries/ListXpRules/`, `Features/ProgramProgress/Commands/UpdateXpRule/` |
| DTO | `src/CoppAddresd.Application` | `DTOs/ProgramProgress/ProgramProgressDtos.cs` (`XpRuleDto`) |
| Controller | `src/CoppAddresd.Api` | `Controllers/ProgramController.cs` (`GET/PUT /api/v1/program/xp-rules`) |
| Seeder | `src/CoppAddresd.Api` | `Seeders/ProgramProgressSeeder.cs` (`SeedXpRulesAsync`) |

### 14.7 Acceptance criteria

See §10.2 (AC-23, AC-24). Tests are **optional** in this phase and are run manually by the user per the current workflow (no automated test run).

---

## 15. Clinical XP (P1.5 — "Paso 3")

Clinical XP translates **real clinical evolution** (baseline vs latest measurement, the same data the Transformation Score uses) into gamification XP — but through **clinician-validated, never punitive** rules. It extends the XP rules catalog (§14) with a `clinical` category and adds a professional validation queue for significant improvements. **It fires ONLY on `POST /api/v1/program/scores/calculate`** (the manual clinician recompute); `GET /api/v1/program/scores` computes scores and returns the pending-review count but **never awards XP**.

### 15.1 New table `app.clinical_xp_reviews`

All conventions match §3/§14 (audit via `AuditTriggerInterceptor`, see §8.5). One row per `(patient_id, health_score_id, metric_id)` = one significant improvement detected in one score period.

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `patient_id` | `uuid` | NOT NULL, FK `app.patient_profiles(id)` ON DELETE RESTRICT | |
| `health_score_id` | `uuid` | NOT NULL, FK `app.health_scores(id)` ON DELETE RESTRICT | The `source_ref_id` of the award dedupe (`clinical_period`) |
| `metric_id` | `uuid` | NOT NULL, FK `app.measurement_metrics(id)` ON DELETE RESTRICT | |
| `rule_code` | `varchar(60)` | NOT NULL DEFAULT `'CLINICAL_SIGNIFICANT'` | Rule awarded if the review is approved |
| `delta_pct` | `numeric(8,3)` | NULL | Observed |Δ%| of the metric in the period |
| `status` | `varchar(20)` | NOT NULL DEFAULT `'pending'`, CHECK IN (`pending`,`approved`,`rejected`) | State machine below |
| `decided_by` | `uuid` | NULL, FK `auth.users(id)` (raw SQL, ON DELETE SET NULL) | Clinician who decided |
| `decided_at` | `timestamptz` | NULL | |
| `created_at` / `updated_at` | `timestamptz` | NOT NULL DEFAULT `now()` / NULL | |

Indexes: `uq_clinical_xp_reviews_patient_score_metric` UNIQUE (`patient_id`, `health_score_id`, `metric_id`), `ix_clinical_xp_reviews_status`, `ix_clinical_xp_reviews_decided_by`.

`app.xp_ledger` gains two nullable columns:

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `validated_by` | `uuid` | NULL, FK `auth.users(id)` (raw SQL, ON DELETE SET NULL) | Clinician who validated the award |
| `validated_at` | `timestamptz` | NULL | Set together with `validated_by` |

A ledger row whose rule has `requires_validation = true` and `validated_by IS NULL` is **pending validation** and is excluded from the XP totals (see §15.5).

### 15.2 Seeded rules (4 new, category `clinical`)

`ProgramProgressSeeder` seeds these idempotently by `code` (same `ON CONFLICT DO NOTHING` semantics as §14.2):

| Code | Category | BaseXp | RequiresValidation | MaxPerDay | MaxPerWeek |
|------|----------|--------|--------------------|-----------|------------|
| `CLINICAL_IMPROVE` | clinical | 50 | false | NULL | NULL |
| `CLINICAL_SIGNIFICANT` | clinical | 100 | **true** | NULL | NULL |
| `CLINICAL_STABLE` | clinical | 20 | false | NULL | NULL |
| `CLINICAL_WEEKLY_ALL_UP` | clinical | 150 | false | NULL | NULL |

The `clinical` category rules have no `max_per_day`/`max_per_week`: the idempotency dedupe of `xp_ledger` (§15.3) already limits each rule to one award per clinical period.

### 15.3 Award classification (per metric, fires ONLY on `/scores/calculate`)

In the `CalculateScoresCommand` handler, after computing + persisting the new `app.health_scores` row for the period, the repository evaluates **each baseline metric** (`app.clinical_baselines`) against its latest `app.clinical_measurements` in the same period window the Transformation calculator uses:

| Classification | Condition (using `favorable_direction` + |Δ%|) | Action |
|----------------|-----------------------------------------------|--------|
| **Significant** | favorable AND `\|Δ%\| >= threshold` (default 5%, config `Program:ClinicalXp:SignificantThresholdPct`) | Creates/reuses a `clinical_xp_reviews` row (`status = 'pending'`). **No XP yet** — a clinician decides (§15.4). |
| **Improve** | favorable AND `1% <= \|Δ%\| < threshold` | Auto-awards `CLINICAL_IMPROVE` (50 XP): `source_ref_type = 'clinical_period'`, `source_ref_id = health_scores.id`, `reason = 'CLINICAL_IMPROVE'`, `validated_by = NULL`. |
| **Stable** | `\|Δ%\| < 1%` | Auto-awards `CLINICAL_STABLE` (20 XP), same idempotent pattern. |
| **Unfavorable** | change against `favorable_direction` | **0 XP. Never penalizes, never breaks streaks** (decision 12 + §6.15, AC-27). |
| **All-up** | EVERY baseline metric with a measurement in the period is favorable (at least one exists) | Auto-awards `CLINICAL_WEEKLY_ALL_UP` (150 XP) once per period. |

**Idempotency**: the existing partial unique `uq_xp_ledger_source_dedupe (source_ref_type, source_ref_id, reason)` on `xp_ledger` with `source_ref_type = 'clinical_period'` and `source_ref_id = health_scores.id` guarantees **one award per rule per period**. On unique-violation (concurrent recompute) the award is skipped — never a double XP. `CLINICAL_IMPROVE`/`CLINICAL_STABLE`/`CLINICAL_WEEKLY_ALL_UP` have `validated_by = NULL` but their rules have `requires_validation = false` → they **do count** in the totals.

### 15.4 Review / validation flow (professional)

State machine: `pending → approved | rejected` (terminal). Only a **clinician** can decide (same AC-22 guard as baselines: `Physician`, `Nutritionist`, `Psychologist`, `ClinicalDirector` or `Admin`; a patient deciding their own significant XP → `403 FORBIDDEN`).

- **Approve** (`{ "approve": true }`): row → `approved` with `decided_by`/`decided_at`; awards `CLINICAL_SIGNIFICANT` (100 XP) with `validated_by = <clinician>` and `validated_at = now` using the same `clinical_period` dedupe (if the period already has the award, the second approval is marked but no XP is doubled).
- **Reject** (`{ "approve": false }`): row → `rejected` with `decided_by`/`decided_at`; **no XP**.
- Already decided → `409 REVIEW_ALREADY_DECIDED`.

### 15.5 XP totals exclude pending validation

All XP total/summary queries (`pointsTotal`, `pointsToday`, the ledger sums behind `GET /scores` snapshot / enrollment balance / XP level) **exclude `xp_ledger` rows whose rule has `requires_validation = true` AND `validated_by IS NULL`** — a significant award does not count until a clinician approves it. Implemented in the repository sum path (join `xp_rules` via the `Rule` navigation, or a denormalized check): `validated_by != null OR rule == null OR NOT rule.requires_validation`. The `balance_after` chain is unaffected (each row still carries the cumulative balance; only the displayed totals filter pending rows).

### 15.6 API contract

Base `/api/v1/program`, permission `Program.Adapt` (existing, §6.14 — same decision code used for adaptations; no new permission code).

#### 15.6.1 `GET /api/v1/program/xp-rules/clinical-pending`

Paged queue of pending reviews (`?page=1&pageSize=20`, max 100), oldest first.

**Response 200**

```json
{
  "data": [
    {
      "id": "3fa8…",
      "patientId": "9c1e…",
      "metricId": "…",
      "metricCode": "weight",
      "metricName": "Peso",
      "deltaPct": -5.45,
      "ruleCode": "CLINICAL_SIGNIFICANT",
      "status": "pending",
      "healthScorePeriodStart": "2026-09-21",
      "healthScorePeriodEnd": "2026-09-27",
      "createdAt": "2026-09-27T04:12:39Z",
      "decidedBy": null,
      "decidedAt": null
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

**Errors**: `403 FORBIDDEN` (no `Program.Adapt`), `401` (no token).

#### 15.6.2 `POST /api/v1/program/xp-rules/clinical-pending/{id}/decide`

Body: `{ "approve": true | false }`.

**Response 200**: the updated review (same shape as the queue item, now with `status`/`decidedBy`/`decidedAt`).

**Errors**: `400` validation, `403 FORBIDDEN` (no `Program.Adapt` or non-clinician actor), `404 NOT_FOUND` (unknown review), `409 REVIEW_ALREADY_DECIDED`.

### 15.7 Acceptance criteria

See §10.2 (AC-25, AC-26, AC-27). Tests are **optional** in this phase and are run manually by the user per the current workflow.

### 15.8 Implementation placement (mirrors §8.1/§13.8/§14.6)

| Concern | Project | Folder |
|---------|---------|--------|
| Entity + enums | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/ClinicalXpReview.cs`, `XpLedgerEntry.cs` (extend); `Enums/ProgramProgress/ClinicalXpReviewStatus.cs`, `XpRuleCodes.cs`, `XpReason.cs` (extend) |
| EF configurations | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/ClinicalXpReviewConfiguration.cs` (new), `XpLedgerEntryConfiguration.cs` (extend) |
| Migration | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddProgramProgressClinicalXp.cs` (additive; raw-SQL FKs a `auth."Users"` + trigger de auditoría + GRANTs) |
| Award engine | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` (`EvaluateClinicalXpAwardsAsync`, `AwardClinicalXpAsync`, `ListPendingClinicalReviewsAsync`, `DecideClinicalXpReviewAsync`) |
| Repository contract + DTOs | `src/CoppAddresd.Application` | `Interfaces/IProgramRepository.cs` (extend); `Features/ProgramProgress/DTOs/ClinicalXp/` |
| Query / command | `src/CoppAddresd.Application` | `Features/ProgramProgress/Queries/ListClinicalReviews/`, `Commands/DecideClinicalReview/`, `Commands/CalculateScores/` (extend) |
| Controller | `src/CoppAddresd.Api` | `Controllers/ProgramController.cs` (dos acciones bajo `/xp-rules/clinical-pending`) |
| Seeder | `src/CoppAddresd.Api` | `Seeders/ProgramProgressSeeder.cs` (4 reglas clínicas en `SeedXpRulesAsync`) |

---

## 16. Streak multiplier x2 (P1.5 — "Paso 4")

Los hitos de racha (SPEC §6.6) ahora otorgan su XP del catálogo **una única vez por inscripción** y, en los hitos 11/22/50, **activan un multiplicador x2 del paciente** que aplica a TODA la XP otorgada mientras está vigente (sin excepciones, ver §16.3). Es la referencia ADRED del módulo: el día 7 premia la constancia; los días 11/22/50 aceleran el avance del paciente durante 24h/48h/72h.

### 16.1 Tabla de hitos

| Día de racha (`current_streak` alcanza) | Regla (`app.xp_rules`) | XP base | Multiplicador |
|---|---|---|---|
| 7 | `STREAK_7` | 100 | ninguno (0h) |
| 11 | `STREAK_11` | 200 | **x2 por 24h** |
| 22 | `STREAK_22` | 500 | **x2 por 48h** |
| 50 | `STREAK_50` | 1500 | **x2 por 72h** |

Las reglas ya están sembradas en §14.2 (categoría `streak`, topes 1/1); el motor las resuelve por código con el mismo camino de otorgamiento (§14.3) — no hace falta nueva semilla.

### 16.2 Motor de hitos (otorgamiento + activación)

En el camino de racha (dentro de la transacción de `CompleteTask`, inscripción bloqueada `FOR UPDATE`), cuando `current_streak` **crece hoy** hasta un día hito:

1. **Otorgar una vez**: la XP del hito se otorga con la resolución del catálogo — `rule_code = STREAK_{days}`, `source_ref_type = 'streak_milestone'`, `source_ref_id = streak_states.enrollment_id` (la PK de `streak_states` es `enrollment_id`), `reason = 'STREAK_{days}'` (el miembro `XpReason.STREAK_{days}` se persiste con el nombre del miembro, igual que las `CLINICAL_*`). El dedupe parcial `uq_xp_ledger_source_dedupe (source_ref_type, source_ref_id, reason)` la hace idempotente: si la racha se rompió y se reconstruyó hasta el mismo hito, **no** se vuelve a otorgar.
2. **Activar multiplicador** (solo 11/22/50): `streak_states.multiplier_active = 2.0`, `multiplier_ends_at = now + horas`. Si ya había un multiplicador vigente, el nuevo lo **SOBRESCRIBE** (se extiende desde ahora, no se suma). La activación corre **antes** del otorgamiento del hito, por lo que la XP del hito se otorga con el x2 recién activado (§16.3 — el multiplicador aplica a todo).
3. **Guardia compartida**: el mismo chequeo "XP del hito ya otorgada" protege la activación — un hito repetido no re-otorga ni re-activa. El día 7 otorga `STREAK_7` pero **no** activa multiplicador.

### 16.3 Multiplicador del paciente en el camino de otorgamiento

El camino de otorgamiento (`ResolveXpAwardAsync` para tareas/bonus/hitos + `AwardClinicalXpAsync` para la XP clínica) aplica el multiplicador activo del paciente:

1. Si `multiplier_ends_at` es null o ya venció (reloj del servidor) → se trata como **1.0** y se resetea lazy (`ExecuteUpdate` a `multiplier_active = 1.0` / `multiplier_ends_at = null`) en la misma transacción del otorgamiento (C.1).
2. `total = floor(base_xp × rule.Multiplier × patient_multiplier)` (C.2).
3. `xp_ledger.multiplier_used = rule.Multiplier × patient_multiplier` registra el multiplicador **efectivo** de la transacción (prospective: las filas previas quedan NULL).
4. Aplica a: completaciones de tarea, bonus de día perfecto, XP de hito de racha y XP clínica (auto + significativa aprobada). **Excepción: ninguna** — toda la XP se multiplica mientras está vigente. En el fallback sin regla del catálogo también aplica (el multiplicador del paciente es independiente del catálogo).

### 16.4 Exposición en el snapshot

`GET /api/v1/program/me/snapshot` (bloque `streak`) expone:

| Campo | Tipo | Semántica |
|---|---|---|
| `multiplierActive` | `decimal` | `1.0` (sin multiplicador) o `2.0` (vigente) |
| `multiplierEndsAt` | `timestamptz?` | `null` sin multiplicador; instante de expiración (UTC) si está vigente |
| `multiplierRemainingHours` | `int` | horas restantes, **redondeadas hacia abajo**; `0` sin multiplicador |

La lectura **nunca escribe**: un multiplicador vencido se muestra como `1.0`/`null`/`0` en el snapshot; el reset lazy del estado ocurre en el próximo otorgamiento (C.1).

### 16.5 Acceptance criteria

Ver §10.2 (AC-28, AC-29, AC-30). Los tests son **opcionales** en esta fase y los corre el usuario manualmente per el workflow actual (gate = build verde + migración generada sin aplicar).

### 16.6 Implementation placement (espejo de §14.6/§15.8)

| Concern | Project | Folder |
|---------|---------|--------|
| Entity + enums | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/StreakState.cs`, `XpLedgerEntry.cs` (extender); `Enums/ProgramProgress/XpReason.cs` (4 miembros `STREAK_*`) |
| EF configurations | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/StreakStateConfiguration.cs`, `XpLedgerEntryConfiguration.cs` (extender) |
| Migration | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddProgramProgressMultiplier.cs` (aditiva: 2 columnas en `streak_states` + 1 en `xp_ledger` + CHECK `multiplier_active >= 1.0`) |
| Motor de hitos + multiplicador | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` (`AwardStreakMilestoneIfReachedAsync`, `ResolvePatientMultiplierAsync`, `ResolveXpAwardAsync`/`AwardClinicalXpAsync` extendidos, `UpdateStreakAsync` devuelve racha/crecimiento) |
| Snapshot DTO | `src/CoppAddresd.Application` | `DTOs/ProgramProgress/ProgramProgressDtos.cs` (`StreakInfoDto` extendido con los 3 campos) |
| Seeder | `src/CoppAddresd.Api` | `Seeders/ProgramProgressSeeder.cs` (sin cambios: las 4 reglas `STREAK_*` ya existen en §14.2; no se agrega `STREAK_90`) |

---

## 17. Configurable streak threshold & essential tasks (P1.5 — "Paso 5")

La racha deja de ser binaria (perfecta o rota) y pasa a un **umbral configurable por plantilla**: un día mantiene la racha si completó al menos `streak_min_tasks` tareas, y el **rescate con congelamiento** exige al menos una **tarea esencial** completada el día perdido (regla inspirada en ADRED, adaptada a la economía de congelamientos del módulo). Es configuración de plantilla (data-driven): hasta que existan las pantallas de edición ERP (B7), el seeder es la vía de configuración.

### 17.1 Configuración en `app.program_templates`

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `streak_min_tasks` | `smallint` | NOT NULL DEFAULT `1`, CHECK (`>= 1`) | Mínimo de tareas completadas por día (fecha local) para mantener la racha |
| `essential_task_codes` | `jsonb` | NOT NULL DEFAULT `'[]'` | Códigos de tarea que cuentan como esenciales para el rescate con congelamiento |

El seeder `default-83w` fija `streak_min_tasks = 1` y `essential_task_codes = ["nut","ejercicio","nutribiotico"]` **solo en create** (UPSERT idempotente: un template existente nunca se pisa — misma regla que el resto del seeder). Una lista esencial vacía (template previo a §17) significa **sin restricción**: el rescate con congelamiento vuelve al comportamiento previo (no es un bug, es la compatibilidad hacia atrás del default).

### 17.2 Mantenimiento de racha por umbral (B)

En el camino de racha (`ProgramRepository.CompleteTaskCoreAsync` → `UpdateStreakAsync`), el umbral se resuelve en lectura desde la plantilla de la inscripción (`program_templates` vía `program_enrollments.template_id`, misma resolución de plantilla existente):

1. Un día **cumple el umbral** si `tasks_done >= streak_min_tasks`, donde `tasks_done` = cantidad de `task_completions` de la fecha local (incluye la completación en curso). Con el default 1, cualquier día con ≥1 tarea mantiene la racha (comportamiento previo).
2. Un día que cumple el umbral mantiene/incrementa la racha exactamente como antes (consecutivo → `current_streak + 1`; hueco → ver §17.3; mismo día → no-op).
3. El día perfecto ("todas las tareas programadas") **no cambia**: se sigue usando para el bonus de día (SPEC §6.3/§6.5) y para la concesión de congelamientos (1 por 7 días perfectos consecutivos, tope 3 — cadencia intacta).

### 17.3 El rescate requiere una tarea esencial (C)

Cuando un día **no cumple el umbral** y la evaluación de racha llegaría a consumir un congelamiento para preservarla:

- El congelamiento se consume **solo si** el día perdido completó al menos una tarea cuyo código está en `essential_task_codes` de la plantilla (AC-31). Se registra con el camino `Consumed` existente (`streak_freezes(kind='Consumed', used_on_local_date = día perdido)`).
- Si el paciente no completó ninguna tarea esencial el día perdido → la racha se rompe y el congelamiento **NO se consume** (permanece en inventario, AC-32). Sin congelamiento → racha se rompe (sin cambios).
- `essential_task_codes = []` → sin restricción (rescate permitido, compatibilidad previa).

### 17.4 Exposición en el snapshot (D)

`GET /api/v1/program/me/snapshot` (bloque `template`, aditivo — los campos existentes no cambian):

| Campo | Tipo | Semántica |
|---|---|---|
| `streakMinTasks` | `int` | Umbral de la plantilla (para renderizar "necesitas X tareas" si se desea) |
| `essentialTaskCodes` | `string[]` | Códigos esenciales de la plantilla (qué tareas rescatan con congelamiento) |

### 17.5 Acceptance criteria

Ver §10.2 (AC-09 actualizado, AC-31, AC-32). Los tests son **opcionales** en esta fase y los corre el usuario manualmente per el workflow actual (gate = build verde + migración generada sin aplicar).

### 17.6 Implementation placement (espejo de §16.6)

| Concern | Project | Folder |
|---------|---------|--------|
| Entity | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/ProgramTemplate.cs` (`StreakMinTasks`, `EssentialTaskCodes`) |
| EF configuration | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/ProgramTemplateConfiguration.cs` (columnas + CHECK `streak_min_tasks >= 1` + jsonb con converter) |
| Migration | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddProgramProgressStreakConfig.cs` (aditiva y reversible: 2 columnas + CHECK en `program_templates`) |
| Camino de racha | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` (`meetsThreshold` en `CompleteTaskCoreAsync`, `UpdateStreakAsync` con `essentialTaskCodes`, helper `HadEssentialTaskAsync`) |
| Snapshot DTO | `src/CoppAddresd.Application` | `DTOs/ProgramProgress/ProgramProgressDtos.cs` (`ProgramSnapshotTemplateDto` + 2 campos) |
| Seeder | `src/CoppAddresd.Api` | `Seeders/ProgramProgressSeeder.cs` (config del template `default-83w` solo en create) |

---

## 18. Granular nutrition XP (P1.5 — "Paso 6")

La nutrición deja de ser solo la tarea `nut` del programa (una completación diaria con puntos de plantilla) y gana **otorgamientos granulares por comida e hidratación** (XP adicional, no sustitutiva) más **premios semanales de adherencia**. Cierra el bucle de la `NutritionPage` del móvil (4 comidas `des`/`alm`/`mer`/`cen` + hidratación `agua`). **Tests OPTIONALES / manuales per el workflow actual** (sin `dotnet test` automático; gate = build verde + migración generada sin aplicar).

### 18.1 Reglas sembradas (4 nuevas en `app.xp_rules`, categoría `nutrition`)

`ProgramProgressSeeder` las siembra idempotentemente por `code` (mismo `ON CONFLICT (code) DO NOTHING` que el resto del catálogo):

| Code | Categoría | BaseXp | MaxPerDay | MaxPerWeek | RequiresValidation |
|------|-----------|--------|-----------|------------|--------------------|
| `NUTRITION_MEAL_COMPLETE` | nutrition | 10 | 4 | 28 | false |
| `NUTRITION_HYDRATION` | nutrition | 5 | 1 | 7 | false |
| `NUTRITION_WEEK_85` | nutrition | 75 | 1 | 1 | false |
| `NUTRITION_RECOVERY` | nutrition | 50 | 1 | 1 | false |

**No se siembra `NUTRITION_PHOTO`** (diferido, §18.7): el móvil mock registra con foto (IA analiza gramos/kcal/adherencia), pero el backend de este paso registra el log directo del paciente sin análisis de foto.

### 18.2 Habit templates (2 tablas nuevas + seeder)

- `app.habit_templates` — catálogo sembrado por `code` (`des`/`alm`/`mer`/`cen` categoría `alimentacion`, `agua` categoría `agua`); el `code` es el `mealCode` del contrato de la API.
- `app.habit_checks` — registro por `(patient_id, habit_template_id, local_date)`, único por tripleta (`uq_habit_checks_patient_template_date`).

Es la **MISMA fuente** de la dimensión `nutrition` del Índice de Salud (SPEC §13.4.3: `app.habit_checks` categoría `alimentacion`): el log granular alimenta la adherencia del Health Score sin duplicar lógica (el repositorio reutiliza la query existente).

### 18.3 Endpoint `POST /api/v1/program/nutrition/log`

| Aspecto | Contrato |
|---------|----------|
| Permiso | `Program.View`; paciente autenticado. El `patientId` se resuelve del JWT (nunca del body) — anti-IDOR AC-11: sin perfil de paciente → `404`. |
| Body | `{ mealCode: 'des'\|'alm'\|'mer'\|'cen'\|'agua', localDate? }` |
| `localDate` | Opcional; por defecto el hoy local del paciente (autoridad del repositorio). Futura → `422 INVALID_DATE`. |
| Upsert | Crea el `habit_check` único por `(paciente, plantilla, fecha)`. |
| Duplicado | `409 HABIT_ALREADY_LOGGED` — la comida/hidratación de esa fecha ya está registrada (semántica "ya registrado" del módulo, precedente `REVIEW_ALREADY_DECIDED`). La XP nunca se duplica: el único de `habit_checks` + el dedupe parcial `('habit_log', habit_check.id, reason)` son el backstop de carrera. |
| XP | `NUTRITION_MEAL_COMPLETE` (10) por comida / `NUTRITION_HYDRATION` (5) por `agua` — por el camino de resolución del catálogo (§14.3 + §16: multiplicador del paciente y topes `max_per_day`/`max_per_week` 4/día y 1/día; sin regla vigente → fallback a los puntos base documentados, sin límites). |
| Respuesta 200 | `{ habitCheckId, mealCode, localDate, isDone, xpAwarded, xpBalanceAfter }` |
| `source_ref_type` | `'habit_log'` con `source_ref_id = habit_check.id` y `reason = 'NUTRITION_MEAL_COMPLETE'/'NUTRITION_HYDRATION'` (dedupe parcial del libro mayor). |

**Aditivo, no sustitutivo**: la tarea `nut` del programa sigue otorgando sus puntos de plantilla al completarse (`CompleteTaskAsync`, sin cambios) y **NO** se auto-completa desde este endpoint. El log granular es XP adicional por registro. El doble premio (tarea 150 + comidas hasta 40/día) está documentado y se tunea vía `xp_rules` (riesgo en PLAN §7).

### 18.4 Otorgamientos semanales (fires on `POST /scores/calculate`)

Evaluados SOLO en `POST /api/v1/program/scores/calculate` (nunca en `GET /scores`), después de persistir la fila de `health_scores` del período (su id es el `source_ref_id` del dedupe):

1. **Adherencia del período** = MISMA fuente que la dimensión `nutrition` del Health Score (SPEC §13.4.3): `app.habit_checks` categoría `alimentacion`, `achieved/total`. Reutiliza la lógica existente del repositorio (`GetNutritionLogAsync`) — no la duplica. Sin logs en el período → adherencia 0 (sin premio, nunca penaliza).
2. **Adherencia ≥ 85%** → `NUTRITION_WEEK_85` (+75) una vez por período (AC-35).
3. **Adherencia ≥ +20 puntos vs el período anterior** (la fila previa de `health_scores`; se compara contra su dimensión de nutrición) → `NUTRITION_RECOVERY` (+50) una vez por período (AC-36). Sin período anterior no hay recuperación que medir.

Idempotencia: dedupe parcial `(source_ref_type='nutrition_period', source_ref_id=health_scores.id, reason)` — un otorgamiento por regla y período; ante carrera (violación única) se omite, nunca doble XP. Respeta el multiplicador del paciente (SPEC §16, C.4) como todo otorgamiento (el helper `AwardPeriodXpAsync` es compartido con la XP clínica).

### 18.5 Decisión de diseño: aditivo vs sustitutivo

| Opción | Decisión |
|--------|----------|
| **Aditivo (elegido)** | La tarea `nut` (150 de plantilla) y los logs granulares (hasta 10×4 = 40/día) coexisten: el módulo premia la acción completa (tarea) Y el detalle (cada comida registrada). El doble premio es visible (rule_code en `xp_ledger`) y tuneable vía el catálogo. |
| Sustitutivo (descartado) | Habría desactivado la XP de la tarea o del log; rompe el contrato de puntos del snapshot (decisiones 5/7) y la experiencia del móvil (la tarea `nut` y la NutritionPage son pantallas distintas). |

### 18.6 Implementation placement (espejo de §15.8/§16.6)

| Concern | Project | Folder |
|---------|---------|--------|
| Entidades + enums | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/HabitTemplate.cs`, `HabitCheck.cs`; `Enums/ProgramProgress/MealCode.cs`, `XpReason.cs`/`XpRuleCodes.cs` (4 miembros `NUTRITION_*`) |
| EF configurations | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/HabitTemplateConfiguration.cs`, `HabitCheckConfiguration.cs` |
| Migración | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddProgramProgressNutritionXp.cs` (aditiva y reversible: 2 tablas + índices + auditoría + GRANTs) |
| Repositorio | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` (`LogNutritionAsync`, `EvaluateNutritionAwardsAsync`, helper compartido `AwardPeriodXpAsync`) |
| Comando + DTOs | `src/CoppAddresd.Application` | `Features/ProgramProgress/Commands/LogNutrition/`, `Features/ProgramProgress/DTOs/Nutrition/` |
| Controller | `src/CoppAddresd.Api` | `Controllers/ProgramController.cs` (`POST /api/v1/program/nutrition/log`) |
| Seeder | `src/CoppAddresd.Api` | `Seeders/ProgramProgressSeeder.cs` (4 reglas `nutrition` + 5 `habit_templates`) |

### 18.7 `NUTRITION_PHOTO` (diferido)

El móvil mock registra comidas con foto ("Registrar lo que comí", IA analiza gramos/kcal/adherencia). Este paso **no** implementa análisis de foto: el log es directo del paciente y el premio por evidencia fotográfica (mayor base, p. ej. `NUTRITION_PHOTO`) queda documentado para cuando exista el servicio de visión. No se agregan columnas de evidencia en `habit_checks`.

### 18.8 Acceptance criteria

AC-33..AC-36 (SPEC §10.2). Tests **opcionales** en esta fase, corridos manualmente por el usuario per el workflow actual (gate = build verde + migración generada sin aplicar).

---

## 19. Nutriobiótico streak (P1.5 — "Paso 7a")

La tarea `nutribiotico` (el pilar de producto de CoppAddresd) mantiene su **propia racha consecutiva**, independiente de la racha general del programa (SPEC §6.6/§17): solo se alimenta al completar la tarea nutribiotico, un día perdido la rompe y los congelamientos **NO la protegen**. Premia la constancia con hitos de corrida (7/14/30/60/90 días) otorgados en el camino de completación de la tarea. **Tests OPTIONALES / manuales per el workflow actual** (gate = build verde + migración generada sin aplicar).

### 19.1 Columnas nuevas en `app.streak_states` (migración `AddProgramProgressNbStreak`, aditiva y reversible)

| Columna | Tipo | Constraints | Notas |
|---------|------|-------------|-------|
| `nb_current_streak` | `smallint` | NOT NULL DEFAULT `0` | Racha consecutiva de la tarea nutribiotico (0 = sin corrida activa) |
| `nb_longest_streak` | `smallint` | NOT NULL DEFAULT `0` | Máximo histórico de `nb_current_streak` |
| `nb_last_completed_date` | `date` | NULL | Fecha local del último día que aportó a la racha; NULL hasta la primera completación |

Convenciones de `streak_states` (PK = `enrollment_id`, snake_case, defaults en migración). No se agregan tablas nuevas ni índices (una fila por inscripción, lecturas por PK).

### 19.2 Reglas sembradas (5 nuevas en `app.xp_rules`, categoría `nutriobiotic`)

`ProgramProgressSeeder` las siembra idempotentemente por `code` (mismo `ON CONFLICT (code) DO NOTHING` que el resto del catálogo):

| Code | Categoría | BaseXp | MaxPerDay | MaxPerWeek | RequiresValidation |
|------|-----------|--------|-----------|------------|--------------------|
| `NB_STREAK_7` | nutriobiotic | 50 | 1 | 1 | false |
| `NB_STREAK_14` | nutriobiotic | 100 | 1 | 1 | false |
| `NB_STREAK_30` | nutriobiotic | 250 | 1 | 1 | false |
| `NB_STREAK_60` | nutriobiotic | 500 | 1 | 1 | false |
| `NB_STREAK_90` | nutriobiotic | 1000 | 1 | 1 | false |

Los miembros `NB_STREAK_7/14/30/60/90` de `XpReason` (16..20) y las constantes homónimas de `XpRuleCodes` persisten el `reason` del libro mayor con el nombre del miembro (precedente `CLINICAL_*`/`NUTRITION_*`).

### 19.3 Mantenimiento de la racha propia (B)

En el camino de completación (`ProgramRepository.CompleteTaskCoreAsync`, dentro de la transacción con la inscripción bloqueada `FOR UPDATE`), **solo en la primera escritura** de `task_code = 'nutribiotico'` (el replay idempotente nunca llega acá):

1. **Nuevo conteo**: si `nb_last_completed_date == ayer` (fecha local del paciente) → `nb_current_streak + 1`; en cualquier otro caso (sin historial o día perdido) → `1`. La racha NO distingue días perfectos ni umbrales (SPEC §17): solo pregunta si ayer se completó la tarea.
2. **Actualización** vía `ExecuteUpdate` (convención del repositorio): `nb_current_streak = nuevo`, `nb_longest_streak = MAX(nb_longest_streak, nuevo)`, `nb_last_completed_date = hoy`, `updated_at = now`.
3. **Hito alcanzado** (el nuevo conteo cae exactamente en 7/14/30/60/90): otorga la XP del hito por el camino de resolución del catálogo — `rule_code = NB_STREAK_{days}`, `source_ref_type = 'nb_milestone'`, `source_ref_id = task_completions.id` (la completación que disparó el hito), `reason = 'NB_STREAK_{days}'`. El multiplicador del paciente (SPEC §16, C) aplica como en todo otorgamiento. Los topes 1/día y 1/semana se verifican en `ResolveXpAwardAsync`: un tope alcanzado **omite** el hito (nunca rompe la completación ni la racha).

> **Nota de idempotencia (dedupe por corrida, AC-39)**: el dedupe parcial `(source_ref_type, source_ref_id, reason)` del libro mayor usa `source_ref_id = task_completions.id`, NO `streak_states.enrollment_id` como los hitos de la racha general (SPEC §16, una vez por inscripción). Cada corrida nueva genera una completación distinta al alcanzar el hito, por lo que el re-otorgamiento de una corrida posterior **no colisiona** con el de la anterior: **cada 7/14/30/60/90 días de corrida re-otorga su hito** (AC-39). Dentro de la misma semana el tope `max_per_week = 1` de la regla limita el farm (el otorgamiento se omite, sin error).

### 19.4 Reglas que NO cambian

- La racha general (`current_streak`/`longest_streak`/`last_active_date`), los congelamientos y el rescate (SPEC §17) quedan **intactos**: la racha del nutribiótico es un contador aparte.
- La tarea `nutribiotico` sigue otorgando sus puntos de plantilla (`TASK_NUTRIBIOTICO`, 80 base) al completarse; los hitos `NB_STREAK_*` son XP **aditiva** (decisión 25).
- El bonus de día perfecto (`DAY_BONUS`) y los hitos de la racha general (`STREAK_*`, SPEC §16) no se tocan.

### 19.5 Exposición en el snapshot (D)

`GET /api/v1/program/me/snapshot` (bloque `streak`, aditivo — los campos existentes no cambian):

| Campo | Tipo | Semántica |
|-------|------|-----------|
| `nbStreak` | `int` | Racha consecutiva actual de la tarea nutribiotico |
| `nbLongestStreak` | `int` | Máximo histórico de la racha del nutribiótico |
| `nbNextMilestone` | `{ days, xp, daysRemaining } \| null` | Próximo hito por encima de `nbStreak` (de la tabla 7/14/30/60/90 con su XP base del catálogo); `null` si la racha ya es ≥ 90 |

La lectura nunca escribe (mismo patrón que el multiplicador, SPEC §16, D).

### 19.6 Acceptance criteria

AC-37, AC-38, AC-39 (SPEC §10.2). Tests **opcionales** en esta fase, corridos manualmente por el usuario per el workflow actual (gate = build verde + migración generada sin aplicar).

### 19.7 Implementation placement (espejo de §18.6)

| Concern | Project | Folder |
|---------|---------|--------|
| Entidad + enums | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/StreakState.cs` (3 propiedades `Nb*`); `Enums/ProgramProgress/XpReason.cs` (5 miembros `NB_STREAK_*`), `XpRuleCodes.cs` (5 constantes) |
| EF configuration | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/StreakStateConfiguration.cs` (3 columnas, `smallint` default 0 + `date` nullable) |
| Migración | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddProgramProgressNbStreak.cs` (aditiva y reversible: 3 columnas en `streak_states`) |
| Motor de la racha + hitos | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` (`UpdateNbStreakAsync` + tabla `NbMilestones`; hook en `CompleteTaskCoreAsync` solo para `nutribiotico`; `GetSnapshotAsync` con `nbStreak`/`nbLongestStreak`/`nbNextMilestone`) |
| Snapshot DTO | `src/CoppAddresd.Application` | `DTOs/ProgramProgress/ProgramProgressDtos.cs` (`StreakInfoDto` + 3 campos aditivos con default; `NbNextMilestoneDto` nuevo) |
| Seeder | `src/CoppAddresd.Api` | `Seeders/ProgramProgressSeeder.cs` (5 reglas `NB_STREAK_*`, categoría `nutriobiotic`) |

---

## 20. Gamified notifications (P1.5 — "Paso 7b")

La gamificación reconoce los logros **en el momento en que ocurren** con una
notificación push (FCM) más un log persistente (`app.notifications`). El
backend no tiene colas/cron (patrón documentado, §13.3), por lo que la
notificación se dispara **transaccionalmente dentro del flujo de otorgamiento**
(tras la escritura de la XP, solo en la primera concesión — nunca en replay)
con semántica **best-effort**: un fallo de envío o de persistencia jamás rompe
la transacción de XP (AC-42). Reutiliza la infraestructura FCM **existente**
(`app.device_tokens` + `IFcmClient`, módulo de notificaciones) — no la duplica.

### 20.1 New table `app.notifications`

Convenciones del módulo (§3): schema `app.`, PK `uuid` con `gen_random_uuid()`,
timestamps `timestamptz`, auditoría trigger-based (tabla sin PHI, §8.5).

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `patient_id` | `uuid` | NOT NULL, FK `app.patient_profiles(id)` ON DELETE RESTRICT | Dueño de la notificación |
| `type` | `varchar(60)` | NOT NULL | `milestone_reached` / `nb_milestone` / `level_up` / `day_complete` (SPEC §20, C) |
| `title` | `varchar(120)` | NOT NULL | Título corto del push |
| `message` | `text` | NOT NULL | Copy de gamificación (sin PHI) |
| `priority` | `varchar(20)` | NOT NULL DEFAULT `'normal'` | `normal` / `high` / `critical` (la crítica ignora el horario de silencio) |
| `channel` | `varchar(20)` | NOT NULL DEFAULT `'push'` | Canal de entrega (solo `push` en MVP) |
| `sent_at` | `timestamptz` | NOT NULL DEFAULT `now()` | Instante de generación (reloj del servidor) |
| `read_at` | `timestamptz` | NULL | Marca de lectura del centro de notificaciones |

Index: `ix_notifications_patient_sent_at` (`patient_id`, `sent_at` DESC) — soporta
el listado del centro de notificaciones y los conteos anti-spam por día.

### 20.2 Service semantics (B)

`IGamifiedNotificationService` (Application, `Services/ProgramProgress/`):

1. **Anti-spam** (config `Program:Notifications`, defaults): máx. **2 por tipo
   por día local** (`MaxPerTypePerDay` = 2), máx. **6 totales por día local**
   (`MaxPerDay` = 6). Se cuenta contra `app.notifications` en la ventana del
   día local del paciente (fecha local → rango UTC, DST-aware). Límite
   alcanzado → **se omite en silencio** (log debug; AC-41).
2. **Horario de silencio** (`QuietHoursStart` = 22 / `QuietHoursEnd` = 7,
   hora local del paciente): fuera de la ventana de entrega salvo que la
   prioridad sea `critical`.
3. **Orden**: contexto del paciente (timezone + userId) → anti-spam → persistir
   el log → push FCM best-effort (fan-out de `app.device_tokens` vía
   `IDeviceTokenRepository` + `IFcmClient`; un token obsoleto `UNREGISTERED` se
   elimina). Todo el cuerpo captura excepciones: el servicio **nunca lanza**
   (AC-42: el flujo de otorgamiento de XP continúa intacto).

### 20.3 Trigger map (C) — transaccional, solo primera concesión

En `ProgramRepository` (dentro de `CompleteTaskCoreAsync` / los motores de
hitos, transacción con la inscripción `FOR UPDATE`), tras el otorgamiento de la
XP correspondiente:

| Evento | `type` | Mensaje (message) | Prioridad |
|--------|--------|-------------------|-----------|
| Hito de racha alcanzado (7/11/22/50) | `milestone_reached` | `🏆 ¡X días! +N XP` (hitos 11/22/50 con x2 activado: `· ¡x2 por N horas!` plegado en el mismo mensaje) | high |
| Hito de la racha del nutribiótico (7/14/30/60/90) | `nb_milestone` | `💊 ¡X días tomando tu Nutriobiótico!` | high |
| Subida de nivel (cruce de umbral de la escalera `XpLevels`) | `level_up` | `⭐ ¡Subiste a Nivel X!` (X = nombre del nivel alcanzado) | high |
| Día perfecto (bonus `DAY_BONUS` otorgado) | `day_complete` | `✅ Día perfecto · +N XP` (N = bonus efectivo) | normal |

Reglas:
- **Solo primera concesión**: el replay idempotente (`CompleteTaskAsync`) nunca
  genera notificaciones; el guardia "XP ya otorgada" del hito de racha
  (§16, B.3) y los topes del nutribiótico (§19, B.4) protegen también la
  notificación.
- **Subida de nivel**: se compara el nivel ANTES y DESPUÉS de todos los
  otorgamientos del día (tarea + bonus + hitos), evaluado tras el flush del
  libro mayor para que el balance incluya las filas nuevas.
- La notificación corre **después** de la escritura de la XP y **nunca la
  revierte** (best-effort).

### 20.4 FUTURE items (deferred — require a scheduler)

No hay cron/queue en este backend (patrón documentado, §13.3). Lo siguiente
queda documentado para cuando exista un scheduler (P3):

- `multiplier_expiring` ("tu x2 vence en N horas") — timing de expiración del
  multiplicador (§16).
- Racha en riesgo antes del fin del día local ("te faltan X tareas") — timing
  de fin de día.
- Evaluación semanal / reporte de la semana — timing semanal.

### 20.5 Patient endpoints (D)

Base `/api/v1/program`, `[Authorize]` + `Program.View` (código existente), el
`patientId` se resuelve SIEMPRE del JWT vía `IProgramActorContext` (nunca del
body) — sin perfil de paciente → 404 (anti-IDOR AC-11):

- `GET /api/v1/program/notifications?page=1&pageSize=20` — centro de
  notificaciones paginado (máx. pageSize 100), orden descendente por
  `sent_at`, con `readAt` por fila y `unreadCount` total para el badge del
  móvil.

  **Response 200**

  ```json
  {
    "data": [
      { "id": "…", "type": "milestone_reached", "title": "🏆 ¡7 días!",
        "message": "🏆 ¡7 días! +100 XP", "priority": "high", "channel": "push",
        "sentAt": "2026-09-27T11:14:08Z", "readAt": null }
    ],
    "total": 1, "page": 1, "pageSize": 20, "totalPages": 1, "unreadCount": 1
  }
  ```

- `POST /api/v1/program/notifications/{id:guid}/read` — marca la notificación
  como leída (`read_at = now`). Si no pertenece al paciente → 404 (sin
  distinguir si existe). Respuesta `204 No Content`.

### 20.6 Acceptance criteria

Ver §10.2 (AC-40, AC-41, AC-42). Tests **OPCIONALES** en esta fase, corridos
manualmente por el usuario per el workflow actual (gate = build verde +
migración generada sin aplicar).

### 20.7 Implementation placement (espejo de §18.6/§19.7)

| Concern | Project | Folder |
|---------|---------|--------|
| Entidad | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/AppNotification.cs` (nuevo) |
| EF configuration | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/AppNotificationConfiguration.cs` (nuevo); `DbSet` en `Persistence/AppDbContext.cs` |
| Migración | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddProgramProgressNotifications.cs` (aditiva y reversible: 1 tabla + índice + auditoría + GRANTs) |
| Servicio (Application) | `src/CoppAddresd.Application` | `Services/ProgramProgress/{IGamifiedNotificationService,GamifiedNotificationService}.cs` (nuevos) |
| Repositorio del log | `src/CoppAddresd.Application` | `Interfaces/INotificationLogRepository.cs` (nuevo) |
| Implementación EF | `src/CoppAddresd.Infrastructure` | `Repositories/NotificationLogRepository.cs` (nuevo) |
| Disparadores | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` (hooks en `CompleteTaskCoreAsync`, `AwardStreakMilestoneIfReachedAsync`, `UpdateNbStreakAsync`) |
| Query / command + DTOs | `src/CoppAddresd.Application` | `Features/ProgramProgress/Queries/ListNotifications/`, `Features/ProgramProgress/Commands/MarkNotificationRead/`, `Features/ProgramProgress/DTOs/Notifications/` |
| Controller | `src/CoppAddresd.Api` | `Controllers/ProgramController.cs` (`GET/POST /api/v1/program/notifications...`) |
| DI | `src/CoppAddresd.Infrastructure` | `DependencyInjection.cs` (registro del servicio + repositorio) |

---

## 21. Weakness detection & weekly assessment (P1.5 — "Paso 7c")

La misma data que ya alimenta los puntajes (SPEC §13) y la nutrición (SPEC §18) se convierte en **debilidades accionables** para el clínico: el motor determinista de reglas (ADRED-inspired, sin ML) detecta hallazgos en `POST /scores/calculate` y los persiste en un log nuevo (`app.weaknesses`) con dedupe idempotente. La **narrativa semanal LLM** ("AI weekly assessment") queda **documentada como contrato FUTURO** (§21.5): este backend NO llama IA en este paso (el endpoint del ai-service no existe; no se inventan llamadas).

### 21.1 New table `app.weaknesses`

Convenciones del módulo (§3): schema `app.`, PK `uuid` con `gen_random_uuid()`, timestamps `timestamptz`. **La tabla NO se adjunta al trigger de auditoría** (`description` puede contener contexto clínico — misma exclusión por diseño que `emotional_records`, §8.5).

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `patient_id` | `uuid` | NOT NULL, FK `app.patient_profiles(id)` ON DELETE RESTRICT | Dueño del hallazgo |
| `code` | `varchar(60)` | NOT NULL | Código canónico de la regla (`WK_*`, §21.2); llave del dedupe AC-43 |
| `category` | `varchar(40)` | NOT NULL, CHECK IN (`nutritional`,`clinical`,`psychological`,`exercise`,`adherence`,`supplement`,`sleep`,`motivation`) | Eje funcional (enum `WeaknessCategory`) |
| `severity` | `varchar(20)` | NOT NULL DEFAULT `'low'`, CHECK IN (`low`,`medium`,`high`,`critical`) | Enum `WeaknessSeverity` |
| `title` | `varchar(120)` | NOT NULL | Título corto legible |
| `description` | `text` | NULL | Hallazgo con indicador + acción sugerida (puede contener contexto clínico → sin auditoría) |
| `detected_at` | `timestamptz` | NOT NULL DEFAULT `now()` | Reloj del servidor |
| `metric_id` | `uuid` | NULL, FK `app.measurement_metrics(id)` ON DELETE RESTRICT | Métrica del indicador (glucosa, % grasa) cuando la regla la tiene |
| `indicator_value` | `numeric(10,4)` | NULL | Valor que disparó la regla (p. ej. 58 = adherencia 58%, 128 = glucosa mg/dL) |
| `source` | `varchar(20)` | NOT NULL DEFAULT `'ai'`, CHECK IN (`ai`,`professional`,`system`) | Enum `WeaknessSource`; una fila `ai` validada después permanece `ai` |
| `status` | `varchar(20)` | NOT NULL DEFAULT `'open'`, CHECK IN (`open`,`acknowledged`,`in_intervention`,`resolved`,`dismissed`) | Enum `WeaknessStatus`; ciclo de vida §21.4 |
| `assigned_to` | `uuid` | NULL, FK `auth.users(id)` (raw SQL, ON DELETE SET NULL) | Clínico asignado al caso |
| `resolved_at` | `timestamptz` | NULL | Se fija solo al transicionar a `resolved` |
| `created_at` / `updated_at` | `timestamptz` | NOT NULL DEFAULT `now()` / NULL | |

Indexes: `ix_weaknesses_patient_status` (`patient_id`, `status`) — cola del paciente y dedupe AC-43; `ix_weaknesses_status` — cola clínica global; `IX_weaknesses_metric_id` (FK).

### 21.2 Rules engine (deterministic, ADRED-inspired)

`WeaknessRulesEngine` (Application, función pura sin I/O) evalúa un `PatientWeeklyData` — paquete semanal reunido por el repositorio sobre la MISMA ventana que el Índice de Salud (SPEC §13.2) — y devuelve descriptores de debilidad. Un indicador `null` (sin dato físico) NO dispara su regla ("sin datos → sin hallazgo", nunca penaliza por ausencia).

| Código | Categoría | Severidad | Condición | Fuente | Acción sugerida |
|--------|-----------|-----------|-----------|--------|-----------------|
| `WK_NUT_LOW_ADHERENCE` | nutritional | medium | adherencia nutricional < 70% | `app.habit_checks` categoría `alimentacion` (MISMA fuente que la dimensión `nutrition`, §13.4.3/§18) | `create_intervention` |
| `WK_NUT_CRITICAL` | nutritional | high | adherencia nutricional < 50% | ídem | `telehealth_referral` (nutricionista) |
| `WK_CLIN_GLUCOSE_HIGH` | clinical | high | tendencia `up` de glucosa (vs línea base) && actual > 125 mg/dL — **REQUIRES_CLINICAL_VALIDATION** | `app.clinical_baselines` + `app.clinical_measurements` (código `glucose`) | `referral_doctor` |
| `WK_CLIN_BODYFAT_UP` | clinical | medium | delta de % grasa > 0.3 puntos en la semana | ídem (código `body_fat`) | `referral_nutritionist` |
| `WK_PSY_LOW_MOTIVATION` | psychological | medium | motivación < 5/10 | último `app.emotional_records`; **proxy documentado**: el módulo solo persiste `mood_score` 1..5 → `motivation = mood × 2` (escala 1..10; dispara con ánimo ≤ 2) | `referral_psychologist` |
| `WK_PSY_HIGH_STRESS` | psychological | medium | estrés > 7/10 — **LATENTE**: no existe columna física de estrés; dispara cuando una fuente futura alimente `StressScore` | — | `referral_psychologist` |
| `WK_PSY_SLEEP_POOR` | sleep | low | sueño promedio < 6 h — **LATENTE**: no existe fuente de sueño; dispara cuando una fuente futura alimente `AvgSleepHours` | — | `ai_recommendation` |
| `WK_ADH_LOW_STREAK` | adherence | medium | adherencia semanal < 50% | dimensión `adherence` de `app.health_scores` del período (§13.4.1) | `recovery_mode` |
| `WK_ADH_NB_MISSED` | supplement | low | adherencia del nutribiótico a 7 días < 70% | `app.task_completions` (`nutribiotico`, días distintos / 7) | `ai_recommendation` |
| `WK_ADH_EXERCISE_LOW` | exercise | low | cumplimiento de ejercicio < 60% | `app.task_completions` (`ejercicio`, días distintos / días del período) | `reto_adjustment` |

> **REQUIRES_CLINICAL_VALIDATION**: los umbrales clínicos (glucosa 125 mg/dL, delta de % grasa 0.3) son propuestos por el equipo (referencia ADRED adaptada) y deben confirmarse con el comité clínico antes de operar como referencia. Las reglas LATENTES (estrés/sueño) están implementadas en el motor pero no pueden disparar hasta que exista la fuente de datos.

### 21.3 Trigger (AC-45)

- La detección corre SOLO en `POST /api/v1/program/scores/calculate`, **una vez por recálculo**, después de puntajes + XP clínica + premios semanales de nutrición (el paquete semanal lee la fila fresca de `health_scores`). `GET /api/v1/program/scores` nunca detecta ni persiste debilidades.
- `WeaknessDetectionService` (Application): reúne el paquete vía repositorio → evalúa con `WeaknessRulesEngine` → persiste SOLO las NUEVAS (`PersistDetectedWeaknessesAsync`, dedupe AC-43). Idempotente por el estado abierto: re-correr `/calculate` no duplica.
- Las filas se crean con `source = 'ai'`, `status = 'open'`, `detected_at = now()` y el `indicator_value`/`metric_id` de la regla.

### 21.4 API contract

Base `/api/v1/program`, `[Authorize]`. El paciente NUNCA transiciona estados (AC-44 → 403 vía guardia clínica AC-22).

- `GET /api/v1/program/weaknesses?page=1&pageSize=20` — `Program.View`; debilidades del paciente autenticado (el `patientId` se resuelve del JWT, nunca del body — anti-IDOR AC-11: sin perfil → 404). Orden descendente por `detectedAt`, paginado (default 20, máx. pageSize 100).

  **Response 200**
  ```json
  {
    "data": [
      { "id": "…", "patientId": "…", "code": "WK_NUT_LOW_ADHERENCE", "category": "nutritional",
        "severity": "medium", "title": "Adherencia nutricional baja",
        "description": "Adherencia nutricional del período 58% (< 70%). Acción sugerida: crear intervención de seguimiento nutricional.",
        "detectedAt": "2026-09-27T11:14:08Z", "metricId": null, "indicatorValue": 58,
        "source": "ai", "status": "open", "assignedTo": null, "resolvedAt": null,
        "createdAt": "2026-09-27T11:14:08Z", "updatedAt": null }
    ],
    "total": 1, "page": 1, "pageSize": 20, "totalPages": 1
  }
  ```

- `GET /api/v1/program/weaknesses/open?page=1&pageSize=20` — `Program.Adapt`; cola clínica de filas `status = 'open'` (todas los pacientes), orden ascendente FIFO por `detectedAt` (la más antigua primero). Mismo shape paginado.

- `POST /api/v1/program/weaknesses/{id:guid}/status` — `Program.Adapt` + rol clínico (guardia AC-22: `Physician`/`Nutritionist`/`Psychologist`/`ClinicalDirector`/`Admin`; paciente → `403 FORBIDDEN`). Body `{ "status": "acknowledged"|"in_intervention"|"resolved"|"dismissed" }`. `resolved` fija `resolved_at`; `open` se rechaza (la fila ya nace abierta); transición idempotente (mismo estado → 200 sin error). 404 si no existe.

  **Errors**: `400` (estado inválido), `403 FORBIDDEN` (sin permiso o no clínico), `404 NOT_FOUND`.

### 21.5 FUTURE — AI weekly assessment contract (documented, NOT implemented)

La evaluación semanal narrativa (LLM) queda **contratada pero diferida a P3** — necesita trabajo en el `ai-service` + un scheduler o disparo manual (no hay cron en este backend, §13.3):

- **Backend** (a construir): ensambla un `WeeklyAssessmentContext` y llama a un **endpoint NUEVO del ai-service** (a crear en ese repo; no existe hoy):
  ```json
  {
    "patientId": "…",
    "periodStartLocalDate": "2026-09-21",
    "periodEndLocalDate": "2026-09-27",
    "healthScore": 81,
    "transformationScore": 88,
    "dimensions": { "adherence": 66, "clinical": 75, "nutrition": 80, "psychology": 90, "exercise": 100 },
    "weaknesses": [ { "code": "WK_NUT_LOW_ADHERENCE", "category": "nutritional", "severity": "medium", "indicatorValue": 58 } ],
    "adherenceSummary": { "nutritionPct": 58, "weeklyPct": 45, "nb7dPct": 86, "exercisePct": 33 }
  }
  ```
- **ai-service** (a crear): devuelve la narrativa en el shape JSON de ADRED: `{ summary, strengths, weaknesses_text, recommendations, next_actions }`.
- **Disparo**: desde el scheduler futuro (pre-warm semanal de `app.health_scores`, §13.3) o un disparo manual del clínico; la detección determinista de §21.2/§21.3 ya alimenta el contexto.

### 21.6 Acceptance criteria

Ver §10.2 (AC-43, AC-44, AC-45). Tests **OPCIONALES** en esta fase, corridos manualmente por el usuario per el workflow actual (gate = build verde + migración generada sin aplicar).

### 21.7 Implementation placement (espejo de §20.7)

| Concern | Project | Folder |
|---------|---------|--------|
| Entidad + enums | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/Weakness.cs`; `Enums/ProgramProgress/WeaknessCategory.cs`, `WeaknessSeverity.cs`, `WeaknessStatus.cs`, `WeaknessSource.cs`, `WeaknessCodes.cs` |
| EF configuration + DbSet | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/WeaknessConfiguration.cs`; `Persistence/AppDbContext.cs` |
| Migración | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddProgramProgressWeaknesses.cs` (aditiva y reversible: 1 tabla + índices + CHECKs + FK SQL a `auth.users` + GRANTs; **sin trigger de auditoría**, §21.1) |
| Motor de reglas + servicio | `src/CoppAddresd.Application` | `Services/ProgramProgress/{PatientWeeklyData,WeaknessDescriptor,WeaknessRulesEngine,IWeaknessDetectionService,WeaknessDetectionService}.cs` |
| Repositorio | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` (`BuildPatientWeeklyDataAsync`, `PersistDetectedWeaknessesAsync`, `ListWeaknessesAsync`, `ListOpenWeaknessesAsync`, `UpdateWeaknessStatusAsync`); `Interfaces/IProgramRepository.cs` |
| Trigger | `src/CoppAddresd.Application` | `Features/ProgramProgress/Commands/CalculateScores/CalculateScoresCommand.cs` (hook tras XP clínica + nutrición) |
| Query / command + DTOs | `src/CoppAddresd.Application` | `Features/ProgramProgress/Queries/ListWeaknesses/`, `Queries/ListOpenWeaknesses/`, `Commands/UpdateWeaknessStatus/`, `DTOs/Weaknesses/` |
| Controller | `src/CoppAddresd.Api` | `Controllers/ProgramController.cs` (3 acciones + request) |
| DI | `src/CoppAddresd.Infrastructure` | `DependencyInjection.cs` (registro del servicio) |

---

## 22. Interventions & telemedicine XP (P1.5 — "Paso 7d")

Las debilidades detectadas por el motor determinista (SPEC §21) se convierten en **intervenciones accionables** que el clínico gestiona y el paciente acepta. Cada intervención sigue una **máquina de estados** y otorga XP en eventos clave del ciclo de vida, incluyendo hooks para la integración con el servicio de telemedicina (servicio separado, schema `tele.`).

### 22.1 New table `app.interventions`

Convenciones del módulo (§3): schema `app.`, PK `uuid` con `gen_random_uuid()`, timestamps `timestamptz`. La tabla SÍ se adjunta al trigger de auditoría (sin PHI: ids, estados, timestamps, `xp_awarded_total` — precedente `notifications`).

| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| `id` | `uuid` | PK, `DEFAULT gen_random_uuid()` | |
| `patient_id` | `uuid` | NOT NULL, FK `app.patient_profiles(id)` ON DELETE RESTRICT | Dueño de la intervención |
| `weakness_id` | `uuid` | NULL, FK `app.weaknesses(id)` ON DELETE SET NULL | Debilidad origen (una intervención por debilidad, AC-46) |
| `type` | `varchar(60)` | NOT NULL, CHECK IN (`nutrition_adjustment`, `exercise_adjustment`, `psychological_support`, `telehealth_nutrition`, `telehealth_medical`, `telehealth_psychology`, `recovery_mission`, `plan_adaptation`) | Enum `InterventionType` |
| `title` | `varchar(120)` | NOT NULL | Título corto legible |
| `description` | `text` | NULL | Descripción detallada |
| `status` | `varchar(30)` | NOT NULL DEFAULT `'detected'`, CHECK IN (`detected`, `evaluated`, `recommended`, `accepted`, `in_progress`, `completed`, `reevaluation`) | Enum `InterventionStatus` |
| `severity` | `varchar(20)` | NOT NULL DEFAULT `'medium'` | Severidad heredada de la debilidad |
| `assigned_to` | `uuid` | NULL, FK `auth.users(id)` (raw SQL, ON DELETE SET NULL) | Clínico asignado |
| `recommended_at` | `timestamptz` | NULL | Instante en que se recomendó al paciente |
| `accepted_at` | `timestamptz` | NULL | Instante en que el paciente aceptó |
| `completed_at` | `timestamptz` | NULL | Instante en que se completó |
| `patient_action` | `varchar(120)` | NULL | Acción que el paciente debe realizar |
| `result` | `text` | NULL | Resultado de la intervención |
| `xp_awarded_total` | `int` | NOT NULL DEFAULT `0` | XP acumulada por esta intervención |
| `created_at` / `updated_at` | `timestamptz` | NOT NULL DEFAULT `now()` / NULL | |

Indexes: `ix_interventions_patient_status` (`patient_id`, `status`), `ix_interventions_status`.

### 22.2 State machine

- `detected → accepted`: paciente acepta (AC-47)
- `detected → evaluated | recommended`: clínico evalúa/recomienda
- `evaluated → recommended | in_progress | reevaluation`
- `recommended → accepted | in_progress`
- `accepted → in_progress | reevaluation`
- `in_progress → completed | reevaluation`
- `reevaluation → evaluated | recommended | completed`
- `completed`: terminal

### 22.3 XP rules (seeded, category `intervention`)

| Code | BaseXp | RequiresValidation | MaxPerDay | MaxPerWeek | Trigger |
|------|--------|--------------------|-----------|------------|---------|
| `WEAKNESS_ASSESS` | 20 | false | 1 | 1 | Creación de intervención desde debilidad (AC-46) |
| `INTERV_ACCEPT` | 15 | false | 1 | 7 | Paciente acepta (AC-47) |
| `TELE_SCHEDULE` | 50 | false | 1 | 7 | Teleconsulta agendada (AC-49) |
| `TELE_ATTEND` | 100 | **true** | — | — | Clínico confirma asistencia (AC-49) |
| `TELE_COMPLY` | 50 | false | 1 | 7 | Cumplimiento evaluado (AC-49) |
| `INTERV_COMPLETE` | 200 | **true** | — | — | Intervención completada con resultado (AC-48) |
| `RECOVERY_MISSION` | 50 | false | 1 | 7 | Paciente acepta recovery_mission (AC-47) |

### 22.4 Creation from weaknesses (AC-46)

| Acción de la regla | Tipo de intervención |
|--------------------|--------------------|
| `create_intervention` | Según categoría: nutritional → `nutrition_adjustment`, exercise → `exercise_adjustment`, psychological → `psychological_support`, otro → `plan_adaptation` |
| `telehealth_referral` | Según categoría: nutritional → `telehealth_nutrition`, psychological → `telehealth_psychology`, otro → `telehealth_medical` |
| `referral_doctor` | `telehealth_medical` |
| `referral_nutritionist` | `telehealth_nutrition` |
| `referral_psychologist` | `telehealth_psychology` |
| `recovery_mode` | `recovery_mission` |
| `ai_recommendation` / `rto_adjustment` | `plan_adaptation` |

Una debilidad solo genera **una** intervención (el repositorio verifica antes de crear). La intervención nace con `status = 'detected'` y otorga `WEAKNESS_ASSESS` (+20) una vez. La debilidad se transiciona a `in_intervention`.

### 22.5 API contract

Base `/api/v1/program`, `[Authorize]`.

- `GET /api/v1/program/interventions` — Paciente: intervenciones propias, paginadas. `Program.View`.
- `POST /api/v1/program/interventions/{id:guid}/accept` — Paciente acepta (AC-47). `Program.View`.
- `GET /api/v1/program/interventions/open` — Clínico: cola de intervenciones abiertas. `Program.Adapt`.
- `POST /api/v1/program/interventions/{id:guid}/status` — Clínico: `{ status, result?, assignedTo? }`. `Program.Adapt` + rol clínico (AC-22).
- `POST /api/v1/program/interventions/{id:guid}/tele-scheduled` — Hook de telemedicina (AC-49). `Program.Adapt`.
- `POST /api/v1/program/interventions/{id:guid}/tele-attended` — Hook de telemedicina (AC-49). `Program.Adapt`.
- `POST /api/v1/program/interventions/{id:guid}/tele-comply` — Hook de telemedicina (AC-49). `Program.Adapt`.

### 22.6 Cross-service integration contract (DOCUMENT ONLY)

El servicio de **Telemedicina** (servicio separado, schema `tele.`, repo separado) debe llamar a los hooks de telemedicina cuando schedule/attend una cita vinculada a una intervención:

1. **Al agendar**: `POST /api/v1/program/interventions/{id}/tele-scheduled` (el servicio de tele almacena un `program_intervention_id` en su cita).
2. **Al confirmar asistencia**: `POST /api/v1/program/interventions/{id}/tele-attended`.
3. **Al evaluar cumplimiento**: `POST /api/v1/program/interventions/{id}/tele-comply`.

**Contrato a acordar**: la tabla `tele.telemedicine_appointments` ganará una columna `program_intervention_id UUID NULL` en un paso futuro del servicio de telemedicina. La API principal no escribe en la tabla de telemedicina; la integración es unidireccional (tele → program via HTTP hooks).

### 22.7 Acceptance criteria

- **AC-46**: Debilidad con acción `create_intervention` → se crea una intervención `detected` + `WEAKNESS_ASSESS` +20 (una vez por debilidad).
- **AC-47**: Paciente acepta (`detected→accepted`) → `INTERV_ACCEPT` +15; si es `recovery_mission` también `RECOVERY_MISSION` +50. 404 si no pertenece; 409 si estado inválido.
- **AC-48**: Clínico completa con resultado → `INTERV_COMPLETE` +200 (validated_by = clínico) + debilidad → `resolved`. Requiere resultado; estado inválido → 409.
- **AC-49**: Hooks de telemedicina: `tele-scheduled` → `TELE_SCHEDULE` +50; `tele-attended` → `TELE_ATTEND` +100 (validated_by); `tele-comply` → `TELE_COMPLY` +50. Cada uno una vez por intervención (dedupe parcial).

### 22.8 Implementation placement

| Concern | Project | Folder |
|---------|---------|--------|
| Entidad + enums | `src/CoppAddresd.Domain` | `Entities/ProgramProgress/Intervention.cs`; `Enums/ProgramProgress/InterventionType.cs`, `InterventionStatus.cs` |
| EF configuration + DbSet | `src/CoppAddresd.Infrastructure` | `Configurations/ProgramProgress/InterventionConfiguration.cs`; `Persistence/AppDbContext.cs` |
| Migración | `src/CoppAddresd.Infrastructure` | `Migrations/<timestamp>_AddProgramProgressInterventions.cs` |
| Servicio de detección | `src/CoppAddresd.Application` | `Services/ProgramProgress/WeaknessDetectionService.cs` |
| Repositorio | `src/CoppAddresd.Infrastructure` | `Repositories/ProgramRepository.cs` + `Interfaces/IProgramRepository.cs` |
| Query / command + DTOs | `src/CoppAddresd.Application` | `Features/ProgramProgress/Queries/ListInterventions/`, `Queries/ListOpenInterventions/`, `Commands/AcceptIntervention/`, `Commands/UpdateInterventionStatus/`, `Commands/MarkTeleScheduled/`, `Commands/MarkTeleAttended/`, `Commands/MarkTeleComply/`, `DTOs/Interventions/` |
| Controller | `src/CoppAddresd.Api` | `Controllers/ProgramController.cs` (7 acciones + request) |
| Seeder | `src/CoppAddresd.Api` | `Seeders/ProgramProgressSeeder.cs` (7 reglas `intervention`) |
