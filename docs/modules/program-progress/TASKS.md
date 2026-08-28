# TASKS — Program Progress Module

> Dependency-ordered implementation backlog for the 83-week patient program.
> Source of truth: [`PLAN.md`](./PLAN.md) (master plan) + [`SPEC.md`](./SPEC.md)
> (contract). All entity names, endpoint names, phase IDs, and acceptance
> criteria in this file match SPEC.md. Tasks T-XX IDs are stable and reused
> in PR titles, commit messages, and the review checklist.

---

## 0. Phases & batches at a glance

| Phase | Batches | Exit criteria |
|-------|---------|---------------|
| **P0 — Plan** | (this file + SPEC + PLAN) | Reviewer sign-off (Gate G1) |
| **P1 — MVP** | B1 Foundations · B2 Seeder · B3 Repository · B4 Application · B5 API · B5-S Health & Transformation Score engine · B5-R XP Rules catalog · B5-C Clinical XP · B5-M Streak multiplier (P1.5) · B5-T Streak threshold & essentials (P1.5) · B5-N Granular nutrition XP (P1.5) · B5-NB Nutriobiótico streak (P1.5) · B5-NOT Gamified notifications (P1.5) · B5-WK Weakness detection (P1.5) · B5-IV Interventions & tele XP (P1.5) · B6 Mobile Slice 1+2 · B7 ERP Phase-1 | All P1 acceptance scenarios AC-01..AC-15 pass; P1.5 acceptance scenarios AC-19..AC-22 pass; XP rules acceptance AC-23/AC-24 pass (manual, B5-R); clinical XP acceptance AC-25..AC-27 pass (manual, B5-C); streak multiplier acceptance AC-28..AC-30 pass (manual, B5-M); streak threshold & essentials acceptance AC-31/AC-32 pass (manual, B5-T); granular nutrition acceptance AC-33..AC-36 pass (manual, B5-N); nutribiótico streak acceptance AC-37..AC-39 pass (manual, B5-NB); gamified notifications acceptance AC-40..AC-42 pass (manual, B5-NOT); weakness detection acceptance AC-43..AC-45 pass (manual, B5-WK); intervention acceptance AC-46..AC-49 pass (manual, B5-IV); Gate G2 review before apply |
| **P2 — Adaptation** | B8 Mobile Slice 3+4 · B9 Adaptation engine · B10 ERP Adaptation queue · B11 Media rotation | AC-16/AC-17 pass; clinician can approve and the next week reflects new content |
| **P3 — Enhancements** | B12 Streak rescue audit · B13 Bulk enrollment · B14 CSV export · B15 i18n | All P2 acceptance scenarios pass; locale parity in EN/ES |
| **Gate G3 — Final verification** | B16 Verification & observability | All SPEC §10.2 acceptance scenarios pass; mobile demo continuity proven |

---

## Gate G1 — Plan approval (blocks P1)

| Gate | Owner | Trigger | Exit |
|------|-------|---------|------|
| **G1** | Reviewer | PLAN.md + SPEC.md + TASKS.md merged | Reviewer sign-off recorded in PR; SPEC decision table (§0) accepted as binding |

---

## Batch B1 — Foundations (P1, no I/O)

### T-01 — Domain entities and enums

- **Phase**: P1
- **Depends on**: —
- **Objective**: Add 11 new domain entities and supporting enums in `CoppAddresd.Domain`.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/ProgramTemplate.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/WeeklyDayTemplate.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/ProgramEnrollment.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/ProgramWeek.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/DailyCheckIn.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/TaskCompletion.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/XpLedgerEntry.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/StreakState.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/StreakFreeze.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/AdaptationRecommendation.cs`
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/EmotionalRecord.cs`
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/ProgramEnrollmentStatus.cs`
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/ProgramWeekStatus.cs`
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/TaskCode.cs` (string-backed enum, fixed values: `podcast`, `vitals`, `nut`, `ejercicio`, `nutribiotico`, `emocional`)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/XpReason.cs`
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/AdaptationKind.cs`
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/AdaptationStatus.cs`
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/AdaptationTargetEntityType.cs`
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/StreakFreezeKind.cs`
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/TemplateStatus.cs`
- **Implementation notes**: Mirror Wellness entity style (`Guid Id`, `DateTime CreatedAt`, nullable `Guid? CreatedBy`, navigation collections initialized to `[]`). Status enums are `string`-backed and stored as `varchar(20)` (existing convention).
- **Acceptance criteria**:
  - Entities compile; no new NuGet packages.
  - Every `CreatedBy`/`UpdatedBy` is `Guid?` with a SQL FK (no EF navigation), per SPEC §3 intro.
  - `TaskCode` enum has exactly the 6 codes the mobile uses (verifiable via `Enum.GetNames`).
- **Verification**: `dotnet build src/CoppAddresd.Domain` returns 0 errors.
- **Suggested agent role**: `sdd-apply` (Tier 2 — implementation).

### T-02 — EF configurations and DbContext registration

- **Phase**: P1
- **Depends on**: T-01
- **Objective**: Add `IEntityTypeConfiguration<T>` for each new entity and register `DbSet<>` in `AppDbContext`.
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/ProgramTemplateConfiguration.cs`
  - (and 10 sibling files, one per entity)
  - `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs` (add `DbSet<>` for each entity)
- **Implementation notes**: Mirror `NutritionPlanConfiguration` style. Set `ToTable("name", "app")`, explicit column names, `gen_random_uuid()` default, `timestamptz` timestamps, all unique constraints and indexes from SPEC §3. Nullable FK columns to existing `app.*` tables get `OnDelete(DeleteBehavior.Restrict)`; cascade only when parent is owned (e.g., `program_weeks` → `program_enrollments`).
- **Acceptance criteria**:
  - All 11 tables have a configuration file.
  - `AppDbContext` exposes a `DbSet<>` for each entity.
  - Indexes from SPEC §3 are emitted (verifiable via `dotnet ef migrations script`).
- **Verification**: `dotnet build src/CoppAddresd.Infrastructure` returns 0 errors.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-03 — Generate migration `AddProgramProgressCore`

- **Phase**: P1
- **Depends on**: T-02
- **Objective**: Produce a single EF migration that creates the 11 tables and their indexes.
- **Affected paths**: `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressCore.cs` (and Designer/Snapshot siblings).
- **Implementation notes**: `dotnet ef migrations add AddProgramProgressCore --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api --output-dir Migrations`. Do not split into multiple migrations. Verify the migration is purely additive.
- **Acceptance criteria**:
  - `dotnet ef database update` succeeds against an empty `app` schema.
  - `\d app.task_completions` shows all nullable FKs and the unique `(enrollment_id, local_date, task_code)`.
- **Verification**:
  - `dotnet ef database update --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api`
  - `psql -c "\d app.program_enrollments"` against the dev DB returns expected columns/indexes.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B2 — Seeder & permissions (P1)

### T-04 — Auth Service seeder for `Program.*` permissions

- **Phase**: P1
- **Depends on**: —
- **Objective**: Seed 5 new permission codes in `auth.permissions`.
- **Affected paths**:
  - `src/Services/CoppAddresd.Auth/SeedData/ProgramProgressPermissions.cs` (new file, invoked from the existing Auth seeder)
- **Implementation notes**: Idempotent UPSERT keyed on `code` column. Codes: `Program.View`, `Program.Edit`, `Program.Enroll`, `Program.Adapt`, `Program.ForceComplete`. Follow the convention of the existing 59-permission seeder.
- **Acceptance criteria**:
  - After Auth Service restart, `SELECT code FROM auth.permissions WHERE code LIKE 'Program.%'` returns 5 rows.
  - Re-running the seeder does not duplicate.
- **Verification**:
  - `dotnet run --project src/Services/CoppAddresd.Auth` (or rely on startup auto-seeding per AGENTS.md).
  - `psql -c "SELECT code FROM auth.permissions WHERE code LIKE 'Program.%' ORDER BY code;"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-05 — `ProgramProgressSeeder` (default 83-week template)

- **Phase**: P1
- **Depends on**: T-04
- **Objective**: Insert 1 default `program_templates` row (`default-83w`) and 7×N `weekly_day_templates` rows.
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/SeedData/ProgramProgressSeeder.cs`
  - `src/CoppAddresd.Api/Program.cs` (invoke seeder after `db.Database.Migrate()`; same pattern as Wellness seeders if they exist)
- **Implementation notes**: Idempotent by `code = 'default-83w'`. Points match mobile mock (`PROGRAM_TASKS` in `antares-paciente/src/data/program.ts`): 80/120/150/150/80/120. Weekday distribution: seed all 7 weekdays × 6 task codes so any future week has content. Configurable via `appsettings.json → Program:DefaultTemplate:Code`.
- **Acceptance criteria**:
  - After API restart, `SELECT COUNT(*) FROM app.program_templates WHERE code = 'default-83w'` = 1.
  - `SELECT COUNT(*) FROM app.weekly_day_templates WHERE template_id = '<that id>'` ≥ 42 (7 × 6).
- **Verification**:
  - `dotnet run --project src/CoppAddresd.Api` and query.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-06 — Migration applied to dev DB

- **Phase**: P1
- **Depends on**: T-03, T-05
- **Objective**: Confirm the dev DB has the new schema and seeded data.
- **Affected paths**: (no source changes)
- **Implementation notes**: Run `dotnet ef database update` against the shared dev DB; document the output in a brief PR comment.
- **Acceptance criteria**:
  - All 11 tables exist in `app.`.
  - Seeded data is present.
- **Verification**: SQL probe queries (see T-05).
- **Suggested agent role**: `cheap-gate` (Tier 0 — mechanical check).

---

## Batch B3 — Repository (P1)

### T-07 — `ProgramRepository`

- **Phase**: P1
- **Depends on**: T-03
- **Objective**: Implement `IProgramRepository` with all read/write paths required by SPEC §6 and §7.
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs`
  - `src/CoppAddresd.Application/Interfaces/IProgramRepository.cs`
- **Implementation notes**:
  - `CompleteTaskAsync` must lock the enrollment row `FOR UPDATE` inside `CreateExecutionStrategy().ExecuteAsync(...)` (same wrapper as `WellnessRepository.AddPlanWithAssignmentAsync`).
  - Use `ExecuteUpdate` for streak/freezes to avoid the navigation-tracking pitfall documented in `AddFirstVersionAndActivateAsync` (AGENTS.md).
  - `GetSnapshotAsync` is a single SQL projection; no N+1.
  - All `AsNoTracking` for reads; tracked only for the locked enrollment row.
- **Acceptance criteria**:
  - Repository exposes: `EnrollAsync`, `PauseAsync`, `ResumeAsync`, `WithdrawAsync`, `CompleteTaskAsync` (idempotent), `GetSnapshotAsync`, `GetCalendarAsync`, `GetPathAsync`, `ListTemplatesAsync`, `GetTemplateAsync`, `UpsertTemplateAsync`, `ReplaceWeekdayTasksAsync`, `ListAdaptationsAsync`, `DecideAdaptationAsync`.
  - `CompleteTaskAsync` returns a typed result that distinguishes first-write vs replay.
- **Verification**: unit tests for each method (see T-08).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-08 — Repository tests

- **Phase**: P1
- **Depends on**: T-07
- **Objective**: Unit + integration tests covering happy path, idempotency, and DB-constraint violations.
- **Affected paths**: `tests/CoppAddresd.UnitTests/ProgramProgress/ProgramRepositoryTests.cs`
- **Implementation notes**: Use the same `COP_TEST_DB_CONNECTION` pattern as the existing 7-test audit suite. Test cases: AC-01, AC-02, AC-03, AC-04, AC-05, AC-10, AC-13, AC-15 (snapshot immutability).
- **Acceptance criteria**:
  - 12+ tests pass against a fresh `coppaddresd_prog_test_<timestamp>` DB.
  - 50-thread concurrency test asserts exactly one row.
- **Verification**: `dotnet test tests/CoppAddresd.UnitTests --filter "FullyQualifiedName~ProgramRepository"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B4 — Application layer (P1)

### T-09 — Commands

- **Phase**: P1
- **Depends on**: T-07
- **Objective**: MediatR commands for every state-changing endpoint in SPEC §7.
- **Affected paths**:
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/CompleteTask/*` (`CompleteTaskCommand`, `Handler`, `Validator`)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/DecideAdaptation/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/EnrollPatient/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/PauseEnrollment/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/ResumeEnrollment/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/WithdrawEnrollment/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/CreateTemplate/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/UpdateTemplate/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/PublishTemplate/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/ArchiveTemplate/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/ReplaceWeekdayTasks/*`
- **Implementation notes**: Mirror Wellness handler style. Inject `ICurrentContext` for actor; pass `CancellationToken` everywhere; throw domain exceptions for business rule violations (mapped by `GlobalExceptionHandlerMiddleware`).
- **Acceptance criteria**: All 11 commands compile; each handler has a `Validator` (T-11).
- **Verification**: `dotnet build src/CoppAddresd.Application` returns 0 errors.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-10 — Queries and DTOs

- **Phase**: P1
- **Depends on**: T-09 (same batch can run in parallel)
- **Objective**: MediatR queries returning DTOs that match the SPEC §7 JSON shapes.
- **Affected paths**:
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/GetSnapshot/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/GetCalendar/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/GetPath/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListTemplates/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/GetTemplate/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListEnrollments/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListAdaptations/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/GetAdaptation/*`
  - `src/CoppAddresd.Application/Features/ProgramProgress/DTOs/*.cs`
- **Implementation notes**: Snapshot DTO must include `todayTasks[].content` with the resolved MediaItem title/duration/thumbnail URL (call existing `MediaController` URL helper or new `IMediaUrlResolver`).
- **Acceptance criteria**: Snapshot DTO round-trips with the JSON shape in SPEC §7.1.
- **Verification**: `dotnet build` clean.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-11 — FluentValidation validators

- **Phase**: P1
- **Depends on**: T-09
- **Objective**: Validator for every command in T-09.
- **Affected paths**: `src/CoppAddresd.Application/Features/ProgramProgress/Commands/**/*Validator.cs`
- **Implementation notes**: Validate IANA timezone strings, `localDate` not in the future, `moodScore ∈ [1,5]`, `points >= 0`, `clientRequestId` length ≤ 64. No business rules (those live in handlers).
- **Acceptance criteria**: A test that sends an invalid `clientRequestId` (e.g., 100 chars) returns `400 Validation`.
- **Verification**: `dotnet test --filter "Validator"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-12 — Handler tests

- **Phase**: P1
- **Depends on**: T-09, T-11
- **Objective**: Unit tests for handlers with mocked `IProgramRepository` and `ICurrentContext`.
- **Affected paths**: `tests/CoppAddresd.UnitTests/ProgramProgress/Handlers/*.cs`
- **Implementation notes**: Cover AC-01..AC-15 (excluding DB-only AC-02/AC-10/AC-15 which are integration). Use the same fakes-in-memory pattern as `tests/CoppAddresd.Telemedicine.UnitTests` (referenced in AGENTS.md).
- **Acceptance criteria**: 15+ handler tests pass; coverage ≥ 80% on `ProgramProgress` namespace.
- **Verification**: `dotnet test tests/CoppAddresd.UnitTests --filter "FullyQualifiedName~ProgramProgress.Handlers"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Gate G2 — Code review before apply (blocks P1 deploy)

| Gate | Owner | Trigger | Exit |
|------|-------|---------|------|
| **G2** | Backend reviewer | B1..B5 PR opened | All P1 acceptance scenarios pass locally; reviewer checklist signed off |

---

## Batch B5 — API (P1)

### T-13 — `ProgramController`

- **Phase**: P1
- **Depends on**: T-09, T-10
- **Objective**: Map every SPEC §7 endpoint to a controller action.
- **Affected paths**: `src/CoppAddresd.Api/Controllers/ProgramController.cs` (+ optional subcontrollers for `EnrollmentsController`, `TemplatesController`, `AdaptationsController`).
- **Implementation notes**: `[Authorize]` at controller level; `[RequirePermission("Program.X")]` on each action. Mirror `WellnessController` for cross-cutting patterns (CancellationToken, ILogger, ICurrentContext injection). All 404s for cross-patient reads.
- **Acceptance criteria**: All SPEC §7 endpoints reachable; OpenAPI document emits without warnings.
- **Verification**: `dotnet run --project src/CoppAddresd.Api` and `curl` against each endpoint with a test JWT.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-14 — Authorization & IDOR tests

- **Phase**: P1
- **Depends on**: T-13
- **Objective**: Integration tests for cross-patient 404, missing-permission 403, paused enrollment 409.
- **Affected paths**: `tests/CoppAddresd.UnitTests/ProgramProgress/Api/AuthorizationTests.cs`
- **Implementation notes**: Cover AC-11, AC-12. Reuse the Telemedicine integration test pattern (WebApplicationFactory + `COP_TEST_DB_CONNECTION`).
- **Acceptance criteria**: 6+ tests pass.
- **Verification**: `dotnet test --filter "FullyQualifiedName~ProgramProgress.Api"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-15 — API contract tests (snapshot/complete/calendar/path)

- **Phase**: P1
- **Depends on**: T-13
- **Objective**: WebApplicationFactory tests asserting the JSON shapes match SPEC §7.
- **Affected paths**: `tests/CoppAddresd.UnitTests/ProgramProgress/Api/ContractTests.cs`
- **Implementation notes**: Use `JsonElement` to assert shape (field presence, not brittle deep-equality). Cover AC-01, AC-05, AC-08, AC-09.
- **Acceptance criteria**: 4+ contract tests pass.
- **Verification**: `dotnet test --filter "FullyQualifiedName~ProgramProgress.Api.Contract"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-S — Health & Transformation Score engine (P1.5)

> Sits between **B5 (API)** and **B6 (Mobile Slice 1+2)** so the score endpoint is live
> before the mobile wire-up. The mobile integration itself lives in **T-40** of this
> batch; ERP / admin tooling for `app.clinical_baselines` is intentionally **out of this
> batch** and is added in a later ERP phase (the baselines are clinician-authored via a
> future clinical-flow endpoint; for MVP the seeder / direct SQL fills them in dev).

### T-35 — Domain entities, enums, and EF configurations for the 4 new tables

- **Phase**: P1.5
- **Depends on**: T-01, T-02 (Domain / EF conventions established)
- **Objective**: Add the 4 new domain entities + enums + EF configurations + `DbSet<>` registration for `health_score_weights`, `clinical_baselines`, `health_scores`, `transformation_scores`.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/HealthScoreWeight.cs` (new)
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/ClinicalBaseline.cs` (new)
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/HealthScore.cs` (new)
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/TransformationScore.cs` (new)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/ScoreDimension.cs` (new; `Adherence | Clinical | Nutrition | Psychology | Exercise`)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/ScoreTrend.cs` (new; `Up | Down | Stable`)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/FavorableDirection.cs` (new; `LowerIsBetter = -1 | HigherIsBetter = 1`)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/HealthScoreWeightConfiguration.cs` (new)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/ClinicalBaselineConfiguration.cs` (new)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/HealthScoreConfiguration.cs` (new)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/TransformationScoreConfiguration.cs` (new)
  - `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs` (add 4 `DbSet<>`)
- **Implementation notes**:
  - Mirror `HealthScoreWeightConfiguration`: explicit `ToTable("health_score_weights", "app")`, UNIQUE on `dimension`, `numeric(5,4)` precision, default value `0.30`/`0.30`/`0.20`/`0.10`/`0.10`.
  - `ClinicalBaselineConfiguration`: composite UNIQUE on `(patient_id, metric_id)`; FKs to `app.patient_profiles`, `app.measurement_metrics`, `app.unit_of_measures` all `OnDelete(DeleteBehavior.Restrict)`. `set_by` is a raw SQL FK to `auth.users(id)` (no EF navigation; same pattern as `app.patient_profiles`).
  - `HealthScoreConfiguration`: UNIQUE on `(patient_id, period_start, period_end)`; index on `(patient_id, period_end DESC)`; CHECK constraints on the 0..100 score columns.
  - `TransformationScoreConfiguration`: index on `(patient_id, week_number DESC)`; `detail` is `jsonb` with `DEFAULT '{}'::jsonb`.
- **Acceptance criteria**:
  - All 4 entities compile; all 4 configurations compile; `AppDbContext` exposes a `DbSet<>` for each.
  - Indexes and UNIQUE constraints from SPEC §13.1 are emitted (verifiable via `dotnet ef migrations script`).
  - `ScoreDimension` enum has exactly the 5 values the formula expects.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure` returns 0 errors.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-36 — Migration `AddProgramProgressScores` + weights seeder

- **Phase**: P1.5
- **Depends on**: T-35
- **Objective**: Produce a single EF migration that creates the 4 tables and their indexes, and extend `ProgramProgressSeeder` to UPSERT the 5 default `app.health_score_weights` rows.
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressScores.cs` (new, additive)
  - `src/CoppAddresd.Infrastructure/SeedData/ProgramProgressSeeder.cs` (extend)
- **Implementation notes**:
  - `dotnet ef migrations add AddProgramProgressScores --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api --output-dir Migrations`. Verify the migration is purely additive (no renames, no drops). It is intentionally a **separate migration** from `AddProgramProgressCore` so the schema history reads as a series of reviewable additive steps.
  - The seeder uses `ON CONFLICT (dimension) DO NOTHING` against `app.health_score_weights.dimension` so re-runs are idempotent. Defaults: `adherence=0.30, clinical=0.30, nutrition=0.20, psychology=0.10, exercise=0.10`.
  - The seeder **does not** seed `clinical_baselines`, `health_scores`, or `transformation_scores` — those are per-patient and created at runtime (baselines by clinicians, scores by the calculator on read).
- **Acceptance criteria**:
  - `dotnet ef database update` succeeds against a dev DB that already has `AddProgramProgressCore` applied.
  - `\d app.health_score_weights` shows the `dimension` UNIQUE constraint and the `numeric(5,4)` column.
  - After API restart, `SELECT COUNT(*) FROM app.health_score_weights` = 5 and `SELECT SUM(weight)` = `1.0000`.
- **Verification**:
  - `dotnet ef database update --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api`
  - `psql -c "SELECT dimension, weight FROM app.health_score_weights ORDER BY dimension;"`
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-37 — Score calculator services + repository support

- **Phase**: P1.5
- **Depends on**: T-36
- **Objective**: Implement `IHealthScoreCalculator` and `ITransformationScoreCalculator` plus the repository extensions that read baselines / measurements / checkins / habits / emotional records and persist score rows.
- **Affected paths**:
  - `src/CoppAddresd.Application/Interfaces/Services/IHealthScoreCalculator.cs` (new)
  - `src/CoppAddresd.Application/Services/ProgramProgress/HealthScoreCalculator.cs` (new)
  - `src/CoppAddresd.Application/Interfaces/Services/ITransformationScoreCalculator.cs` (new)
  - `src/CoppAddresd.Application/Services/ProgramProgress/TransformationScoreCalculator.cs` (new)
  - `src/CoppAddresd.Application/Interfaces/IProgramRepository.cs` (extend)
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (extend with `GetOrComputeHealthScoreAsync`, `GetOrComputeTransformationScoreAsync`, `ListClinicalBaselinesAsync`, `UpsertClinicalBaselineAsync`, `GetLatestMeasurementAsync(patientId, metricId, fromDate, toDate)`)
- **Implementation notes**:
  - The calculators are **pure functions** over the inputs the repository returns; only the repository touches EF. The Application layer's `WeightSumValidator` runs before any write to `app.health_score_weights` (AC-19; defends against misconfigured weights).
  - The repository `GetOrComputeHealthScoreAsync` checks `app.health_scores` by `UNIQUE (patient_id, period_start, period_end)`; if a row exists and is not stale (`period_end >= today_local`), returns the persisted row; otherwise computes, inserts a new row, and returns the new row with `score_previous` set from the previous row.
  - All read queries use `AsNoTracking`; writes inside `CreateExecutionStrategy().ExecuteAsync(...)` for consistency with the existing Wellness pattern.
  - Patient-local time conversion goes through `ICurrentContext` / `program_enrollments.timezone`. The repository never stores UTC and never trusts a raw `DateTime.UtcNow` for the period window.
  - `UpsertClinicalBaselineAsync` is the only write path; it enforces `set_by` is a clinician (AC-22) by checking the caller's role through `ICurrentContext` and rejecting with `403 FORBIDDEN` otherwise.
- **Acceptance criteria**:
  - Calculators are deterministic and side-effect free (testable in unit tests with stub repositories).
  - Repository extension methods return DTOs (no entity leak to Application).
  - `GetOrCompute*Async` inserts at most one row per `(patient_id, period_start, period_end)` (DB-enforced UNIQUE).
- **Verification**: `dotnet build src/CoppAddresd.Application src/CoppAddresd.Infrastructure` returns 0 errors.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-38 — Query / command handlers + API endpoints

- **Phase**: P1.5
- **Depends on**: T-37
- **Objective**: MediatR query for `GET /api/v1/program/scores` and command for `POST /api/v1/program/scores/calculate`, plus DTOs that match SPEC §13.7 exactly.
- **Affected paths**:
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/GetScores/GetScoresQuery.cs` + `Handler.cs` + `Validator.cs`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/CalculateScores/CalculateScoresCommand.cs` + `Handler.cs` + `Validator.cs`
  - `src/CoppAddresd.Application/Features/ProgramProgress/DTOs/Scores/{HealthScoreDto,TransformationScoreDto,IndicatorDetailDto,ScoresResponseDto}.cs` (new)
  - `src/CoppAddresd.Api/Controllers/ProgramController.cs` (extend with `GetScores` and `CalculateScores` actions)
- **Implementation notes**:
  - `[Authorize]` at the controller level is already in place. New actions: `[RequirePermission("Program.View")]` on `GetScores`; `[RequirePermission("Program.Edit")]` on `CalculateScores` (justified in SPEC §13.6 — no new permission code is added).
  - The query handler resolves the caller's `patient_id` via `ICurrentContext`; cross-patient reads return `404` (anti-IDOR, same as `WellnessController`). Clinician callers are scoped to their assigned patients via `app.patient_professionals` (the existing `Patients.ViewOwn` rule).
  - `CalculateScoresCommand` body: `{ PatientId, PeriodEndLocalDate? }`; `Validator` checks IANA / date format and rejects `periodEndLocalDate > today_local` with `422 INVALID_PERIOD`.
  - DTOs are the wire shape; controllers do not return entities. `X-Score-Stale: true` and `X-Score-Recalculated: true` headers are set from the handler's `ResponseHeaders` collection.
- **Acceptance criteria**:
  - All 5 DTOs compile; handlers compile; controller compiles.
  - `GET /api/v1/program/scores` with a valid patient JWT returns the JSON in SPEC §13.7.1.
  - `POST /api/v1/program/scores/calculate` with a clinician JWT and body returns the same shape with `X-Score-Recalculated: true`.
  - Cross-patient `GET` returns `404`; non-clinician `POST` returns `403`.
- **Verification**: `dotnet build src/CoppAddresd.Api` returns 0 errors; manual `curl` against a test JWT.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-39 — Tests (unit + integration + contract, AC-19..AC-22)

- **Phase**: P1.5
- **Depends on**: T-37, T-38
- **Objective**: Prove the engine: unit tests for each formula dimension, integration tests for the bands, contract test for the JSON shape.
- **Affected paths**:
  - `tests/CoppAddresd.UnitTests/ProgramProgress/Scores/HealthScoreCalculatorTests.cs` (new)
  - `tests/CoppAddresd.UnitTests/ProgramProgress/Scores/TransformationScoreCalculatorTests.cs` (new)
  - `tests/CoppAddresd.UnitTests/ProgramProgress/Scores/WeightSumValidatorTests.cs` (new)
  - `tests/CoppAddresd.UnitTests/ProgramProgress/Api/ScoresContractTests.cs` (new)
  - `tests/CoppAddresd.UnitTests/ProgramProgress/Scores/ClinicalBaselineAuthorizationTests.cs` (new)
- **Implementation notes**:
  - **Unit (formula)**: cover the bands in SPEC §13.4.1 (perfect / partial / freeze / missed) and §13.4.2 (5 bands including the no-data `clinical = 50` default), §13.4.3 (`nutrition = 0` when no logs), §13.4.4 (`psychology = 60` when no emotional records), §13.4.5 (single-day vs week). Use the same fakes-in-memory pattern as `tests/CoppAddresd.Telemedicine.UnitTests`.
  - **Unit (transformation)**: cover the 9 bands in §13.5 plus the `no indicators → score = 0, detail = {}` default; assert `detail` JSONB shape including `unit`, `delta`, `delta_pct`, `favorable`, `score`.
  - **Integration**: seed baselines / measurements / checkins / emotional records via fixtures; assert `GET /program/scores` returns the expected Health and Transformation Scores. Cover AC-19 (mixed 7-day checkins), AC-20 (3 baseline metrics with specific `|Δ%|`), AC-21 (no-data neutrals), AC-22 (patient cannot self-set a baseline).
  - **Contract**: assert the JSON keys in SPEC §13.7.1 exist (use `JsonElement` to avoid brittle deep-equality).
  - **Authorization**: assert `POST /program/scores/calculate` returns `403` for a patient JWT and `200` for a clinician JWT.
- **Acceptance criteria**:
  - 20+ tests pass (10 unit-formula, 5 unit-transformation, 2 contract, 1 weight-sum, 2 integration, 1 authorization, plus the 4 AC scenarios).
  - AC-19..AC-22 reproducible from `dotnet test` output.
- **Verification**: `dotnet test --filter "FullyQualifiedName~ProgramProgress.Scores"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-40 — Mobile: wire Evolución tab to `GET /program/scores`

- **Phase**: P1.5 (mobile integration; live in P2 cut if the mobile release is already tagged)
- **Depends on**: T-38 (endpoint live)
- **Objective**: Replace the static Health / Transformation Score mocks in the mobile Evolución tab with `GET /api/v1/program/scores`, with the existing mock as the fallback (demo continuity, same contract as snapshot fallback SPEC §9.2).
- **Affected paths** (mobile repo):
  - `antares-paciente/src/api/program.ts` (extend with `getScores()` and `calculateScores()`)
  - `antares-paciente/src/types/program.ts` (add `HealthScoreDto`, `TransformationScoreDto`, `IndicatorDetailDto`, `ScoresResponseDto`)
  - `antares-paciente/src/pages/ProgramPage.tsx` (Evolución tab)
  - `antares-paciente/src/context/AppContext.tsx` (extend `programSnapshot` with `scores` state)
- **Implementation notes**:
  - The mobile UI keeps the existing 5 pillars and 6 indicator rows visible. The mock values (Health 86, Transformation 87) are the **fallback**, not the source of truth. The fallback keeps the demo working when the API is unreachable (banner: "Sin conexión — mostrando datos del DD/MM", same wording as the snapshot fallback).
  - Labels are localized to Spanish per the existing convention: "Adherencia", "Evolución clínica", "Nutrición", "Bienestar psicológico", "Actividad física"; indicators: "Peso", "IMC", "% grasa", "Cintura", "Glucosa", "Adherencia".
  - The mobile never throws a fatal error on API failure. The `X-Score-Stale: true` header surfaces as a sub-line under the score ("Última actualización: DD/MM HH:MM").
- **Acceptance criteria**:
  - With the backend up, opening the Evolución tab shows real scores within 500ms p95 of the snapshot.
  - With the backend down, the mock fallback renders; no error to the user.
  - The 5 pillars and 6 indicators continue to be visible after the swap.
- **Verification**:
  - `npm run dev` and toggle the backend on/off; manual smoke.
  - `npm run build` returns 0 errors.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-41 — Docs / observability for the score engine

- **Phase**: P1.5
- **Depends on**: T-39 (tests stable)
- **Objective**: Extend the module docs (this file + the future `docs/modules/program-progress/README.md` in T-34) with the engine's endpoints, the period definition, the no-cron rationale, and the clinician-only baseline rule. Add structured logs for score recompute.
- **Affected paths**:
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (this PR — §13 already in place; no further change in this task)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (this PR — decisions 12..15 already in place; no further change in this task)
  - `src/CoppAddresd.Application/Services/ProgramProgress/HealthScoreCalculator.cs` (add `_logger.LogInformation("Program.ScoreComputed ...")` with patient_id, period, score, NO PHI)
  - `src/CoppAddresd.Application/Services/ProgramProgress/TransformationScoreCalculator.cs` (same pattern)
- **Implementation notes**:
  - Logs are JSON-formatted, no PII (no `mood_score`, no `barriers`, no `notes`). The log line carries `patient_id`, `period_start`, `period_end`, `score`, `dimensions`, and a `trigger` (`OnRead | ManualRecompute`).
  - No change is required to the existing `docs/modules/program-progress/SPEC.md` / `PLAN.md` files (the engine is already documented there in this PR). This task is the **implementation hook** for the structured logs and the future `README.md` addition (T-34 picks the README up).
- **Acceptance criteria**:
  - `_logger.LogInformation` calls compile; structured fields are emitted in the JSON log shape.
  - No PHI in the log payload (assert via grep in tests if practical).
- **Verification**: `dotnet build src/CoppAddresd.Application` returns 0 errors; `dotnet test` remains green.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-R — XP Rules catalog (P1.5)

> Data-driven XP rules catalog with anti-fraud limits (SPEC §14, decision 20).
> Sits after **B5-S** (same phase, P1.5) so the awarding path resolves the rules
> that the score engine and the rest of the module already assume. **Tests are
> OPTIONAL in this batch and are run manually by the user per the current
> workflow** (no automated `dotnet test` run; the batch gates on a green build +
> a generated migration only).

### T-42 — Entity, configuration, DbSet and migration `AddXpRulesCatalog`

- **Phase**: P1.5
- **Depends on**: T-02 (EF conventions), T-03 (migration flow)
- **Objective**: Add `XpRule` entity + `XpRuleConfiguration` + `DbSet<XpRule>` + the additive migration that creates `app.xp_rules` and adds `xp_ledger.rule_code`.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/XpRule.cs` (new)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/XpRuleCodes.cs` (new; shared seed/resolution codes)
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/XpLedgerEntry.cs` (add `RuleCode`)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/XpRuleConfiguration.cs` (new)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/XpLedgerEntryConfiguration.cs` (add `rule_code` + FK by `code` via `HasPrincipalKey`)
  - `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs` (add `DbSet<XpRule>`)
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddXpRulesCatalog.cs` (new, additive)
- **Implementation notes**:
  - Entity: `code varchar(60)` UNIQUE, `name varchar(120)`, `category varchar(40)`, `base_xp int?` (NULL = defer to template points), `multiplier numeric(4,2)` default 1.00, `max_per_day`/`max_per_week` nullable, `requires_validation`/`active` bools, `valid_from date` default `CURRENT_DATE`, `valid_until date?`, `created_at`/`updated_at`.
  - Configuration mirrors the module style (`ToTable("xp_rules","app")`, snake_case, CHECK constraints that mirror the API validation: `multiplier > 0`, `base_xp >= 0`, limits `>= 0`); indexes `uq_xp_rules_code` UNIQUE, `ix_xp_rules_category`, `ix_xp_rules_active`.
  - `xp_ledger.rule_code` is nullable `varchar(60)` with FK → `app.xp_rules.code` via `HasPrincipalKey(x => x.Code)` and `OnDelete(Restrict)` (clean option: EF generates the alternate key `AK_xp_rules_code` automatically; the column is backward compatible, all pre-existing rows stay NULL).
  - The migration attaches the `xp_rules` audit trigger (`audit.attach_table_audit`) — module convention §8.5.
- **Acceptance criteria**:
  - Domain + Infrastructure compile; `AppDbContext` exposes `DbSet<XpRule>`.
  - `AddXpRulesCatalog` is purely additive (no renames/drops) and reversible.
  - `\d app.xp_rules` shows the UNIQUE `code`, the CHECKs and the `CURRENT_DATE` default; `app.xp_ledger.rule_code` is nullable with the FK.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure` returns 0 errors; migration generated (NOT applied).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-43 — Award-path wiring + anti-fraud limits

- **Phase**: P1.5
- **Depends on**: T-42 (entity + migration)
- **Objective**: Resolve the catalog rule before every XP award in `ProgramRepository.CompleteTaskCoreAsync` (task completion + day bonus), apply precedence/multiplier and enforce `max_per_day`/`max_per_week`.
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (add `ResolveActiveRuleAsync` + `ResolveXpAwardAsync`; wire into steps 8 and 9 of `CompleteTaskCoreAsync`)
- **Implementation notes**:
  - **Precedence** (SPEC §14.3): rule `active` AND `valid_from <= today(UTC) <= valid_until` → `points = rule.base_xp ?? <existing source>` (for `TASK_*` the template `weekly_day_templates.points`; for `DAY_BONUS` the rule's 50), `total = floor(base × multiplier)`.
  - **Limits**: count `app.xp_ledger` rows for `(enrollment_id, rule_code)` in the patient-local day/week window (local date → UTC range, DST-aware via the existing `ResolveTimeZone`/`LocalDateToUtcStart` helpers); exceeded → `BusinessRuleViolationException` (`XP_DAILY_LIMIT_REACHED` / `XP_WEEKLY_LIMIT_REACHED`, both → 409 via `ExceptionHandlingMiddleware`).
  - **Fallback** (document in a comment): no rule / inactive / expired → current behavior (template points, no limits, `rule_code = NULL`). Replay body must stay byte-identical to the first write (`PointsAwarded` and `checkin.TotalPoints`/`BonusAwarded` reflect the resolved points).
  - The resolution runs inside the existing `FOR UPDATE` transaction (no new locking); pre-catalog rows (NULL `rule_code`) never count toward limits.
  - Streak milestones: the current implementation does NOT award XP at milestones, so the `STREAK_*` rules are seeded as data-driven config for the ERP/future awards but have no awarding call site yet (documented in SPEC §14.2).
- **Acceptance criteria**:
  - `dotnet build src/CoppAddresd.Infrastructure` returns 0 errors.
  - AC-23 reproducible manually: second same-day award under a `max_per_day = 1` rule → `409 XP_DAILY_LIMIT_REACHED`; awarded rows carry `rule_code`.
  - AC-24 reproducible manually: deactivating/expiring a rule falls back to template points with `rule_code = NULL`, history untouched.
- **Verification**: `dotnet build src/CoppAddresd.Infrastructure`; manual scenario per the current workflow (no automated tests in this batch).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-44 — Admin endpoints `GET/PUT /api/v1/program/xp-rules`

- **Phase**: P1.5
- **Depends on**: T-43
- **Objective**: MediatR query + command + validators + repository methods + controller actions for the catalog, with prospective-only edit semantics.
- **Affected paths**:
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListXpRules/ListXpRulesQuery.cs` (new)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/UpdateXpRule/UpdateXpRuleCommand.cs` (new; command + validator)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/UpdateXpRule/UpdateXpRuleCommandHandler.cs` (new)
  - `src/CoppAddresd.Application/DTOs/ProgramProgress/ProgramProgressDtos.cs` (add `XpRuleDto`)
  - `src/CoppAddresd.Application/Interfaces/IProgramRepository.cs` (add `ListXpRulesAsync`, `GetXpRuleByCodeAsync`, `UpdateXpRuleAsync`)
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (implement the 3 methods)
  - `src/CoppAddresd.Api/Controllers/ProgramController.cs` (add `GET/PUT xp-rules`)
- **Implementation notes**:
  - `GET /api/v1/program/xp-rules` and `PUT /api/v1/program/xp-rules/{code}` both `[RequirePermission("Program.Edit")]` (existing permission code; no new one — same justification as §13.6).
  - Editable fields (prospective only, SPEC §14.4): `base_xp`, `multiplier`, `max_per_day`, `max_per_week`, `requires_validation`, `active`, `valid_until`. Identity (`code`, `name`, `category`, `valid_from`) is immutable via this endpoint.
  - Validator: `multiplier > 0`, `base_xp >= 0`, `max_per_day >= 0`, `max_per_week >= 0`; handler rejects `valid_until < valid_from` with `422 INVALID_VALIDITY_WINDOW`.
  - Update persists with `dbContext.XpRules.Update(...)` + `SaveChanges` (entity is loaded `AsNoTracking`); `updated_at`/`updated_by` set on write.
- **Acceptance criteria**:
  - All touched projects compile (0 errors).
  - `GET /xp-rules` with a `Program.Edit` JWT returns the 11 seeded rules (after seeder run) in the §14.5.1 shape.
  - `PUT /xp-rules/{code}` with invalid values → 400; unknown code → 404; valid edit → 200 with the updated rule; `xp_ledger` history untouched.
- **Verification**: `dotnet build src/CoppAddresd.Api` returns 0 errors (temp OutputPath if a dev process locks the bin); manual `curl` per the current workflow.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-45 — Seeder extension + module docs (SPEC §14, PLAN, TASKS)

- **Phase**: P1.5
- **Depends on**: T-42, T-44
- **Objective**: Seed the 11 default rules idempotently and keep PLAN/SPEC/TASKS consistent with the catalog.
- **Affected paths**:
  - `src/CoppAddresd.Api/Seeders/ProgramProgressSeeder.cs` (add `SeedXpRulesAsync`, UPSERT by `code`)
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (this PR — §14 already in place; no further change in this task)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (this PR — decision 16 + phasing/risk rows already in place)
  - `coppAddresdBack/docs/modules/program-progress/TASKS.md` (this PR — this batch)
- **Implementation notes**:
  - `ON CONFLICT (code) DO NOTHING` semantics (same as the weights seeder): a rule that already exists is never overwritten, so admin edits survive re-runs.
  - Seed values (SPEC §14.2): 6× `TASK_*` (base NULL, 1/7), `DAY_BONUS` (50, 1/7), `STREAK_7/11/22/50` (100/200/500/1500, 1/1). `STREAK_*` values are consistent with the milestone engine of "Paso 4" (SPEC §16, B5-M), which awards them once per enrollment.
  - Logs: `Regla de XP sembrada: {Code} (categoría {Category}, base {BaseXp})` — no PHI.
- **Acceptance criteria**:
  - After API restart, `SELECT COUNT(*) FROM app.xp_rules` = 11; re-running the seeder does not duplicate and does not overwrite edits.
  - Docs agree: table names, endpoint paths, AC numbers, batch ID (`B5-R`, `T-42..T-45`) consistent across PLAN/SPEC/TASKS.
- **Verification**: `dotnet build src/CoppAddresd.Api` returns 0 errors; docs cross-check (grep for `xp_rules`, `xp-rules`, `B5-R`, `AC-24`).
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-C — Clinical XP with professional validation (P1.5)

> "Paso 3" del módulo: la evolución clínica real (línea base vs medición) se
> convierte en XP gamificada SOLO a través de reglas `clinical` del catálogo
> (§15) y, para mejorías significativas, de una **revisión profesional
> obligatoria** (`app.clinical_xp_reviews`). Se dispara únicamente en
> `POST /scores/calculate`; `GET /scores` nunca otorga XP. **Tests OPTIONALES /
> manuales per el workflow actual** (sin `dotnet test` automático; gate = build
> verde + migración generada sin aplicar).

### T-46 — Schema: entidad `ClinicalXpReview` + columnas `validated_by`/`validated_at` + migración `AddProgramProgressClinicalXp`

- **Phase**: P1.5
- **Depends on**: T-42 (xp_rules + rule_code en xp_ledger), T-35 (health_scores)
- **Objective**: Entidad + enum de estado + configuración EF + `DbSet` + columna `rule_code` por defecto, y la migración aditiva que crea `app.clinical_xp_reviews` y agrega `validated_by`/`validated_at` a `xp_ledger`.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/ClinicalXpReview.cs` (nuevo)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/ClinicalXpReviewStatus.cs` (nuevo; `pending|approved|rejected`, persistido en minúscula)
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/XpLedgerEntry.cs` (agregar `ValidatedBy`, `ValidatedAt`, navegación `Rule`)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/XpRuleCodes.cs` + `XpReason.cs` (agregar `CLINICAL_*`)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/ClinicalXpReviewConfiguration.cs` (nuevo)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/XpLedgerEntryConfiguration.cs` (extender)
  - `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs` (agregar `DbSet<ClinicalXpReview>`)
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressClinicalXp.cs` (nueva, aditiva)
- **Implementation notes**:
  - `clinical_xp_reviews`: UNIQUE `(patient_id, health_score_id, metric_id)`, índices `(status)` y `(decided_by)`, `rule_code varchar(60) NOT NULL DEFAULT 'CLINICAL_SIGNIFICANT'`, `status varchar(20) DEFAULT 'pending'` con CHECK, `delta_pct numeric(8,3)`. FKs `patient_profiles`/`health_scores`/`measurement_metrics` RESTRICT.
  - `xp_ledger.validated_by`/`validated_at` nullable; las FKs a `auth."Users"` se crean por SQL en la migración con `ON DELETE SET NULL` (patrón de `AddProgramProgressCore`), junto con el trigger de auditoría de `clinical_xp_reviews` (§8.5) y los GRANTs a `app_user`.
  - La navegación `XpLedgerEntry.Rule` habilita la exclusión de pendientes en las proyecciones de balance (§15.5).
- **Acceptance criteria**:
  - Domain + Infrastructure compilan (0 errores).
  - `AddProgramProgressClinicalXp` es aditiva y reversible; `\d app.clinical_xp_reviews` muestra el UNIQUE `(patient_id, health_score_id, metric_id)` y el default de `rule_code`; `app.xp_ledger` tiene `validated_by`/`validated_at` con FK a `auth."Users"`.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure`; migración generada (NO aplicada).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-47 — Motor de otorgamiento clínico en `POST /scores/calculate`

- **Phase**: P1.5
- **Depends on**: T-46 (tabla + columnas), T-43 (resolución de reglas), T-37 (calculadores/ventana del período)
- **Objective**: `EvaluateClinicalXpAwardsAsync` (repositorio) + hook en `CalculateScoresCommandHandler`: tras persistir la fila de `health_scores`, clasificar cada línea base con medición del período (favorable + umbral, favorable 1%..umbral, estable, desfavorable, all-up) y producir revisiones `pending` / auto-otorgamientos idempotentes.
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`EvaluateClinicalXpAwardsAsync`, `AwardClinicalXpAsync`, helpers `LoadClinicalPeriodIndicatorsAsync`)
  - `src/CoppAddresd.Application/Interfaces/IProgramRepository.cs` (extender)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/CalculateScores/CalculateScoresCommand.cs` (handler: llamar a la evaluación + log sin PHI)
  - `src/CoppAddresd.Application/Features/ProgramProgress/DTOs/ClinicalXp/ClinicalXpEvaluationResult.cs` (nuevo)
- **Implementation notes**:
  - Umbral de significancia configurable `Program:ClinicalXp:SignificantThresholdPct` (default 5). El motor NUNCA penaliza: métrica desfavorable → 0 XP, sin racha ni castigo (AC-27).
  - Idempotencia por el dedupe parcial `(source_ref_type='clinical_period', source_ref_id=health_scores.id, reason)`: un otorgamiento por regla y período; violación única (carrera) → se omite.
  - El `GET /scores` NO llama al motor (solo computa y expone la cola pendiente).
- **Acceptance criteria**: Application + Infrastructure compilan; AC-25/AC-27 reproducibles manualmente (una sola evaluación por período, sin doble XP).
- **Verification**: `dotnet build src/CoppAddresd.Application src/CoppAddresd.Infrastructure`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-48 — Flujo de validación profesional: query + decide + exclusión de pendientes en totales

- **Phase**: P1.5
- **Depends on**: T-47
- **Objective**: `GET /api/v1/program/xp-rules/clinical-pending` (cola paginada) y `POST /api/v1/program/xp-rules/clinical-pending/{id}/decide` (aprobar → otorga `CLINICAL_SIGNIFICANT` validado; rechazar → sin XP; ya decidida → 409), con guardia de rol clínico (AC-22). Excluir de los totales de XP las filas `requires_validation` sin `validated_by`.
- **Affected paths**:
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListClinicalReviews/ListClinicalReviewsQuery.cs` (nuevo)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/DecideClinicalReview/DecideClinicalReviewCommand.cs` (nuevo; command + validator + handler)
  - `src/CoppAddresd.Application/Features/ProgramProgress/DTOs/ClinicalXp/ClinicalReviewDto.cs` (nuevo)
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`ListPendingClinicalReviewsAsync`, `DecideClinicalXpReviewAsync` + filtro de pendientes en las proyecciones de balance de `GetEnrollmentAsync`/`ListEnrollmentsAsync`/`GetSnapshotAsync`)
  - `src/CoppAddresd.Api/Context/ProgramActorContext.cs` (exponer `Roles` del JWT)
  - `src/CoppAddresd.Api/Controllers/ProgramController.cs` (dos acciones bajo `/xp-rules/clinical-pending`)
- **Implementation notes**:
  - Ambas acciones `[RequirePermission("Program.Adapt")]` (código existente, sin permiso nuevo). La decisión exige rol clínico (`Physician`/`Nutritionist`/`Psychologist`/`ClinicalDirector`/`Admin`) → 403; `REVIEW_ALREADY_DECIDED` → 409 vía `BusinessRuleViolationException`.
  - Aprobada: `xp_ledger` con `validated_by = clínico`, `validated_at = now` (cuenta en totales desde la aprobación). Rechazada: sin XP. El aprobado de un período ya otorgado marca la revisión pero no duplica XP.
  - Los totales filtran `ValidatedBy != null OR Rule == null OR !Rule.RequiresValidation` (§15.5): las `CLINICAL_IMPROVE/STABLE/ALL_UP` tienen `validated_by` null pero `requires_validation = false` → cuentan.
- **Acceptance criteria**: Api compila; AC-26 reproducible manualmente (cola, aprobar/rechazar, 409 en segunda decisión, totales sin la XP pendiente antes de aprobar).
- **Verification**: `dotnet build src/CoppAddresd.Api` (temp OutputPath si un proceso de dev bloquea el bin); manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-49 — Seeder de las 4 reglas clínicas + docs (SPEC §15, PLAN, TASKS)

- **Phase**: P1.5
- **Depends on**: T-46, T-48
- **Objective**: Seed idempotente de `CLINICAL_IMPROVE` (50), `CLINICAL_SIGNIFICANT` (100, `requires_validation = true`), `CLINICAL_STABLE` (20) y `CLINICAL_WEEKLY_ALL_UP` (150) — categoría `clinical`, sin topes por día/semana — y mantener PLAN/SPEC/TASKS consistentes con el "Paso 3".
- **Affected paths**:
  - `src/CoppAddresd.Api/Seeders/ProgramProgressSeeder.cs` (extender `SeedXpRulesAsync`; descriptor con `RequiresValidation`)
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (§15 + decisión 21 + AC-25..AC-27 + listas de §8.4/§8.5/§14.2)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (decisión 17 + phasing + riesgo)
  - `coppAddresdBack/docs/modules/program-progress/TASKS.md` (este batch)
- **Implementation notes**: `ON CONFLICT (code) DO NOTHING` (una regla existente nunca se pisa). Logs: `Regla de XP sembrada: {Code} ... validación {RequiresValidation}` — sin PHI.
- **Acceptance criteria**: Tras el restart, `SELECT COUNT(*) FROM app.xp_rules WHERE category = 'clinical'` = 4; re-correr el seeder no duplica ni pisa ediciones. Docs consistentes (`clinical_xp_reviews`, `clinical-pending`, `B5-C`, `T-46..T-49`, `AC-27`).
- **Verification**: `dotnet build src/CoppAddresd.Api`; docs cross-check (grep de `clinical_xp_reviews`, `CLINICAL_SIGNIFICANT`, `B5-C`).
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-M — Streak multiplier x2 (P1.5, "Paso 4")

> Multiplicador x2 por hito de racha (SPEC §16, decisión 22): los hitos de racha
> (7/11/22/50 días) otorgan su XP del catálogo **una vez por inscripción** y los
> hitos 11/22/50 **activan un multiplicador x2 del paciente** (24h/48h/72h) que
> aplica a TODA la XP mientras está vigente. **Tests OPTIONALES / manuales per el
> workflow actual** (sin `dotnet test` automático; gate = build verde + migración
> generada sin aplicar).

### T-50 — Schema: entidad `StreakState`/`XpLedgerEntry` + razones `STREAK_*` + configuración EF + migración `AddProgramProgressMultiplier`

- **Phase**: P1.5
- **Depends on**: T-42 (xp_rules + `rule_code` en `xp_ledger`), T-46 (patrón de columnas en `xp_ledger`)
- **Objective**: Columnas `multiplier_active`/`multiplier_ends_at` en `app.streak_states` y `multiplier_used` en `app.xp_ledger` + entidades/configuraciones + 4 miembros `STREAK_*` en `XpReason` + migración aditiva.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/StreakState.cs` (`MultiplierActive` `decimal(4,2)` default 1.0, `MultiplierEndsAt` `DateTime?`)
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/XpLedgerEntry.cs` (`MultiplierUsed` `decimal?`)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/XpReason.cs` (`STREAK_7/11/22/50`, nombrados igual que sus códigos de regla — precedente `CLINICAL_*`)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/StreakStateConfiguration.cs` (columnas + CHECK `ck_streak_states_multiplier_active_positive` `multiplier_active >= 1.0`)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/XpLedgerEntryConfiguration.cs` (`multiplier_used` `numeric(4,2)` nullable)
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressMultiplier.cs` (nueva, aditiva y reversible)
- **Implementation notes**:
  - Migración solo `ADD COLUMN` (metadata-only en PG 11+, sin backfill): `multiplier_active numeric(4,2) NOT NULL DEFAULT 1.0` (default constante → rápido), `multiplier_ends_at timestamptz NULL`, `multiplier_used numeric(4,2) NULL`.
  - El CHECK `multiplier_active >= 1.0` espeja la semántica (1.0 = sin multiplicador).
- **Acceptance criteria**:
  - Domain + Infrastructure compilan (0 errores).
  - `AddProgramProgressMultiplier` es aditiva y reversible; `\d app.streak_states` muestra `multiplier_active` (default 1.0 + CHECK) y `multiplier_ends_at`; `app.xp_ledger` tiene `multiplier_used` nullable.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure`; migración generada (NO aplicada).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-51 — Motor de hitos: otorgamiento único + activación del multiplicador

- **Phase**: P1.5
- **Depends on**: T-50 (columnas), T-43 (`ResolveXpAwardAsync`)
- **Objective**: En el camino de racha (`UpdateStreakAsync` → hook en `CompleteTaskCoreAsync`), al crecer la racha hoy hasta un día hito: otorgar la XP del hito UNA vez (`source_ref_type='streak_milestone'`, `source_ref_id = streak_states.enrollment_id`, `reason='STREAK_{days}'`, dedupe parcial) y activar el x2 (11/22/50 → `multiplier_active=2.0`, `multiplier_ends_at = now + 24h/48h/72h`, sobrescribe el vigente). Guardia compartida: hito repetido no re-otorga ni re-activa.
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`AwardStreakMilestoneIfReachedAsync`, tabla estática `StreakMilestones`, hook tras el flush del día perfecto en `CompleteTaskCoreAsync`, `UpdateStreakAsync` devuelve `(Current, GrewToday)`)
- **Implementation notes**:
  - La activación corre ANTES del otorgamiento: la XP del hito se otorga con el x2 recién activado (SPEC §16, C.4).
  - El hook corre después del `SaveChanges` del día perfecto para que `balance_after` incluya el bonus del día.
- **Acceptance criteria**: Infrastructure compila; AC-28 reproducible manualmente (día 11 → `STREAK_11` + x2 24h una sola vez; día 7 → `STREAK_7` sin multiplicador; racha reconstruida → sin re-otorgamiento ni re-activación).
- **Verification**: `dotnet build src/CoppAddresd.Infrastructure`; manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-52 — Multiplicador del paciente en el camino de otorgamiento + snapshot

- **Phase**: P1.5
- **Depends on**: T-51 (activación), T-43 (resolución de reglas)
- **Objective**: Aplicar el multiplicador del paciente a TODOS los otorgamientos (`ResolveXpAwardAsync` + `AwardClinicalXpAsync`): vigente → `total = floor(base × rule.Multiplier × patient_multiplier)` y `multiplier_used` en el libro mayor; vencido/null → 1.0 con reset lazy (`ExecuteUpdate`). Exponer `multiplierActive`/`multiplierEndsAt`/`multiplierRemainingHours` en el snapshot.
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`ResolvePatientMultiplierAsync`; `ResolveXpAwardAsync` devuelve `(Points, RuleCode, MultiplierUsed)`; `AwardClinicalXpAsync`; proyección `GetSnapshotAsync`)
  - `src/CoppAddresd.Application/DTOs/ProgramProgress/ProgramProgressDtos.cs` (`StreakInfoDto` extendido con `MultiplierActive`/`MultiplierEndsAt`/`MultiplierRemainingHours`)
- **Implementation notes**:
  - `multiplierRemainingHours` = `floor((ends_at − now).TotalHours)`; el snapshot NUNCA escribe (el reset lazy ocurre en el próximo otorgamiento).
  - Shape aditivo: los campos existentes del snapshot no cambian.
- **Acceptance criteria**: Application + Infrastructure compilan; AC-29/AC-30 reproducibles manualmente (ventana x2 → filas con `multiplier_used`; vencido → 1.0 con reset lazy y snapshot en 1.0/null/0).
- **Verification**: `dotnet build src/CoppAddresd.Api` (temp OutputPath si un proceso de dev bloquea el bin); manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-53 — Docs (SPEC §16, PLAN, TASKS)

- **Phase**: P1.5
- **Depends on**: T-50, T-52
- **Objective**: Mantener PLAN/SPEC/TASKS consistentes con el "Paso 4": decisión 22 + §16 + AC-28..AC-30 + filas de phasing/riesgo.
- **Affected paths**:
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (§16 con tabla de hitos 7/11/22/50, fórmula `total = floor(base × rule × patient)`, reset lazy, "aplica a toda la XP", campos del snapshot, AC-28..AC-30; §0 decisión 22; §3.7/§3.8; §14.2 nota de consistencia)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (decisión 18 + phasing + riesgo)
  - `coppAddresdBack/docs/modules/program-progress/TASKS.md` (este batch)
- **Implementation notes**: Sin cambios de seeder (las 4 reglas `STREAK_*` ya existen en §14.2; no se agrega `STREAK_90`).
- **Acceptance criteria**: Docs consistentes (`multiplier_active`, `multiplier_used`, `B5-M`, `T-50..T-53`, `AC-30`, `AddProgramProgressMultiplier`).
- **Verification**: `dotnet build src/CoppAddresd.Api`; docs cross-check (grep de `multiplier_`, `B5-M`, `AddProgramProgressMultiplier`).
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-T — Streak threshold & essentials (P1.5, "Paso 5")

> Umbral de racha configurable por plantilla (SPEC §17, decisión 23): un día
> mantiene la racha si completó `streak_min_tasks` tareas (default 1) y el
> rescate con congelamiento exige al menos una tarea esencial el día perdido
> (`essential_task_codes`, referencia ADRED). El día perfecto y la cadencia de
> concesión de congelamientos no cambian. **Tests OPTIONALES / manuales per el
> workflow actual** (sin `dotnet test` automático; gate = build verde +
> migración generada sin aplicar).

### T-54 — Schema: `ProgramTemplate` + configuración EF + migración `AddProgramProgressStreakConfig`

- **Phase**: P1.5
- **Depends on**: T-02 (EF conventions), T-03 (migration flow)
- **Objective**: Columnas `streak_min_tasks` (SMALLINT NOT NULL default 1, CHECK `>= 1`) y `essential_task_codes` (jsonb NOT NULL default `'[]'`) en `app.program_templates` + entidad + configuración EF + migración aditiva.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/ProgramTemplate.cs` (`StreakMinTasks` `short` default 1, `EssentialTaskCodes` `List<string>` default `[]`)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/ProgramTemplateConfiguration.cs` (columnas + CHECK + jsonb con value converter `System.Text.Json`)
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressStreakConfig.cs` (nueva, aditiva y reversible)
- **Implementation notes**: Migración solo `ADD COLUMN` con defaults constantes (metadata-only en PG 11+, sin backfill): `streak_min_tasks smallint NOT NULL DEFAULT 1`, `essential_task_codes jsonb NOT NULL DEFAULT '[]'::jsonb` + CHECK. Backward compatible: las plantillas existentes quedan con `essential_task_codes = '[]'` (lista vacía = sin restricción, SPEC §17.1).
- **Acceptance criteria**: Domain + Infrastructure compilan (0 errores); `AddProgramProgressStreakConfig` es aditiva y reversible; `\d app.program_templates` muestra las 2 columnas con sus defaults + CHECK.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure`; migración generada (NO aplicada).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-55 — Mantenimiento de racha por umbral configurable

- **Phase**: P1.5
- **Depends on**: T-54 (columnas), T-07 (`CompleteTaskCoreAsync`/`UpdateStreakAsync`)
- **Objective**: En `CompleteTaskCoreAsync`, evaluar el umbral con la plantilla de la inscripción: `meetsThreshold = tasks_done >= template.StreakMinTasks` (default 1 → comportamiento previo); el día perfecto sigue siendo "todas las tareas" (bonus + concesión de congelamientos, sin cambios).
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (gate del camino de racha en `CompleteTaskCoreAsync`; `UpdateStreakAsync` recibe `essentialTaskCodes`)
- **Implementation notes**: `tasks_done` = conteo de `task_completions` de la fecha local (incluye la completación en curso, misma query que `isPerfect`). Re-ejecutar el mismo día es no-op (guardia `last_active_date == hoy` ya existente). El motor de hitos (SPEC §16) queda intacto: sigue dependiendo de `streakGrewToday`.
- **Acceptance criteria**: Infrastructure compila; AC-31 reproducible manualmente (umbral 3 → día de 2 tareas no mantiene la racha; con congelamiento + 1 esencial → consumido y racha preservada).
- **Verification**: `dotnet build src/CoppAddresd.Infrastructure`; manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-56 — Rescate con congelamiento solo con tarea esencial + snapshot

- **Phase**: P1.5
- **Depends on**: T-55 (umbral), T-54 (códigos esenciales)
- **Objective**: Regla "el rescate requiere una tarea esencial" (SPEC §17, C, referencia ADRED): un día bajo umbral solo se rescata con congelamiento si completó ≥1 tarea en `essential_task_codes`; sin esencial → la racha se rompe y el congelamiento NO se consume (AC-32). Exponer `streakMinTasks`/`essentialTaskCodes` en el snapshot (aditivo).
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`HadEssentialTaskAsync`; rama de hueco de `UpdateStreakAsync`; proyección `GetSnapshotAsync`)
  - `src/CoppAddresd.Application/DTOs/ProgramProgress/ProgramProgressDtos.cs` (`ProgramSnapshotTemplateDto` + `StreakMinTasks`/`EssentialTaskCodes`)
- **Implementation notes**: Lista esencial vacía → sin restricción (compatibilidad previa, SPEC §17.1). El consumo se registra con el camino `Consumed` existente; la cadencia de concesión (1 por 7 perfectos, tope 3) no cambia. Shape aditivo: los campos existentes del snapshot no cambian.
- **Acceptance criteria**: Application + Infrastructure compilan; AC-32 reproducible manualmente (congelamiento NO consumido, racha rota, inventario intacto); snapshot expone los 2 campos nuevos.
- **Verification**: `dotnet build src/CoppAddresd.Api` (temp OutputPath si un proceso de dev bloquea el bin); manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-57 — Seeder de configuración + docs (SPEC §17, PLAN, TASKS)

- **Phase**: P1.5
- **Depends on**: T-54, T-56
- **Objective**: Seed de la configuración de racha del template `default-83w` (solo en create) y mantener PLAN/SPEC/TASKS consistentes con el "Paso 5".
- **Affected paths**:
  - `src/CoppAddresd.Api/Seeders/ProgramProgressSeeder.cs` (`StreakMinTasks = 1`, `EssentialTaskCodes = ["nut","ejercicio","nutribiotico"]` en el create del template)
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (§17 + decisión 23 + AC-09 actualizado + AC-31/AC-32 + §3.1/§6.6/§7.1/§8.4)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (decisión 19 + phasing + riesgo)
  - `coppAddresdBack/docs/modules/program-progress/TASKS.md` (este batch)
- **Implementation notes**: El seed solo fija la configuración al crear el template (idempotente por `code`): si la plantilla ya existe, no se toca su configuración. Logs: sin PHI (los códigos de tarea no son datos de paciente).
- **Acceptance criteria**: Tras el restart (template nuevo), `SELECT streak_min_tasks, essential_task_codes FROM app.program_templates WHERE code = 'default-83w'` = `1` y `["nut","ejercicio","nutribiotico"]`; re-correr el seeder no pisa config de un template existente. Docs consistentes (`streak_min_tasks`, `essential_task_codes`, `B5-T`, `T-54..T-57`, `AC-32`, `AddProgramProgressStreakConfig`).
- **Verification**: `dotnet build src/CoppAddresd.Api`; docs cross-check (grep de `streak_min_tasks`, `B5-T`, `AddProgramProgressStreakConfig`).
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-N — Granular nutrition XP (P1.5, "Paso 6")

> Nutrición granular por comida + hidratación (SPEC §18, decisión 24): la
> NutritionPage del móvil (4 comidas `des`/`alm`/`mer`/`cen` + hidratación
> `agua`) cierra su bucle con `POST /api/v1/program/nutrition/log` — XP por
> registro **ADITIVA** a la tarea `nut` existente (el flujo de `CompleteTask`
> queda intacto) — y los premios semanales de adherencia
> (`NUTRITION_WEEK_85` ≥85% y `NUTRITION_RECOVERY` +20pp) se disparan SOLO en
> `POST /scores/calculate` (mismo patrón que la XP clínica). **Tests
> OPTIONALES / manuales per el workflow actual** (sin `dotnet test`
> automático; gate = build verde + migración generada sin aplicar).

### T-58 — Schema: entidades `HabitTemplate`/`HabitCheck` + enums `MealCode`/`XpReason`/`XpRuleCodes` + configuraciones EF + `DbSet` + migración `AddProgramProgressNutritionXp` + fix del fake de tests

- **Phase**: P1.5
- **Depends on**: T-42 (xp_rules + `rule_code`), T-02 (EF conventions), T-03 (migration flow)
- **Objective**: Entidades de hábito de alimentación/hidratación + enum `MealCode` (`des`/`alm`/`mer`/`cen`/`agua`) + 4 miembros `NUTRITION_*` en `XpReason`/`XpRuleCodes` + configuraciones EF + `DbSet` en `AppDbContext` + la migración aditiva que crea `app.habit_templates` y `app.habit_checks` (único por `(patient_id, habit_template_id, local_date)`, auditoría + GRANTs). **Incluye el fix de compilación del proyecto de tests**: `FakeProgramRepository` no implementaba los 3 miembros del paso 3 (`EvaluateClinicalXpAwardsAsync`, `ListPendingClinicalReviewsAsync`, `DecideClinicalXpReviewAsync`) ni los 2 nuevos de este batch (`LogNutritionAsync`, `EvaluateNutritionAwardsAsync`); se agregan con el estilo fake existente (listas vacías / resultados configurables). También se alinean los constructores de DTO del snapshot en `ProgramQueryHandlerTests` (campos `StreakMinTasks`/`EssentialTaskCodes`/`MultiplierActive` que los pasos 4-5 agregaron y el test no reflejaba — pre-existing break que dejaba el proyecto de tests sin compilar).
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/HabitTemplate.cs` (nuevo)
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/HabitCheck.cs` (nuevo)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/MealCode.cs` (nuevo)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/XpReason.cs` + `XpRuleCodes.cs` (4 miembros `NUTRITION_*`)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/HabitTemplateConfiguration.cs` + `HabitCheckConfiguration.cs` (nuevos)
  - `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs` (2 `DbSet<>`)
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressNutritionXp.cs` (nueva, aditiva)
  - `tests/CoppAddresd.UnitTests/ProgramProgress/Handlers/FakeProgramRepository.cs` (fix de compilación)
  - `tests/CoppAddresd.UnitTests/ProgramProgress/Handlers/ProgramQueryHandlerTests.cs` (fix de constructores DTO, pre-existing)
- **Implementation notes**:
  - `habit_templates`: `code varchar(20)` UNIQUE (`des`/`alm`/`mer`/`cen`/`agua`), `name`, `category varchar(40)` (`alimentacion`/`agua`), `sort_order`. `habit_checks`: `patient_id`, `habit_template_id`, `local_date`, `is_done bool default true` + UNIQUE `(patient_id, habit_template_id, local_date)` — MISMO contrato de columnas que consume la dimensión de nutrición del Health Score (`GetNutritionLogAsync`, SPEC §13.4.3).
  - La migración adjunta los triggers de auditoría (`audit.attach_table_audit`) de las 2 tablas (sin PHI, precedente `clinical_xp_reviews`) y los GRANTs a `app_user` — patrón de `AddProgramProgressClinicalXp`.
  - El fake: los métodos clínicos devuelven `ClinicalXpEvaluationResult.Empty` / lista configurable; los de nutrición devuelven resultados configurables (patrón fakes-in-memory, sin BD).
- **Acceptance criteria**:
  - Domain + Infrastructure + tests compilan (0 errores; el proyecto de tests vuelve a compilar con el fix del fake + los constructores).
  - `AddProgramProgressNutritionXp` es aditiva y reversible; `\d app.habit_checks` muestra el UNIQUE por tripleta y el `is_done` default; las 2 tablas tienen trigger de auditoría.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure tests/CoppAddresd.UnitTests` (temp OutputPath si un proceso de dev bloquea el bin); migración generada (NO aplicada).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-59 — Endpoint `POST /api/v1/program/nutrition/log` (comando + repositorio + controller)

- **Phase**: P1.5
- **Depends on**: T-58 (schema), T-43 (`ResolveXpAwardAsync`)
- **Objective**: Comando MediatR `LogNutritionCommand` + `ProgramRepository.LogNutritionAsync` + acción `POST /api/v1/program/nutrition/log`: upsert idempotente del `habit_check` (único por tripleta; duplicado → `409 HABIT_ALREADY_LOGGED`), XP granular por el camino del catálogo (`NUTRITION_MEAL_COMPLETE` 10×4/día / `NUTRITION_HYDRATION` 5×1/día, multiplicador del paciente incluido) y dedupe `('habit_log', habit_check.id, reason)`. Body `{ mealCode, localDate? }` (futura → `422 INVALID_DATE`); `[RequirePermission("Program.View")]`, paciente del JWT (anti-IDOR 404).
- **Affected paths**:
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/LogNutrition/LogNutritionCommand.cs` (nuevo; command + validator + handler)
  - `src/CoppAddresd.Application/Features/ProgramProgress/DTOs/Nutrition/NutritionDtos.cs` (nuevo; `NutritionLogResultDto`)
  - `src/CoppAddresd.Application/Interfaces/IProgramRepository.cs` (`LogNutritionAsync`)
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (implementación: FOR UPDATE sobre la inscripción activa, upsert, otorgamiento)
  - `src/CoppAddresd.Api/Controllers/ProgramController.cs` (`POST nutrition/log` + `LogNutritionRequest`)
- **Implementation notes**: La transacción sigue el patrón de `CompleteTaskAsync` (inscripción bloqueada FOR UPDATE → serializa los topes diarios/semanales). La tarea `nut` NO se auto-completa ni se toca: el log es aditivo (SPEC §18.5). Log estructurado sin PHI.
- **Acceptance criteria**: Application + Infrastructure + Api compilan (0 errores); AC-33/AC-34 reproducibles manualmente (primera comida → +10 y fila `habit_check`; misma comida el mismo día → `409 HABIT_ALREADY_LOGGED` sin doble XP; `agua` → +5 tope 1/día).
- **Verification**: `dotnet build src/CoppAddresd.Api` (temp OutputPath si un proceso de dev bloquea el bin); manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-60 — Otorgamientos semanales de nutrición en `POST /scores/calculate`

- **Phase**: P1.5
- **Depends on**: T-59 (`habit_checks`), T-47 (motor de período en `/calculate`), T-37 (`GetNutritionLogAsync`)
- **Objective**: `ProgramRepository.EvaluateNutritionAwardsAsync` + hook en `CalculateScoresCommandHandler`: tras persistir la fila de `health_scores`, evaluar la adherencia del período (MISMA fuente que la dimensión `nutrition` del Health Score — reutiliza `GetNutritionLogAsync`, no duplica) y otorgar `NUTRITION_WEEK_85` (≥85%, +75) y `NUTRITION_RECOVERY` (≥+20pp vs el período anterior, +50) una vez por período con el dedupe `('nutrition_period', health_scores.id, reason)` y el multiplicador del paciente. Generalizar el helper de otorgamiento de período (`AwardPeriodXpAsync` con `sourceRefType`).
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`EvaluateNutritionAwardsAsync`, `AwardPeriodXpAsync` compartido con la XP clínica)
  - `src/CoppAddresd.Application/Interfaces/IProgramRepository.cs` (`EvaluateNutritionAwardsAsync`)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/CalculateScores/CalculateScoresCommand.cs` (hook + log sin PHI)
  - `src/CoppAddresd.Application/Features/ProgramProgress/DTOs/Nutrition/NutritionDtos.cs` (`NutritionWeeklyAwardsResult`)
- **Implementation notes**: El GET `/scores` NUNCA llama al motor (solo computa y devuelve la cola). Sin logs en el período → adherencia 0 → sin premio (nunca penaliza, AC-35). La adherencia previa se lee de la fila anterior de `health_scores` (`score_nutrition`).
- **Acceptance criteria**: Application + Infrastructure compilan (0 errores); AC-35/AC-36 reproducibles manualmente (un otorgamiento por regla y período; re-correr `/calculate` no duplica; multiplicador aplicado).
- **Verification**: `dotnet build src/CoppAddresd.Application src/CoppAddresd.Infrastructure`; manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-61 — Seeder (4 reglas `nutrition` + 5 `habit_templates`) + docs (SPEC §18, PLAN, TASKS)

- **Phase**: P1.5
- **Depends on**: T-58 (schema), T-60
- **Objective**: Seed idempotente de las 4 reglas `NUTRITION_*` (SPEC §18.1) y las 5 plantillas de hábito (SPEC §18.2), y mantener PLAN/SPEC/TASKS consistentes con el "Paso 6".
- **Affected paths**:
  - `src/CoppAddresd.Api/Seeders/ProgramProgressSeeder.cs` (`SeedXpRulesAsync` + 4 reglas; `SeedNutritionHabitTemplatesAsync` nuevo)
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (§18 + decisión 24 + AC-33..AC-36 + §8.4/§8.5/§14.2)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (decisión 20 + phasing + riesgo)
  - `coppAddresdBack/docs/modules/program-progress/TASKS.md` (este batch)
- **Implementation notes**: `ON CONFLICT (code) DO NOTHING` (regla/plantilla existente nunca se pisa). Valores de SPEC §18.1 (10/5/75/50 con topes 4/1 por día) y §18.2 (`des`/`alm`/`mer`/`cen` categoría `alimentacion`, `agua` categoría `agua`). `NUTRITION_PHOTO` NO se siembra (diferido, §18.7). Logs: sin PHI.
- **Acceptance criteria**: Tras el restart, `SELECT COUNT(*) FROM app.xp_rules WHERE category = 'nutrition'` = 4 y `SELECT COUNT(*) FROM app.habit_templates` = 5; re-correr el seeder no duplica ni pisa. Docs consistentes (`habit_checks`, `nutrition/log`, `B5-N`, `T-58..T-61`, `AC-36`, `AddProgramProgressNutritionXp`).
- **Verification**: `dotnet build src/CoppAddresd.Api`; docs cross-check (grep de `habit_templates`, `NUTRITION_WEEK_85`, `B5-N`).
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-NB — Nutriobiótico streak (P1.5, "Paso 7a")

> Racha propia de la tarea `nutribiotico` (SPEC §19, decisión 25): la tarea
> mantiene su PROPIA racha consecutiva en `app.streak_states`
> (`nb_current_streak`/`nb_longest_streak`/`nb_last_completed_date`),
> independiente de la racha general y de los congelamientos (un día perdido la
> rompe — AC-38). Los hitos de corrida (7/14/30/60/90 → `NB_STREAK_*`,
> 50/100/250/500/1000 XP, categoría `nutriobiotic`) se otorgan en el camino de
> completación de la tarea (solo primera escritura) con el multiplicador del
> paciente y el dedupe `('nb_milestone', task_completions.id, reason)` — cada
> corrida nueva re-otorga su hito (AC-39); tope 1/semana. **Tests OPTIONALES /
> manuales per el workflow actual** (sin `dotnet test` automático; gate = build
> verde + migración generada sin aplicar).

### T-62 — Schema: entidad `StreakState` + enums `XpReason`/`XpRuleCodes` + configuración EF + migración `AddProgramProgressNbStreak`

- **Phase**: P1.5
- **Depends on**: T-50 (multiplicador/`multiplier_*` en `streak_states`), T-02 (EF conventions), T-03 (migration flow)
- **Objective**: 3 columnas de racha propia del nutribiótico en `app.streak_states` (`nb_current_streak` SMALLINT NOT NULL default 0, `nb_longest_streak` SMALLINT NOT NULL default 0, `nb_last_completed_date` DATE NULL) + 5 miembros `NB_STREAK_*` en `XpReason` (16..20) y 5 constantes homónimas en `XpRuleCodes` + configuración EF + migración aditiva y reversible `AddProgramProgressNbStreak`.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/StreakState.cs` (3 propiedades `Nb*`)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/XpReason.cs` + `XpRuleCodes.cs` (5 miembros/constantes `NB_STREAK_*`)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/StreakStateConfiguration.cs` (3 columnas: `smallint` default 0 + `date` nullable)
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressNbStreak.cs` (nueva, aditiva y reversible)
- **Implementation notes**: Convenciones de `streak_states` (PK = `enrollment_id`, snake_case, defaults en migración). `short` → `smallint` (Npgsql, precedente `ProgramTemplate.StreakMinTasks`). Sin tablas nuevas ni índices (una fila por inscripción, lecturas por PK). Sin cambios en `AppDbContext` (el `DbSet<StreakState>` ya existe).
- **Acceptance criteria**: Domain + Infrastructure compilan (0 errores); `AddProgramProgressNbStreak` es aditiva y reversible (3 `AddColumn`/3 `DropColumn`); `\d app.streak_states` muestra las 3 columnas con sus defaults.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure` (temp OutputPath si un proceso de dev bloquea el bin); migración generada (NO aplicada).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-63 — Seeder (5 reglas `NB_STREAK_*`, categoría `nutriobiotic`)

- **Phase**: P1.5
- **Depends on**: T-62 (enums), T-45 (patrón del seeder `SeedXpRulesAsync`)
- **Objective**: Seed idempotente de las 5 reglas de la racha del nutribiótico (SPEC §19.2): `NB_STREAK_7` (50), `NB_STREAK_14` (100), `NB_STREAK_30` (250), `NB_STREAK_60` (500), `NB_STREAK_90` (1000), categoría `nutriobiotic`, topes `max_per_day = 1`/`max_per_week = 1`.
- **Affected paths**:
  - `src/CoppAddresd.Api/Seeders/ProgramProgressSeeder.cs` (`SeedXpRulesAsync` + 5 reglas; comentarios de cabecera: 19 → 24 reglas)
- **Implementation notes**: `ON CONFLICT (code) DO NOTHING` (regla existente nunca se pisa). Los nombres coinciden con los miembros de `XpReason` (dedupe por `reason`, precedente `CLINICAL_*`/`NUTRITION_*`).
- **Acceptance criteria**: Tras el restart, `SELECT COUNT(*) FROM app.xp_rules WHERE category = 'nutriobiotic'` = 5; re-correr el seeder no duplica ni pisa. Docs consistentes (`NB_STREAK_7`, `B5-NB`, `AC-39`, `AddProgramProgressNbStreak`).
- **Verification**: `dotnet build src/CoppAddresd.Api` (temp OutputPath si un proceso de dev bloquea el bin); docs cross-check.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-64 — Motor de la racha del nutribiótico + hitos en el camino de completación

- **Phase**: P1.5
- **Depends on**: T-62 (schema), T-43 (`ResolveXpAwardAsync`), T-51 (patrón del motor de hitos)
- **Objective**: `ProgramRepository.UpdateNbStreakAsync` + hook en `CompleteTaskCoreAsync` (solo primera escritura de `task_code = 'nutribiotico'`, dentro de la transacción FOR UPDATE): nuevo conteo (ayer → `+1`; cualquier otro caso → `1`), actualización vía `ExecuteUpdate` (`nb_current_streak`, `nb_longest_streak = MAX`, `nb_last_completed_date = hoy`) y, si el conteo cae exactamente en un hito (7/14/30/60/90), otorgamiento por el camino del catálogo (`rule_code = NB_STREAK_{days}`, `source_ref_type = 'nb_milestone'`, `source_ref_id = task_completions.id`, `reason = 'NB_STREAK_{days}'`) con el multiplicador del paciente. Topes 1/día y 1/semana: un tope alcanzado omite el hito (nunca rompe la completación). NO toca la racha general, el día perfecto ni `TASK_NUTRIBIOTICO`.
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`UpdateNbStreakAsync`, `NbMilestones`, `FindNbMilestone`, `FindNextNbMilestone`, hook en `CompleteTaskCoreAsync`)
- **Implementation notes**: El dedupe parcial usa `source_ref_id = task_completions.id` (la completación que dispara el hito, única por corrida) para que cada corrida nueva RE-OTORGUE su hito (AC-39) sin colisionar con la corrida anterior (a diferencia de los hitos generales, SPEC §16, una vez por inscripción). `ResolveXpAwardAsync` puede lanzar `BusinessRuleViolationException` (XP_DAILY_LIMIT_REACHED/XP_WEEKLY_LIMIT_REACHED): se captura SOLO esa excepción y se omite el hito.
- **Acceptance criteria**: Infrastructure compila (0 errores); AC-37/AC-38/AC-39 reproducibles manualmente (día 7 → `NB_STREAK_7` +50 con `source_ref_id = task_completions.id`; día perdido → reinicio a 1 sin consumir congelamiento; corrida nueva → re-otorgamiento; misma semana → tope 1/semana omite sin error).
- **Verification**: `dotnet build src/CoppAddresd.Infrastructure` (temp OutputPath si un proceso de dev bloquea el bin); manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-65 — Snapshot (bloque `streak` aditivo) + docs (SPEC §19, PLAN, TASKS)

- **Phase**: P1.5
- **Depends on**: T-62 (schema), T-64 (motor)
- **Objective**: Exponer en `GET /api/v1/program/me/snapshot` (bloque `streak`, aditivo — los campos existentes no cambian) `nbStreak` (actual), `nbLongestStreak` y `nbNextMilestone` (`{ days, xp, daysRemaining } | null`; próximo hito de la tabla 7/14/30/60/90 por encima de la racha actual; null si ya ≥ 90). Mantener PLAN/SPEC/TASKS consistentes con el "Paso 7a".
- **Affected paths**:
  - `src/CoppAddresd.Application/DTOs/ProgramProgress/ProgramProgressDtos.cs` (`StreakInfoDto` + 3 campos aditivos con default; `NbNextMilestoneDto` nuevo)
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`GetSnapshotAsync`: proyección + cómputo de `nbNextMilestone` + construcción de `StreakInfoDto`)
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (§19 + decisión 25 + AC-37..AC-39 + §8.4/§14.2)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (decisión 21 + phasing + milestone + riesgo)
  - `coppAddresdBack/docs/modules/program-progress/TASKS.md` (este batch)
- **Implementation notes**: Campos con default (`NbStreak = 0`, `NbLongestStreak = 0`, `NbNextMilestone = null`) para no romper los call sites existentes (precedente: `StreakInfoDto` en tests). La lectura nunca escribe (mismo patrón que el multiplicador, SPEC §16, D).
- **Acceptance criteria**: Application + Infrastructure + tests compilan (0 errores — los constructores existentes de `StreakInfoDto` siguen válidos por los defaults); el snapshot expone los 3 campos nuevos sin alterar los previos; docs consistentes (`nbStreak`, `NB_STREAK_90`, `B5-NB`, `T-62..T-65`, `AC-39`, `AddProgramProgressNbStreak`).
- **Verification**: `dotnet build src/CoppAddresd.Application src/CoppAddresd.Infrastructure tests/CoppAddresd.UnitTests` (temp OutputPath si un proceso de dev bloquea el bin); docs cross-check.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-NOT — Gamified notifications (P1.5, "Paso 7b")

> Notificaciones gamificadas (SPEC §20, decisión 26): log `app.notifications` +
> push FCM **best-effort** disparado transaccionalmente dentro de los flujos de
> otorgamiento (hito de racha, hito del nutribiótico, subida de nivel y día
> perfecto — SOLO primera concesión), reutilizando el camino FCM EXISTENTE
> (`app.device_tokens` + `IFcmClient`, READ-ONLY: no se toca FcmClient/
> DeviceTokenRepository/NotificationsController). Anti-spam (máx. 2 por tipo por
> día + 6 totales por día, config `Program:Notifications`) y horario de
> silencio 22:00–07:00 local (critical lo ignora). Lo que necesita timing
> (`multiplier_expiring`, racha en riesgo, evaluación semanal) queda
> documentado como FUTURO (requiere scheduler, §20.4). **Tests OPTIONALES /
> manuales per el workflow actual** (sin `dotnet test` automático; gate = build
> verde + migración generada sin aplicar).

### T-66 — Schema: entidad `AppNotification` + configuración EF + `DbSet` + migración `AddProgramProgressNotifications`

- **Phase**: P1.5
- **Depends on**: T-02 (EF conventions), T-03 (migration flow)
- **Objective**: Entidad `AppNotification` + `AppNotificationConfiguration` (schema `app`, `notifications`: `id` uuid PK default `gen_random_uuid()`, `patient_id` FK `patient_profiles` RESTRICT, `type` varchar(60), `title` varchar(120), `message` text, `priority` varchar(20) default `'normal'`, `channel` varchar(20) default `'push'`, `sent_at` timestamptz default `now()`, `read_at` timestamptz NULL, índice `ix_notifications_patient_sent_at` (`patient_id`, `sent_at` DESC)) + `DbSet<AppNotification>` + la migración aditiva y reversible que crea la tabla con auditoría trigger-based (sin PHI, precedente `habit_checks`) y GRANTs a `app_user`.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/AppNotification.cs` (nuevo)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/AppNotificationConfiguration.cs` (nuevo)
  - `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs` (`DbSet<AppNotification>`)
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressNotifications.cs` (nueva, aditiva)
- **Implementation notes**: Convenciones del módulo (§3): snake_case, defaults en migración, FK RESTRICT, auditoría `audit.attach_table_audit('app', 'notifications', 'id', ...)` + `GRANT ... TO app_user` (patrón `AddProgramProgressNutritionXp`).
- **Acceptance criteria**: Domain + Infrastructure compilan (0 errores); `AddProgramProgressNotifications` es aditiva y reversible; `\d app.notifications` muestra el índice `(patient_id, sent_at DESC)`, los defaults y el trigger de auditoría.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure` (temp OutputPath si un proceso de dev bloquea el bin); migración generada (NO aplicada).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-67 — Servicio `IGamifiedNotificationService` + repositorio del log + anti-spam

- **Phase**: P1.5
- **Depends on**: T-66 (tabla), T-42 (patrón DI de repositorios)
- **Objective**: Servicio best-effort (Application, `Services/ProgramProgress/`) que inserta el log y envía el push FCM por el camino EXISTENTE (`IDeviceTokenRepository` fan-out + `IFcmClient`; token obsoleto `UNREGISTERED` → se elimina). Anti-spam configurable (`Program:Notifications`: `MaxPerTypePerDay` = 2, `MaxPerDay` = 6, `QuietHoursStart` = 22, `QuietHoursEnd` = 7): límite alcanzado → omisión silenciosa (log debug, AC-41); horario de silencio en hora local del paciente (critical lo ignora). **Nunca lanza** (AC-42: el flujo de XP continúa intacto). Repositorio `INotificationLogRepository` (Application) + `NotificationLogRepository` (Infrastructure): contexto del paciente (userId + timezone), conteos por día, inserción, listado paginado y marcar leída. DI en `Infrastructure/DependencyInjection.cs`.
- **Affected paths**:
  - `src/CoppAddresd.Application/Services/ProgramProgress/IGamifiedNotificationService.cs` (nuevo)
  - `src/CoppAddresd.Application/Services/ProgramProgress/GamifiedNotificationService.cs` (nuevo)
  - `src/CoppAddresd.Application/Interfaces/INotificationLogRepository.cs` (nuevo)
  - `src/CoppAddresd.Infrastructure/Repositories/NotificationLogRepository.cs` (nuevo)
  - `src/CoppAddresd.Infrastructure/DependencyInjection.cs` (registro del servicio + repositorio)
  - `src/CoppAddresd.Application/CoppAddresd.Application.csproj` (`Microsoft.Extensions.Configuration.Abstractions` 10.0.10 — `IConfiguration` del servicio)
- **Implementation notes**: El log se persiste con la MISMA unidad de trabajo del flujo de otorgamiento (mismo `AppDbContext` scoped): queda atómico con la XP. El cuerpo completo del servicio captura excepciones (best-effort, AC-42). El día/ventana del anti-spam se calcula en hora local del paciente (fecha local → rango UTC, DST-aware, precedente `LocalDateToUtcStart`).
- **Acceptance criteria**: Application + Infrastructure compilan (0 errores); AC-41/AC-42 reproducibles manualmente (límite alcanzado → sin fila ni push, solo log debug; FCM caído → log persistido + XP intacta).
- **Verification**: `dotnet build src/CoppAddresd.Application src/CoppAddresd.Infrastructure`; manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-68 — Disparadores transaccionales en `ProgramRepository` (hitos, nivel, día perfecto)

- **Phase**: P1.5
- **Depends on**: T-67 (servicio), T-51 (motor de hitos), T-64 (racha nutribiótico)
- **Objective**: Hooks de notificación en los flujos de otorgamiento de `CompleteTaskCoreAsync` (constructor con parámetro opcional `IGamifiedNotificationService?` — null → omitir, patrón de los calculadores): hito de racha (`milestone_reached` high, "🏆 ¡X días! +N XP", x2 plegado en 11/22/50), hito del nutribiótico (`nb_milestone` high, "💊 ¡X días tomando tu Nutriobiótico!"), subida de nivel (`level_up` high, comparando el nivel antes/después de TODOS los otorgamientos del día, tras el flush), día perfecto (`day_complete` normal, "✅ Día perfecto · +N XP"). Solo primera concesión (el replay nunca llega; las guardias de hitos protegen también la notificación).
- **Affected paths**:
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (hooks en `CompleteTaskCoreAsync`, `AwardStreakMilestoneIfReachedAsync` + parámetro `patientId`, `UpdateNbStreakAsync` + parámetro `patientId`)
- **Implementation notes**: La notificación corre DESPUÉS de la escritura de la XP y NUNCA la revierte (best-effort, AC-42). `multiplier_expiring` es FUTURO (necesita scheduler) — no se implementa (§20.4).
- **Acceptance criteria**: Infrastructure compila (0 errores); AC-40 reproducible manualmente (cada evento de primera concesión → 1 fila `app.notifications` con su tipo; replay → 0 filas; XP intacta ante cualquier fallo).
- **Verification**: `dotnet build src/CoppAddresd.Infrastructure` (temp OutputPath si un proceso de dev bloquea el bin); manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-69 — Endpoints del centro de notificaciones + docs (SPEC §20, PLAN, TASKS)

- **Phase**: P1.5
- **Depends on**: T-66, T-67
- **Objective**: `GET /api/v1/program/notifications` (paginado, `readAt` + `unreadCount`, `Program.View`, paciente del JWT — anti-IDOR 404) y `POST /api/v1/program/notifications/{id:guid}/read` (marca leída; 404 si no es del paciente). Mantener PLAN/SPEC/TASKS consistentes con el "Paso 7b".
- **Affected paths**:
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListNotifications/ListNotificationsQuery.cs` (nuevo; query + handler)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/MarkNotificationRead/MarkNotificationReadCommand.cs` (nuevo; command + validator + handler)
  - `src/CoppAddresd.Application/Features/ProgramProgress/DTOs/Notifications/NotificationDtos.cs` (nuevo; `NotificationDto`, `PaginatedNotificationsResult`)
  - `src/CoppAddresd.Api/Controllers/ProgramController.cs` (dos acciones)
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (§20 + decisión 26 + AC-40..AC-42 + §1/§8.5/§10.2/§11)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (decisión 22 + phasing + riesgo)
  - `coppAddresdBack/docs/modules/program-progress/TASKS.md` (este batch)
- **Implementation notes**: Mismo patrón de `GetScores`/`LogNutrition` (resolución del paciente vía `IProgramActorContext`, nunca del body). pageSize clamp 1..100 (convención del módulo).
- **Acceptance criteria**: Api compila (0 errores); `GET /notifications` con JWT de paciente devuelve el shape de SPEC §20.5; `POST .../{id}/read` de una notificación ajena → 404 (anti-IDOR). Docs consistentes (`notifications`, `B5-NOT`, `T-66..T-69`, `AC-42`, `AddProgramProgressNotifications`).
- **Verification**: `dotnet build src/CoppAddresd.Api` (temp OutputPath si un proceso de dev bloquea el bin); docs cross-check (grep de `app.notifications`, `B5-NOT`, `AddProgramProgressNotifications`).
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-WK — Weakness detection (P1.5, "Paso 7c")

> Detección determinista de debilidades del paciente (SPEC §21, decisión 27): el
> motor de reglas (ADRED-inspired, sin ML) convierte la MISMA data de los
> puntajes (SPEC §13/§18) en hallazgos accionables, disparado SOLO en
> `POST /scores/calculate` (una vez por recálculo, idempotente por el dedupe de
> estado abierto AC-43). Cola clínica + transiciones con guardia clínica AC-22
> (AC-44); vista del paciente (AC-45). La narrativa semanal LLM queda
> DOCUMENTADA como contrato FUTURO (SPEC §21.5, P3: requiere endpoint nuevo en
> el ai-service + scheduler o disparo manual) — NO se implementa en este paso.
> **Tests OPTIONALES / manuales per el workflow actual** (sin `dotnet test`
> automático; gate = build verde + migración generada sin aplicar).

### T-70 — Schema: entidad `Weakness` + enums + configuración EF + `DbSet` + migración `AddProgramProgressWeaknesses`

- **Phase**: P1.5
- **Depends on**: T-02 (EF conventions), T-03 (migration flow), T-35 (FK `measurement_metrics` disponible)
- **Objective**: Entidad `Weakness` + 4 enums string-backed (`WeaknessCategory`/`WeaknessSeverity`/`WeaknessStatus`/`WeaknessSource`) + códigos `WeaknessCodes` (`WK_*`) + `WeaknessConfiguration` + `DbSet<Weakness>` + la migración aditiva y reversible que crea `app.weaknesses` (CHECKs de valores, índices `(patient_id, status)` y `(status)`, FK SQL a `auth.users` para `assigned_to` con ON DELETE SET NULL, GRANTs a `app_user`; **sin trigger de auditoría** — `description` puede contener contexto clínico, exclusión por diseño §21.1/§8.5).
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/Weakness.cs` (nuevo)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/WeaknessCategory.cs` + `WeaknessSeverity.cs` + `WeaknessStatus.cs` + `WeaknessSource.cs` + `WeaknessCodes.cs` (nuevos)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/WeaknessConfiguration.cs` (nuevo)
  - `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs` (`DbSet<Weakness>`)
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressWeaknesses.cs` (nueva, aditiva)
- **Implementation notes**: Convenciones del módulo (§3): snake_case, defaults en migración (`severity='low'`, `source='ai'`, `status='open'`, `detected_at now()`), FK RESTRICT a `patient_profiles`/`measurement_metrics`. Migración con `table.CheckConstraint` para los 4 CHECKs (patrón `AddProgramProgressClinicalXp`).
- **Acceptance criteria**: Domain + Infrastructure compilan (0 errores); `AddProgramProgressWeaknesses` es aditiva y reversible; `\d app.weaknesses` muestra las columnas con defaults + CHECKs, los 2 índices y la FK a `auth."Users"` (assigned_to); sin trigger de auditoría sobre la tabla.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure` (temp OutputPath si un proceso de dev bloquea el bin); migración generada (NO aplicada).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-71 — Motor de reglas `WeaknessRulesEngine` + `PatientWeeklyData` + servicio de detección

- **Phase**: P1.5
- **Depends on**: T-70 (schema), T-37 (`BuildHealthScoreInputAsync`/`GetNutritionLogAsync`/helpers de período)
- **Objective**: `WeaknessRulesEngine` (función pura, 10 reglas `WK_*` con umbrales documentados — clínicos marcados `REQUIRES_CLINICAL_VALIDATION`) + `PatientWeeklyData` (paquete semanal: adherencia nutricional = misma fuente §18; glucosa/% grasa desde líneas base + mediciones; motivación = proxy `mood × 2` del último registro emocional; estrés/sueño LATENTES; adherencia semanal desde `health_scores`; nutribiótico 7d y ejercicio desde `task_completions`) + `IWeaknessDetectionService`/`WeaknessDetectionService` (orquesta: repositorio → motor → persistencia con dedupe AC-43).
- **Affected paths**:
  - `src/CoppAddresd.Application/Services/ProgramProgress/PatientWeeklyData.cs` (nuevo)
  - `src/CoppAddresd.Application/Services/ProgramProgress/WeaknessDescriptor.cs` (nuevo)
  - `src/CoppAddresd.Application/Services/ProgramProgress/WeaknessRulesEngine.cs` (nuevo)
  - `src/CoppAddresd.Application/Services/ProgramProgress/IWeaknessDetectionService.cs` + `WeaknessDetectionService.cs` (nuevos)
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`BuildPatientWeeklyDataAsync`, `PersistDetectedWeaknessesAsync`; queries set-based sin N+1)
  - `src/CoppAddresd.Application/Interfaces/IProgramRepository.cs` (extender)
  - `src/CoppAddresd.Infrastructure/DependencyInjection.cs` (registro del servicio)
- **Implementation notes**: Indicador `null` → la regla NO dispara (nunca penaliza por ausencia de datos). El dedupe lee los códigos `open`/`acknowledged`/`in_intervention` del paciente en una query set y omite los descriptores repetidos. El servicio loguea sin PHI (`Program.WeaknessesDetected`).
- **Acceptance criteria**: Application + Infrastructure compilan (0 errores); AC-43 reproducible manualmente (2 reglas disparan → 2 filas; re-correr `/calculate` no duplica).
- **Verification**: `dotnet build src/CoppAddresd.Application src/CoppAddresd.Infrastructure`; manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-72 — Trigger en `POST /scores/calculate` + endpoints (cola clínica, vista paciente, transiciones)

- **Phase**: P1.5
- **Depends on**: T-71 (motor + servicio), T-38 (handler de `/calculate`), T-48 (guardia clínica AC-22)
- **Objective**: Hook de detección en `CalculateScoresCommandHandler` (una vez por recálculo, tras XP clínica + nutrición; `GET /scores` nunca detecta — AC-45) + 3 endpoints: `GET /api/v1/program/weaknesses` (`Program.View`, paciente del JWT, anti-IDOR 404), `GET /api/v1/program/weaknesses/open` (`Program.Adapt`, cola FIFO de `open`), `POST /api/v1/program/weaknesses/{id:guid}/status` (`Program.Adapt` + rol clínico AC-22 → paciente 403, AC-44; transición idempotente, `resolved` fija `resolved_at`, `open` rechazado).
- **Affected paths**:
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/CalculateScores/CalculateScoresCommand.cs` (hook + log sin PHI)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListWeaknesses/ListWeaknessesQuery.cs` (nuevo; query + handler)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListOpenWeaknesses/ListOpenWeaknessesQuery.cs` (nuevo; query + handler)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/UpdateWeaknessStatus/UpdateWeaknessStatusCommand.cs` (nuevo; command + validator + handler)
  - `src/CoppAddresd.Application/Features/ProgramProgress/DTOs/Weaknesses/WeaknessDtos.cs` (nuevo; `WeaknessDto`, `PaginatedWeaknessesResult`, `UpdateWeaknessStatusRequest`)
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (`ListWeaknessesAsync`, `ListOpenWeaknessesAsync`, `UpdateWeaknessStatusAsync`)
  - `src/CoppAddresd.Api/Controllers/ProgramController.cs` (3 acciones)
  - `tests/CoppAddresd.UnitTests/ProgramProgress/Handlers/FakeProgramRepository.cs` (fix de compilación: 5 miembros nuevos del contrato, patrón del fake)
- **Implementation notes**: La transición usa `CreateExecutionStrategy` (una transacción: estado + `resolved_at`); guardia AC-22 con `ClinicianRoles` compartido. Validator: `weaknessId` requerido y estado en los 4 transicionables (`open` → error de validación). Logs sin PHI.
- **Acceptance criteria**: Api compila (0 errores); AC-44/AC-45 reproducibles manualmente (clínico transiciona; paciente → 403; detección solo en `/calculate`).
- **Verification**: `dotnet build src/CoppAddresd.Api` (temp OutputPath si un proceso de dev bloquea el bin); manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-73 — Docs (SPEC §21, PLAN, TASKS) + contrato FUTURO de la evaluación semanal

- **Phase**: P1.5
- **Depends on**: T-70, T-72
- **Objective**: Mantener PLAN/SPEC/TASKS consistentes con el "Paso 7c" y documentar el contrato FUTURO de la evaluación semanal LLM (SPEC §21.5): `WeeklyAssessmentContext` (scores + dimensiones + debilidades + resumen de adherencia) → endpoint NUEVO del ai-service → narrativa ADRED (`summary`/`strengths`/`weaknesses_text`/`recommendations`/`next_actions`). NO implementar la llamada IA (el endpoint no existe).
- **Affected paths**:
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (§21 + decisión 27 + AC-43..AC-45 + §1/§8.5/§10.2)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (decisión 23 + phasing + riesgo + milestone)
  - `coppAddresdBack/docs/modules/program-progress/TASKS.md` (este batch)
- **Implementation notes**: Los umbrales clínicos (glucosa 125 mg/dL, delta % grasa 0.3) llevan la marca `REQUIRES_CLINICAL_VALIDATION`; estrés/sueño documentados como reglas LATENTES (sin fuente física aún). La tabla `weaknesses` documentada como NO auditada (PHI en `description`).
- **Acceptance criteria**: Docs consistentes (`weaknesses`, `WK_NUT_LOW_ADHERENCE`, `B5-WK`, `T-70..T-73`, `AC-45`, `AddProgramProgressWeaknesses`, `WeaknessRulesEngine`).
- **Verification**: docs cross-check (grep de `weaknesses`, `B5-WK`, `AddProgramProgressWeaknesses`, `REQUIRES_CLINICAL_VALIDATION`).
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B5-IV — Interventions & tele XP (P1.5, "Paso 7d")

> Intervenciones derivadas de debilidades + hooks de telemedicina (SPEC §22,
> decisión 28): la tabla `app.interventions` con máquina de estados, 7 reglas
> XP de categoría `intervention` (una por evento del ciclo de vida), creación
> automática desde debilidades (AC-46), aceptación del paciente (AC-47),
> completación clínica con validación (AC-48) y hooks de telemedicina
> (AC-49). El contrato cross-service con el servicio de Telemedicina queda
> DOCUMENTADO (sin código cross-repo). **Tests OPTIONALES / manuales per el
> workflow actual** (sin `dotnet test` automático; gate = build verde +
> migración generada sin aplicar).

### T-74 — Schema: entidad `Intervention` + enums `InterventionType`/`InterventionStatus` + configuración EF + `DbSet` + migración `AddProgramProgressInterventions`

- **Phase**: P1.5
- **Depends on**: T-70 (weaknesses schema + entity pattern), T-02 (EF conventions), T-03 (migration flow)
- **Objective**: Entidad `Intervention` + 2 enums string-backed (`InterventionType`/`InterventionStatus`) + `InterventionConfiguration` + `DbSet<Intervention>` + la migración aditiva y reversible que crea `app.interventions` con CHECKs, índices `(patient_id, status)` y `(status)`, FK SQL a `auth.users` para `assigned_to` con ON DELETE SET NULL, FK a `weaknesses` con ON DELETE SET NULL, trigger de auditoría (sin PHI, precedente `notifications`) y GRANTs a `app_user`.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/Intervention.cs` (nuevo)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/InterventionType.cs` (nuevo)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/InterventionStatus.cs` (nuevo)
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/InterventionConfiguration.cs` (nuevo)
  - `src/CoppAddresd.Infrastructure/Persistence/AppDbContext.cs` (`DbSet<Intervention>`)
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddProgramProgressInterventions.cs` (nueva, aditiva)
- **Implementation notes**: Convenciones del módulo (§3): snake_case, defaults en migración (`severity='medium'`, `status='detected'`, `xp_awarded_total=0`), FK RESTRICT a `patient_profiles`, SET NULL a `weaknesses`. La FK a `auth.users` para `assigned_to` se crea por SQL en la migración (patrón `weaknesses.assigned_to`). 8 tipos de intervención cubren todas las acciones del motor de debilidades (§22.4). 7 estados cubren la máquina de vida (§22.2).
- **Acceptance criteria**: Domain + Infrastructure compilan (0 errores); `AddProgramProgressInterventions` es aditiva y reversible; `\d app.interventions` muestra las columnas con defaults + CHECKs, los 2 índices, las FKs y el trigger de auditoría.
- **Verification**: `dotnet build src/CoppAddresd.Domain src/CoppAddresd.Infrastructure` (temp OutputPath si un proceso de dev bloquea el bin); migración generada (NO aplicada).
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-75 — Seeder (7 reglas `intervention`) + enums en `XpReason`/`XpRuleCodes`

- **Phase**: P1.5
- **Depends on**: T-74 (entity/enums), T-45 (patrón del seeder `SeedXpRulesAsync`)
- **Objective**: Seed idempotente de las 7 reglas de intervenciones (SPEC §22.3): `WEAKNESS_ASSESS` (20), `INTERV_ACCEPT` (15), `TELE_SCHEDULE` (50), `TELE_ATTEND` (100, requires_validation), `TELE_COMPLY` (50), `INTERV_COMPLETE` (200, requires_validation), `RECOVERY_MISSION` (50), categoría `intervention`. Agregar 7 miembros `WEAKNESS_ASSESS`/`INTERV_ACCEPT`/`TELE_SCHEDULE`/`TELE_ATTEND`/`TELE_COMPLY`/`INTERV_COMPLETE`/`RECOVERY_MISSION` a `XpReason` (21..27) y 7 constantes homónimas a `XpRuleCodes`.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/XpReason.cs` (7 miembros nuevos)
  - `src/CoppAddresd.Domain/Enums/ProgramProgress/XpRuleCodes.cs` (7 constantes nuevas)
  - `src/CoppAddresd.Api/Seeders/ProgramProgressSeeder.cs` (`SeedXpRulesAsync` + 7 reglas; actualizar docstring de 24→31)
- **Implementation notes**: `ON CONFLICT (code) DO NOTHING` (regla existente nunca se pisa). Los nombres coinciden con los miembros de `XpReason` (dedupe por reason, precedente `CLINICAL_*`/`NUTRITION_*`/`NB_STREAK_*`). `TELE_ATTEND` e `INTERV_COMPLETE` son las únicas con `RequiresValidation = true` (se validan por un clínico). Logs sin PHI.
- **Acceptance criteria**: Tras el restart, `SELECT COUNT(*) FROM app.xp_rules WHERE category = 'intervention'` = 7; re-correr el seeder no duplica ni pisa. Domain compila (0 errores).
- **Verification**: `dotnet build src/CoppAddresd.Domain`; docs cross-check.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-76 — Creación de intervenciones desde debilidades + máquina de estados + XP awards

- **Phase**: P1.5
- **Depends on**: T-74 (schema), T-75 (enums + seeder), T-71 (motor de reglas de debilidades), T-43 (`ResolveXpAwardAsync`)
- **Objective**: Extender `IProgramRepository` + `ProgramRepository` con 8 métodos de intervención (`EnsureInterventionFromWeaknessAsync`, `ListInterventionsAsync`, `ListOpenInterventionsAsync`, `AcceptInterventionAsync`, `UpdateInterventionStatusAsync`, `MarkTeleScheduledAsync`, `MarkTeleAttendedAsync`, `MarkTeleComplyAsync`) + `AwardInterventionXpAsync` (helper privado, camino del catálogo con dedupe parcial `'intervention'`). Extender `WeaknessDetectionService` con `MapActionToInterventionType` (§22.4) y creación automática post-persistencia. Actualizar `PersistDetectedWeaknessesAsync` para devolver las debilidades nuevas (con IDs) en lugar de solo el conteo.
- **Affected paths**:
  - `src/CoppAddresd.Application/Interfaces/IProgramRepository.cs` (8 métodos nuevos + `PersistDetectedWeaknessesAsync` retorno cambiado a `IReadOnlyList<Weakness>`)
  - `src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` (8 implementaciones + helper + `ToInterventionDto` + `IsValidInterventionTransition` + `GetEnrollmentIdForPatientAsync`)
  - `src/CoppAddresd.Application/Services/ProgramProgress/WeaknessDetectionService.cs` (extender con `MapActionToInterventionType` y loop de creación de intervenciones post-persistencia, best-effort)
  - `tests/CoppAddresd.UnitTests/ProgramProgress/Handlers/FakeProgramRepository.cs` (fix de compilación: 8 miembros nuevos del contrato)
- **Implementation notes**: La creación de intervención corre DESPUÉS de persistir las debilidades (ya en la misma transacción de `/calculate`). `EnsureInterventionFromWeaknessAsync` verifica que no exista ya una intervención para la debilidad (AC-46: una por debilidad) y transiciona la debilidad a `in_intervention`. Cada XP award usa `AwardInterventionXpAsync` con el dedupe parcial `('intervention', intervention.id, reason)`. Las transiciones inválidas lanzan `BusinessRuleViolationException` → 409. `completed` requiere resultado. La debilidad se marca `resolved` al completar (AC-48). Los hooks de telemedicina (`MarkTele*`) no requieren guardia clínica explícita (la API los protege con `Program.Adapt`).
- **Acceptance criteria**: Application + Infrastructure compilan (0 errores); AC-46/AC-47/AC-48/AC-49 reproducibles manualmente (creación automática desde debilidad; aceptación; completación con resultado; hooks de tele XP).
- **Verification**: `dotnet build src/CoppAddresd.Application src/CoppAddresd.Infrastructure`; manual per el workflow actual.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-77 — Endpoints + controller + DTOs + docs (SPEC §22, PLAN, TASKS)

- **Phase**: P1.5
- **Depends on**: T-74, T-75, T-76
- **Objective**: 7 acciones en `ProgramController` + 6 MediatR commands/queries (`ListInterventionsQuery`, `ListOpenInterventionsQuery`, `AcceptInterventionCommand`, `UpdateInterventionStatusCommand`, `MarkTeleScheduledCommand`, `MarkTeleAttendedCommand`, `MarkTeleComplyCommand`) + DTOs (`InterventionDto`, `PaginatedInterventionsResult`, `UpdateInterventionStatusRequest`). Mantener PLAN/SPEC/TASKS consistentes con el "Paso 7d".
- **Affected paths**:
  - `src/CoppAddresd.Api/Controllers/ProgramController.cs` (7 acciones + requests)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListInterventions/` (nuevo)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/ListOpenInterventions/` (nuevo)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/AcceptIntervention/` (nuevo)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/UpdateInterventionStatus/` (nuevo)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/MarkTeleScheduled/` (nuevo)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/MarkTeleAttended/` (nuevo)
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/MarkTeleComply/` (nuevo)
  - `src/CoppAddresd.Application/Features/ProgramProgress/DTOs/Interventions/` (nuevo)
  - `coppAddresdBack/docs/modules/program-progress/SPEC.md` (§22 + decisión 28 + AC-46..AC-49 + §0 + §10.2)
  - `coppAddresdBack/docs/modules/program-progress/PLAN.md` (decisión 28 + phasing + riesgo)
  - `coppAddresdBack/docs/modules/program-progress/TASKS.md` (este batch)
- **Implementation notes**: Mismo patrón de debilidades (queries con `IProgramActorContext`, anti-IDOR, paginación default 20/max 100). Los hooks de telemedicina (`tele-scheduled`, `tele-attended`, `tele-comply`) son `POST` sin body relevante (el ID de la intervención viene de la ruta). `AcceptIntervention` requiere `Program.View` (paciente propio); los demás requieren `Program.Adapt`. Los validators de `UpdateInterventionStatus` exigen `result` cuando `status = completed`.
- **Acceptance criteria**: Api compila (0 errores); AC-46..AC-49 reproducibles manualmente; docs consistentes (`interventions`, `B5-IV`, `T-74..T-77`, `AC-49`, `AddProgramProgressInterventions`).
- **Verification**: `dotnet build src/CoppAddresd.Api` (temp OutputPath si un proceso de dev bloquea el bin); docs cross-check.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B6 — Mobile Slice 1+2 (P1)

> These tasks live in the `antares-paciente` repo. The backend is the dependency.

### T-16 — Mobile API client + types

- **Phase**: P1
- **Depends on**: T-13 (backend snapshot/complete endpoints live)
- **Objective**: Add a typed API client and the TypeScript types matching SPEC §7.
- **Affected paths**:
  - `antares-paciente/src/api/program.ts` (new)
  - `antares-paciente/src/types/program.ts` (new, exported)
  - `antares-paciente/src/api/http.ts` (extend existing HTTP wrapper with `getJson<T>` / `postJson<T>` if not present)
- **Implementation notes**: Use the same auth header + base URL as other API calls in `antares-paciente`. Generate `clientRequestId = ulid()` on each `completeStep`. Cache snapshots in `AppContext` for stale-while-revalidate.
- **Acceptance criteria**: `api/program.ts` exports `getSnapshot`, `completeTask`, `getCalendar`, `getPath`. Types match SPEC §7.1 JSON keys.
- **Verification**: `npm run build` passes; `npm run lint` passes; new file imports compile.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-17 — Wire `AppContext` to snapshot

- **Phase**: P1
- **Depends on**: T-16
- **Objective**: Replace `src/data/program.ts` static constants with live data where applicable.
- **Affected paths**:
  - `antares-paciente/src/context/AppContext.tsx` (add `programSnapshot` state + reducer actions)
  - `antares-paciente/src/pages/ProgramPage.tsx` (replace hardcoded `PROGRAM_TASKS`, `PROGRAM_POINTS_MAX`, `DAY_BONUS_PTS` with snapshot-derived values; fall back to mock if fetch fails)
- **Implementation notes**: Keep the mobile UI shapes intact. Mock fallback guarantees demo continuity. Add a thin `lastSyncFailed` badge state.
- **Acceptance criteria**:
  - App boot triggers `getSnapshot()`; failure → mock fallback, no error to the user.
  - XP gauge, level badge, task cards, and calendar render from snapshot data.
  - AC-18 is reproducible: backend off → mobile still renders.
- **Verification**:
  - `npm run dev` and open in the browser.
  - Toggle backend off; reload app; UI still renders.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-18 — Wire `completeStep` to `POST /tasks/complete`

- **Phase**: P1
- **Depends on**: T-17
- **Objective**: Optimistic UI + idempotent retry + reconcile on response.
- **Affected paths**:
  - `antares-paciente/src/context/AppContext.tsx` (`completeStep` reducer → `api/completeTask`)
  - `antares-paciente/src/pages/ProgramPage.tsx` (`finish` helper uses the new reducer)
- **Implementation notes**: Persist queued writes to `localStorage` keyed by `clientRequestId`. On reconnect, replay the queue in order. Replay must NOT award XP twice (server enforces; client just trusts the response).
- **Acceptance criteria**:
  - AC-01..AC-04 reproducible against the dev backend.
  - Toggling network mid-write does not double-award XP on next sync.
- **Verification**:
  - `npm run dev`; complete a task; kill network; complete again; restore network; refresh — XP is correct.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B7 — ERP admin screens (P1)

### T-19 — Templates list / detail / edit (ERP)

- **Phase**: P1
- **Depends on**: T-13
- **Objective**: ERP UI for managing `program_templates` and `weekly_day_templates`.
- **Affected paths** (ERP repo): `coppaddresd-front/app/admin/program/templates/*`
- **Implementation notes**: Reuse existing CRUD shell patterns. Validate that publishing a template triggers `POST /templates/{id}/publish` which bumps `version`. Editing weekday tasks via `PUT /weekday-tasks` (bulk replace). No silent edits.
- **Acceptance criteria**: Admin can create, edit, publish, and archive a template; weekday grid is editable; all calls require `Program.Edit`.
- **Verification**: `npm run dev` (ERP); manual smoke test.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-20 — Enrollment list (ERP)

- **Phase**: P1
- **Depends on**: T-13
- **Objective**: ERP screen for `GET /program/enrollments`.
- **Affected paths** (ERP repo): `coppaddresd-front/app/admin/program/enrollments/*`
- **Implementation notes**: Filters by `status`, `patientId`. Detail view shows streak, XP, current week, and adaptation history.
- **Acceptance criteria**: Admin can pause / resume / withdraw an enrollment; actions require `Program.Enroll`; audit row visible in `audit.activity_logs`.
- **Verification**: `npm run dev` (ERP); manual smoke test.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B8 — Mobile Slice 3+4 (calendar + path)

### T-21 — Calendar view integration

- **Phase**: P1 (UI) / P2 (depends on adaptation flow)
- **Depends on**: T-16
- **Objective**: Replace the static mock calendar with `GET /program/calendar`.
- **Affected paths**: `antares-paciente/src/pages/ProgramPage.tsx` (calendar tab), `antares-paciente/src/context/AppContext.tsx`.
- **Implementation notes**: Max 92-day window. Render perfect days in the existing green tone, missed days in red.
- **Acceptance criteria**: Calendar opens within 300ms after the first snapshot.
- **Verification**: `npm run dev`; manual smoke.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-22 — Path / sendero view integration

- **Phase**: P1 (UI) / P2 (depends on adaptation flow)
- **Depends on**: T-16
- **Objective**: Replace the static mock path with `GET /program/path`.
- **Affected paths**: `antares-paciente/src/pages/ProgramPage.tsx`, `antares-paciente/src/components/PathBar/*`.
- **Implementation notes**: Render 83 weeks, marking Locked/Active/Completed. Future weeks are visually locked (per SPEC §0 decision 11).
- **Acceptance criteria**: Future weeks are not interactive; completed weeks show points.
- **Verification**: `npm run dev`; manual smoke.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B9 — Adaptation engine (P2)

### T-23 — `ProgramAdaptationEngine` rule engine

- **Phase**: P2
- **Depends on**: T-09 (command handlers exist), T-15 (data exists)
- **Objective**: Deterministic rule engine that creates `adaptation_recommendations` rows.
- **Affected paths**:
  - `src/CoppAddresd.Application/Services/ProgramAdaptationEngine.cs`
  - `src/CoppAddresd.Application/Interfaces/IProgramAdaptationEngine.cs`
- **Implementation notes**: Pure functions, no I/O except through the repository. Rules:
  - 2+ missed perfect days in last 7 days → `RoutineContentRefresh`.
  - Mood consistently ≤ 2 for 7 days → `RoutineContentRefresh` (gentler variant).
  - Patient crosses 5000 XP → `DifficultyChange` (`requires_approval = true`).
  Engine is invoked after every `CompleteTask` in the same transaction.
- **Acceptance criteria**: AC-16 reproducible. Engine is deterministic (no time-based randomness).
- **Verification**: `dotnet test --filter "FullyQualifiedName~ProgramAdaptationEngine"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-24 — Adaptation decision command (Approve / Reject / Apply)

- **Phase**: P2
- **Depends on**: T-23
- **Objective**: Implement the state machine transitions and audit.
- **Affected paths**: `src/CoppAddresd.Application/Features/ProgramProgress/Commands/DecideAdaptation/Handler.cs`
- **Implementation notes**: When transitioning to `Applied`, write an audit row in `audit.activity_logs` with `action = 'AdaptationApplied'` and `target_entity_id`.
- **Acceptance criteria**: AC-17 reproducible; audit row visible in `SELECT * FROM audit.activity_logs WHERE action = 'AdaptationApplied'`.
- **Verification**: `dotnet test --filter "FullyQualifiedName~DecideAdaptation"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-25 — `media_progressions` table + media rotation logic

- **Phase**: P2
- **Depends on**: T-03 (initial migration shipped), T-24
- **Objective**: Add the deferred P2 table and the resolver logic.
- **Affected paths**:
  - `src/CoppAddresd.Domain/Entities/ProgramProgress/MediaProgression.cs`
  - `src/CoppAddresd.Infrastructure/Configurations/ProgramProgress/MediaProgressionConfiguration.cs`
  - `src/CoppAddresd.Infrastructure/Migrations/<timestamp>_AddMediaProgressions.cs`
  - `src/CoppAddresd.Application/Features/ProgramProgress/Queries/GetSnapshot/Handler.cs` (replace the P1 fallback with the P2 resolver)
- **Implementation notes**: Same `AddProgramProgressCore` migration discipline; new migration, additive only.
- **Acceptance criteria**: After migration, `media_progressions` exists; resolver returns the in-window MediaItem per `(template, weekday, local_date)`.
- **Verification**: `dotnet ef database update`; SQL probe.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B10 — ERP Adaptation queue (P2)

### T-26 — Adaptation review queue (ERP)

- **Phase**: P2
- **Depends on**: T-24
- **Objective**: ERP screen listing `Pending` adaptations and a decide button.
- **Affected paths** (ERP repo): `coppaddresd-front/app/admin/program/adaptations/*`
- **Implementation notes**: Filter by `requires_approval = true`. Reject and approve both write audit rows. Rejection requires a note (validation).
- **Acceptance criteria**: Clinician can approve or reject; audit rows visible in ERP detail page.
- **Verification**: `npm run dev`; manual smoke.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B11 — Mobile Slice 5 (P2)

### T-27 — Adaptation toast on next launch

- **Phase**: P2
- **Depends on**: T-26
- **Objective**: Mobile shows a toast on launch if there are `Applied` adaptations the patient has not seen.
- **Affected paths**: `antares-paciente/src/context/AppContext.tsx`, `antares-paciente/src/pages/ProgramPage.tsx`.
- **Implementation notes**: Use `GET /program/adaptations?status=Applied&page=1&pageSize=5`. Track `lastSeenAdaptationId` in `localStorage`.
- **Acceptance criteria**: After a clinician approves an adaptation, the patient sees a toast within 24h of next app open.
- **Verification**: `npm run dev`; manual smoke.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B12 — Streak rescue audit (P3)

### T-28 — Reconciliation job

- **Phase**: P3
- **Depends on**: T-12 (P1 stable)
- **Objective**: Nightly job walks `xp_ledger` and recomputes `streak_states` per enrollment.
- **Affected paths**:
  - `src/CoppAddresd.Application/Jobs/ReconcileStreakJob.cs`
  - `src/CoppAddresd.Infrastructure/BackgroundJobs/Dispatcher.cs` (use the existing `app.background_jobs` + `IJobDispatcher` pattern from patients module PLAN)
- **Implementation notes**: Idempotent (job_id unique). Logs discrepancies. Never overwrites an `is_perfect_day` written by the day handler.
- **Acceptance criteria**: Forcing a known-bad state and running the job restores the correct streak.
- **Verification**: `dotnet test --filter "FullyQualifiedName~ReconcileStreakJob"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B13 — Bulk enrollment (P3)

### T-29 — Bulk enrollment job (admin / clinic onboarding)

- **Phase**: P3
- **Depends on**: T-12
- **Objective**: Admin can enroll N patients in a single transaction with a single audit row per patient.
- **Affected paths**:
  - `src/CoppAddresd.Application/Features/ProgramProgress/Commands/BulkEnrollPatients/*`
  - `src/CoppAddresd.Api/Controllers/ProgramController.cs` (`POST /program/enrollments/bulk`)
- **Implementation notes**: Reuse the `IJobDispatcher` for >100 patients (async, returns a jobId). Permissions: `Program.Enroll`.
- **Acceptance criteria**: 500-patient bulk job completes with N rows; partial failures reported per row.
- **Verification**: integration test against a fixture DB with 500 patients.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B14 — CSV export (P3)

### T-30 — Export endpoint

- **Phase**: P3
- **Depends on**: T-29
- **Objective**: `GET /program/enrollments/export?clinicId=&from=&to=` returns a streamed CSV.
- **Affected paths**: `src/CoppAddresd.Api/Controllers/ProgramController.cs`.
- **Implementation notes**: Use `IAsyncEnumerable` to stream rows; do not buffer the whole result. Permission `Program.Export` (new in P3 seeder).
- **Acceptance criteria**: 100k-enrollment export completes in <30s; respects clinic scope.
- **Verification**: `dotnet test --filter "FullyQualifiedName~Export"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B15 — i18n (P3)

### T-31 — Mobile English strings + locale toggle

- **Phase**: P3
- **Depends on**: T-22 (P1 stable)
- **Objective**: Extract mobile strings into `i18n/en.json` and `i18n/es.json`; add a locale toggle in Settings.
- **Affected paths**: `antares-paciente/src/i18n/*` (new), `antares-paciente/src/pages/ProgramPage.tsx`, `antares-paciente/src/context/AppContext.tsx`.
- **Implementation notes**: Existing Spanish strings stay the default; English added in this task.
- **Acceptance criteria**: Switching the toggle translates the Program page without reload.
- **Verification**: `npm run dev`; manual smoke in both locales.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Batch B16 — Verification & observability (G3)

### T-32 — Performance tests

- **Phase**: G3
- **Depends on**: B1..B7 done
- **Objective**: Verify SPEC §10.4 performance budget (`snapshot < 200ms p95`, `complete < 150ms p95`).
- **Affected paths**: `tests/CoppAddresd.UnitTests/ProgramProgress/PerformanceTests.cs`
- **Implementation notes**: Seed 10k enrollments; run k6 or a custom `dotnet-counters` harness. No flaky assertions (warmup + median over 100 samples).
- **Acceptance criteria**: p95 under budget.
- **Verification**: harness script output committed to PR.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-33 — IDOR / scope / concurrency suite

- **Phase**: G3
- **Depends on**: B1..B7 done
- **Objective**: Final security / correctness suite (covers AC-10, AC-11, AC-12).
- **Affected paths**: `tests/CoppAddresd.UnitTests/ProgramProgress/SecurityTests.cs`
- **Implementation notes**: Same `COP_TEST_DB_CONNECTION` pattern; multi-tenant fixtures.
- **Acceptance criteria**: All 6+ security tests pass; no N+1 detected by `query-performance` skill review.
- **Verification**: `dotnet test --filter "FullyQualifiedName~ProgramProgress.Security"`.
- **Suggested agent role**: `sdd-apply` (Tier 2).

### T-34 — Module README + observability hooks

- **Phase**: G3
- **Depends on**: B1..B7 done
- **Objective**: Write `docs/modules/program-progress/README.md` (Spanish, per AGENTS.md) describing endpoints, schema, and operational gotchas. Add structured logs (`Program.CompleteTask`, `Program.AdaptationApplied`).
- **Affected paths**:
  - `coppAddresdBack/docs/modules/program-progress/README.md` (new, Spanish per repo convention)
  - `src/CoppAddresd.Application/Features/ProgramProgress/**/Handler.cs` (add `_logger.LogInformation("Program.CompleteTask ...")` calls)
- **Implementation notes**: README mirrors the structure of `docs/modules/telemedicine/README.md`. Logs are JSON-formatted, no PII.
- **Acceptance criteria**: README reviewed; logs visible in dev.
- **Verification**: manual review.
- **Suggested agent role**: `sdd-apply` (Tier 2).

---

## Gate G3 — Final verification

| Gate | Owner | Trigger | Exit |
|------|-------|---------|------|
| **G3** | Backend reviewer + mobile reviewer | All B1..B16 merged | All SPEC §10.2 acceptance scenarios pass; performance budgets met; module README reviewed; signed off |

---

## Dependencies map (visual)

```
T-01 (entities) ──▶ T-02 (configs) ──▶ T-03 (migration) ──▶ T-06 (apply)
                                       │
                                       └─▶ T-07 (repo) ──▶ T-08 (repo tests)
                                                              │
T-04 (Auth perms) ─▶ T-05 (seeder) ───────────────────┐       │
                                                       │       ▼
                                                       │   T-09 (commands) ──▶ T-11 (validators) ──▶ T-12 (handler tests)
                                                       │       │                                   │
                                                       │       └──────────── T-10 (queries/DTOs) ──┤
                                                       │                                       │
                                                       │                                       ▼
                                                       └────────────────────────────────▶ T-13 (API) ──▶ T-14 (auth tests)
                                                                                               │
                                                                                               ├─▶ T-15 (contract tests)
                                                                                               │
├─▶ T-35 (score entities) ──▶ T-36 (score migration + weights seeder) ──▶ T-37 (calculators + repo) ──▶ T-38 (GET/POST endpoints) ──▶ T-39 (score tests, AC-19..AC-22)
                                                                                                │                                                                                                          │
                                                                                                │                                                                                                          ├─▶ T-40 (mobile Evolución wire-up)
                                                                                                │                                                                                                          └─▶ T-41 (logs / docs hook)
                                                                                                │
                                                                                                ├─▶ T-42 (xp_rules entity+config+migration) ──▶ T-43 (award-path wiring + limits) ──▶ T-44 (GET/PUT xp-rules endpoints) ──▶ T-45 (rules seeder + docs)
                                                                                                │
                                                                                                ├─▶ T-46 (clinical_xp_reviews schema+migration) ──▶ T-47 (clinical award engine on /calculate) ──▶ T-48 (validation flow + totals exclusion) ──▶ T-49 (clinical seeder + docs)
                                                                                                │
                                                                                                ├─▶ T-50 (multiplier schema+migration) ──▶ T-51 (milestone engine + activation) ──▶ T-52 (patient multiplier in award path + snapshot) ──▶ T-53 (docs)
                                                                                                │
                                                                                                ├─▶ T-54 (streak config schema+migration) ──▶ T-55 (threshold maintenance) ──▶ T-56 (essential-only freeze + snapshot) ──▶ T-57 (seeder + docs)
                                                                                                │
├─▶ T-58 (habit schema+migration + fake fix) ──▶ T-59 (POST nutrition/log) ──▶ T-60 (weekly awards on /calculate) ──▶ T-61 (seeder + docs)
                                                                                                │
                                                                                                ├─▶ T-62 (nb-streak schema+migration) ──▶ T-63 (nb seeder) ──▶ T-64 (nb streak engine + milestones) ──▶ T-65 (nb snapshot + docs)
                                                                                                │
                                                                                                ├─▶ T-66 (notifications schema+migration) ──▶ T-67 (service + anti-spam) ──▶ T-68 (trigger wiring) ──▶ T-69 (endpoints + docs)
                                                                                                │
                                                                                                ├─▶ T-70 (weaknesses schema+migration) ──▶ T-71 (rules engine + detection service) ──▶ T-72 (trigger en /calculate + endpoints) ──▶ T-73 (docs + contrato AI weekly)
                                                                                                │
                                                                                                ├─▶ T-16 (mobile client) ──▶ T-17 (snapshot) ──▶ T-18 (complete)
                                                                                               │                                              │
                                                                                               │                                              ├─▶ T-21 (calendar)
                                                                                               │                                              └─▶ T-22 (path)
                                                                                               │
                                                                                               ├─▶ T-19 (ERP templates) ──▶ T-20 (ERP enrollments)
                                                                                               │
                                                                                               └─▶ T-23 (adaptation engine) ──▶ T-24 (decide) ──▶ T-25 (media prog) ──▶ T-26 (ERP adaptations) ──▶ T-27 (mobile toast)

P3: T-28, T-29, T-30, T-31 (after P1+G2 stable)
G3: T-32, T-33, T-34 (after P1+P2 stable)
```

---

## Suggested agent role cheat-sheet

| Role | Tier | Use for |
|------|------|---------|
| `cheap-gate` | 0 | T-06, single mechanical checks |
| `sdd-explore` | 1 | (rare) investigate weird Postgres errors, fuzzy reverse-engineering |
| `sdd-apply` | 2 | All implementation tasks T-01..T-73 |
| `sdd-verify` | 2 | Could be combined with `sdd-apply` for T-08, T-12, T-14, T-15, T-32, T-33 |
| Reviewer (human / Tier 3) | 3 | G1, G2, G3 gates |

---

## Definition of Done per task

A task is DONE when:

1. All affected paths compile (`dotnet build` for backend; `npm run build` for mobile; `npm run lint` for both).
2. The acceptance criteria above pass.
3. The verification command in the task returns the expected output.
4. SPEC.md and PLAN.md references are still accurate (no inconsistencies introduced).
5. Audit rows are visible for any state-changing operation (when applicable).
6. The PR description cites the task ID (`T-XX`) and the SPEC §X acceptance scenario(s).
