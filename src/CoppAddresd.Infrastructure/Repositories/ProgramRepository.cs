using System.Text.Json;
using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Weaknesses;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Repositorio del módulo Progreso del Programa. Implementa el contrato
/// <see cref="IProgramRepository"/> sobre PostgreSQL (schema <c>app</c>).
///
/// Decisiones de implementación (TASKS T-07):
/// - Completar una tarea es una transacción única con la fila de la inscripción
///   bloqueada <c>FOR UPDATE</c>, envuelta en <c>CreateExecutionStrategy()</c>
///   (NpgsqlRetryingExecutionStrategy no soporta transacciones manuales fuera
///   de su unidad retriable; mismo patrón que <c>WellnessRepository</c>).
/// - Idempotencia por <c>(enrollment_id, local_date, task_code)</c> (índice
///   único) + clave <c>client_request_id</c>: replay nunca otorga XP dos veces.
/// - Racha/congelamientos se actualizan con <c>ExecuteUpdate</c> (evita el
///   pitfall de tracking de navegaciones documentado en AGENTS.md).
/// - Toda lectura es <c>AsNoTracking</c>; solo la inscripción bloqueada se
///   rastrea durante <c>CompleteTaskAsync</c>.
/// - Matemática de semanas/días en fecha local del paciente: las fechas llegan
///   ya en zona del paciente; el repositorio solo hace aritmética de
///   <see cref="DateOnly"/> (SPEC §6.11).
///
/// Motor de puntajes (TASKS T-37, SPEC §13): los calculadores
/// (<see cref="IHealthScoreCalculator"/> / <see cref="ITransformationScoreCalculator"/>)
/// son funciones puras sobre los datos de ventana que este repositorio reúne;
/// aquí vive TODO el acceso a BD (lecturas set-based sin N+1 + persistencia
/// compute-on-read). Los parámetros son opcionales para no romper los call
/// sites de tests que construyen el repositorio sin DI.
/// </summary>
public sealed class ProgramRepository(
    AppDbContext dbContext,
    IConfiguration configuration,
    IHealthScoreCalculator? healthScoreCalculator = null,
    ITransformationScoreCalculator? transformationScoreCalculator = null,
    IGamifiedNotificationService? gamifiedNotificationService = null,
    IProgramContentResolver? programContentResolver = null,
    IProgramAdaptationEngine? programAdaptationEngine = null
) : IProgramRepository
{
    private const int MaxFreezes = 3;

    private readonly IHealthScoreCalculator _healthScoreCalculator =
        healthScoreCalculator
        ?? new HealthScoreCalculator(NullLogger<HealthScoreCalculator>.Instance);

    private readonly ITransformationScoreCalculator _transformationScoreCalculator =
        transformationScoreCalculator
        ?? new TransformationScoreCalculator(NullLogger<TransformationScoreCalculator>.Instance);

    /// <summary>
    /// Notificaciones gamificadas (SPEC §20, "Paso 7b"): opcional para no romper
    /// los call sites de tests que construyen el repositorio sin DI (mismo
    /// patrón que los calculadores). Null → las notificaciones se omiten sin
    /// error; el servicio es best-effort y nunca lanza (AC-42).
    /// </summary>
    private readonly IGamifiedNotificationService? _gamifiedNotificationService =
        gamifiedNotificationService;

    /// <summary>
    /// Resolvedor de contenido del programa (T-74/T-75/T-76): determina el plan
    /// de alimentación activo y la rutina de ejercicio activa para un paciente
    /// en una fecha local. Opcional para no romper call sites de tests.
    /// </summary>
    private readonly IProgramContentResolver? _programContentResolver = programContentResolver;

    /// <summary>
    /// Motor de reglas de adaptación (SPEC §6.8, T-23): funciones puras y
    /// deterministas evaluadas tras cada completación dentro de la misma
    /// transacción. Opcional para no romper call sites de tests; sin DI cae
    /// a la implementación real sin estado.
    /// </summary>
    private readonly IProgramAdaptationEngine _programAdaptationEngine =
        programAdaptationEngine ?? new ProgramAdaptationEngine();

    /// <summary>Cada N días perfectos consecutivos se otorga un congelamiento (OQ-3, default 7).</summary>
    private readonly int _freezeGrantEveryPerfectDays =
        int.TryParse(
            configuration["Program:Streak:FreezeGrantEveryPerfectDays"],
            out var configured
        )
        && configured > 0
            ? configured
            : 7;

    /// <summary>
    /// Umbral de significancia de la XP clínica (SPEC §15, C): |Δ%| favorable
    /// mayor o igual a este valor crea una revisión clínica <c>pending</c> (en
    /// vez de auto-otorgar CLINICAL_IMPROVE). Configurable vía
    /// <c>Program:ClinicalXp:SignificantThresholdPct</c> (default 5).
    /// </summary>
    private readonly decimal _clinicalSignificantThresholdPct =
        decimal.TryParse(
            configuration["Program:ClinicalXp:SignificantThresholdPct"],
            out var configuredThreshold
        )
        && configuredThreshold > 0m
            ? configuredThreshold
            : 5m;

    // ---------------------------------------------------------------- Inscripciones

    public async Task<ProgramEnrollment> EnrollAsync(
        Guid patientId,
        Guid templateId,
        string timezone,
        DateOnly startLocalDate,
        Guid? createdBy = null,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var template =
                    await dbContext
                        .ProgramTemplates.AsNoTracking()
                        .Include(t => t.DayTemplates)
                        .FirstOrDefaultAsync(t => t.Id == templateId, ct)
                    ?? throw new NotFoundException($"Plantilla {templateId} no encontrada.");

                if (template.Status != TemplateStatus.Active)
                {
                    throw new BusinessRuleViolationException(
                        $"TEMPLATE_NOT_ACTIVE: la plantilla {template.Code} no está activa."
                    );
                }

                var now = DateTime.UtcNow;
                var enrollment = new ProgramEnrollment
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    TemplateId = templateId,
                    Timezone = timezone,
                    Status = ProgramEnrollmentStatus.Active,
                    StartedAt = now,
                    StartLocalDate = startLocalDate,
                    CurrentWeekNumber = 1,
                    CreatedBy = createdBy,
                    CreatedAt = now,
                };
                dbContext.ProgramEnrollments.Add(enrollment);

                dbContext.StreakStates.Add(
                    new StreakState
                    {
                        EnrollmentId = enrollment.Id,
                        CurrentStreak = 0,
                        LongestStreak = 0,
                        FreezesRemaining = 0,
                        FreezesUsedTotal = 0,
                    }
                );

                // Semanas 1..N: la 1 nace Active con su snapshot; el resto nacen
                // Locked con snapshot vacío que se toma al activar (SPEC §4.5:
                // ediciones de la plantilla aplican a la próxima semana).
                var snapshot = BuildSnapshot(template.DayTemplates);
                for (var weekNumber = 1; weekNumber <= template.TotalWeeks; weekNumber++)
                {
                    var start = startLocalDate.AddDays((weekNumber - 1) * 7);
                    dbContext.ProgramWeeks.Add(
                        new ProgramWeek
                        {
                            Id = Guid.NewGuid(),
                            EnrollmentId = enrollment.Id,
                            WeekNumber = weekNumber,
                            Status =
                                weekNumber == 1
                                    ? ProgramWeekStatus.Active
                                    : ProgramWeekStatus.Locked,
                            WeekStartDateLocal = start,
                            WeekEndDateLocal = start.AddDays(6),
                            TasksSnapshot = weekNumber == 1 ? snapshot : EmptySnapshot(),
                            TemplateVersionAtStart = template.Version,
                            ActivatedAt = weekNumber == 1 ? now : null,
                            CreatedAt = now,
                        }
                    );
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return enrollment;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync(ct);
                throw new BusinessRuleViolationException(
                    "PATIENT_ALREADY_ENROLLED: el paciente ya tiene una inscripción activa."
                );
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<ProgramEnrollment> PauseAsync(
        Guid enrollmentId,
        Guid? actorId = null,
        CancellationToken ct = default
    )
    {
        var now = DateTime.UtcNow;
        var updated = await dbContext
            .ProgramEnrollments.Where(e =>
                e.Id == enrollmentId && e.Status == ProgramEnrollmentStatus.Active
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(e => e.Status, ProgramEnrollmentStatus.Paused)
                        .SetProperty(e => e.PausedAt, now)
                        .SetProperty(e => e.UpdatedBy, actorId)
                        .SetProperty(e => e.UpdatedAt, now),
                ct
            );

        if (updated == 0)
        {
            await EnsureEnrollmentExistsAsync(enrollmentId, ct);
            throw new BusinessRuleViolationException(
                "INVALID_ENROLLMENT_STATE: solo se puede pausar una inscripción activa."
            );
        }

        return await dbContext
            .ProgramEnrollments.AsNoTracking()
            .SingleAsync(e => e.Id == enrollmentId, ct);
    }

    public async Task<ProgramEnrollment> ResumeAsync(
        Guid enrollmentId,
        Guid? actorId = null,
        CancellationToken ct = default
    )
    {
        var now = DateTime.UtcNow;
        var updated = await dbContext
            .ProgramEnrollments.Where(e =>
                e.Id == enrollmentId && e.Status == ProgramEnrollmentStatus.Paused
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(e => e.Status, ProgramEnrollmentStatus.Active)
                        .SetProperty(e => e.PausedAt, (DateTime?)null)
                        .SetProperty(e => e.UpdatedBy, actorId)
                        .SetProperty(e => e.UpdatedAt, now),
                ct
            );

        if (updated == 0)
        {
            await EnsureEnrollmentExistsAsync(enrollmentId, ct);
            throw new BusinessRuleViolationException(
                "INVALID_ENROLLMENT_STATE: solo se puede reanudar una inscripción pausada."
            );
        }

        return await dbContext
            .ProgramEnrollments.AsNoTracking()
            .SingleAsync(e => e.Id == enrollmentId, ct);
    }

    public async Task<ProgramEnrollment> WithdrawAsync(
        Guid enrollmentId,
        Guid? actorId = null,
        CancellationToken ct = default
    )
    {
        var now = DateTime.UtcNow;
        var updated = await dbContext
            .ProgramEnrollments.Where(e =>
                e.Id == enrollmentId
                && e.Status != ProgramEnrollmentStatus.Completed
                && e.Status != ProgramEnrollmentStatus.Withdrawn
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(e => e.Status, ProgramEnrollmentStatus.Withdrawn)
                        .SetProperty(e => e.WithdrawnAt, now)
                        .SetProperty(e => e.UpdatedBy, actorId)
                        .SetProperty(e => e.UpdatedAt, now),
                ct
            );

        if (updated == 0)
        {
            await EnsureEnrollmentExistsAsync(enrollmentId, ct);
            throw new BusinessRuleViolationException(
                "INVALID_ENROLLMENT_STATE: la inscripción ya está retirada o completada."
            );
        }

        return await dbContext
            .ProgramEnrollments.AsNoTracking()
            .SingleAsync(e => e.Id == enrollmentId, ct);
    }

    // ---------------------------------------------------------------- Lecturas de inscripciones

    public async Task<ProgramEnrollmentDto?> GetEnrollmentAsync(
        Guid enrollmentId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => new ProgramEnrollmentDto(
                e.Id,
                e.PatientId,
                e.TemplateId,
                e.Timezone,
                e.Status,
                e.StartedAt,
                e.StartLocalDate,
                e.CurrentWeekNumber,
                (int?)e.Template!.TotalWeeks ?? 0,
                (int?)
                    e
                        .XpLedgerEntries.Where(x =>
                            x.ValidatedBy != null || x.Rule == null || !x.Rule.RequiresValidation
                        )
                        .OrderByDescending(x => x.BalanceAfter)
                        .Select(x => (int?)x.BalanceAfter)
                        .FirstOrDefault()
                    ?? 0,
                (int?)e.StreakState!.CurrentStreak ?? 0,
                (int?)e.StreakState.LongestStreak ?? 0,
                (int?)e.StreakState.FreezesRemaining ?? 0,
                e.CompletedAt,
                e.PausedAt,
                e.WithdrawnAt,
                e.CreatedAt,
                e.Patient != null ? (e.Patient.FirstName + " " + e.Patient.LastName).Trim() : null,
                e.Patient != null
                    ? (e.Patient.DocumentNumber ?? e.Patient.MedicalRecordNumber)
                    : null,
                e.Template != null ? e.Template.Name : null
            ))
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<ProgramEnrollmentDto> Items, int Total)> ListEnrollmentsAsync(
        Guid? patientId,
        ProgramEnrollmentStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.ProgramEnrollments.AsNoTracking().AsQueryable();

        if (patientId.HasValue)
        {
            query = query.Where(e => e.PatientId == patientId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new ProgramEnrollmentDto(
                e.Id,
                e.PatientId,
                e.TemplateId,
                e.Timezone,
                e.Status,
                e.StartedAt,
                e.StartLocalDate,
                e.CurrentWeekNumber,
                (int?)e.Template!.TotalWeeks ?? 0,
                (int?)
                    e
                        .XpLedgerEntries.Where(x =>
                            x.ValidatedBy != null || x.Rule == null || !x.Rule.RequiresValidation
                        )
                        .OrderByDescending(x => x.BalanceAfter)
                        .Select(x => (int?)x.BalanceAfter)
                        .FirstOrDefault()
                    ?? 0,
                (int?)e.StreakState!.CurrentStreak ?? 0,
                (int?)e.StreakState.LongestStreak ?? 0,
                (int?)e.StreakState.FreezesRemaining ?? 0,
                e.CompletedAt,
                e.PausedAt,
                e.WithdrawnAt,
                e.CreatedAt,
                e.Patient != null ? (e.Patient.FirstName + " " + e.Patient.LastName).Trim() : null,
                e.Patient != null
                    ? (e.Patient.DocumentNumber ?? e.Patient.MedicalRecordNumber)
                    : null,
                e.Template != null ? e.Template.Name : null
            ))
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    // ---------------------------------------------------------------- Completación

    public async Task<CompleteTaskResult> CompleteTaskAsync(
        CompleteTaskInput input,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await CompleteTaskCoreAsync(input, ct);
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Backstop de concurrencia: otro request insertó la misma
                // completación mientras este esperaba el lock. Replay limpio.
                await transaction.RollbackAsync(ct);
                dbContext.ChangeTracker.Clear();
                var winner = await dbContext
                    .TaskCompletions.AsNoTracking()
                    .FirstOrDefaultAsync(
                        t =>
                            t.EnrollmentId == input.EnrollmentId
                            && t.LocalDate == input.LocalDate
                            && t.TaskCode == input.TaskCode,
                        ct
                    );
                if (winner is not null)
                {
                    return await BuildReplayResultAsync(input.EnrollmentId, winner, ct);
                }

                throw;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    private async Task<CompleteTaskResult> CompleteTaskCoreAsync(
        CompleteTaskInput input,
        CancellationToken ct
    )
    {
        // 1. Bloqueo pesimista de la inscripción + validación de estado.
        //    Npgsql 10 ya no expone .ForUpdate(); el lock se toma por SQL
        //    directo (SELECT ... FOR UPDATE) dentro de la transacción.
        var enrollment =
            await dbContext
                .ProgramEnrollments.FromSqlInterpolated(
                    $"SELECT * FROM app.program_enrollments WHERE id = {input.EnrollmentId} FOR UPDATE"
                )
                .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Inscripción {input.EnrollmentId} no encontrada.");

        if (enrollment.Status != ProgramEnrollmentStatus.Active)
        {
            throw new BusinessRuleViolationException(
                "ENROLLMENT_INACTIVE: la inscripción no está activa."
            );
        }

        var template =
            await dbContext
                .ProgramTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == enrollment.TemplateId, ct)
            ?? throw new NotFoundException($"Plantilla {enrollment.TemplateId} no encontrada.");

        // 2. Idempotencia (app-layer, dentro del lock): misma (inscripción, fecha, tarea).
        var existing = await dbContext
            .TaskCompletions.AsNoTracking()
            .FirstOrDefaultAsync(
                t =>
                    t.EnrollmentId == input.EnrollmentId
                    && t.LocalDate == input.LocalDate
                    && t.TaskCode == input.TaskCode,
                ct
            );
        if (existing is not null)
        {
            return await BuildReplayResultAsync(input.EnrollmentId, existing, ct);
        }

        // 2b. Clave de idempotencia reutilizada con otra tarea/fecha → AC-04.
        if (!string.IsNullOrWhiteSpace(input.ClientRequestId))
        {
            var sameKey = await dbContext
                .TaskCompletions.AsNoTracking()
                .FirstOrDefaultAsync(
                    t =>
                        t.EnrollmentId == input.EnrollmentId
                        && t.ClientRequestId == input.ClientRequestId,
                    ct
                );
            if (
                sameKey is not null
                && (sameKey.LocalDate != input.LocalDate || sameKey.TaskCode != input.TaskCode)
            )
            {
                var conflicted = await BuildReplayResultAsync(input.EnrollmentId, sameKey, ct);
                return conflicted with { Outcome = CompleteTaskOutcome.IdempotencyKeyReused };
            }
        }

        // 3. Semana del programa para la fecha (auto-activación por rollover).
        var weekday = ToIsoWeekday(input.LocalDate);
        var week = await dbContext.ProgramWeeks.FirstOrDefaultAsync(
            w =>
                w.EnrollmentId == enrollment.Id
                && w.WeekStartDateLocal <= input.LocalDate
                && input.LocalDate <= w.WeekEndDateLocal,
            ct
        );

        if (week is null)
        {
            throw new UnprocessableEntityException(
                "DATE_OUTSIDE_ACTIVE_WEEK: la fecha no pertenece a ninguna semana del programa."
            );
        }

        if (week.Status == ProgramWeekStatus.Completed)
        {
            throw new BusinessRuleViolationException(
                "TASK_NOT_SCHEDULED: la semana ya fue completada."
            );
        }

        if (week.Status == ProgramWeekStatus.Locked)
        {
            await ActivateWeekAsync(enrollment, template, week, input.LocalDate, ct);
        }

        // 4. La tarea debe estar en el snapshot de la semana (SPEC §6.1).
        var snapshotTasks = ParseSnapshot(week.TasksSnapshot);
        var scheduled = snapshotTasks.FirstOrDefault(t =>
            t.Weekday == weekday && t.TaskCode == input.TaskCode.ToString()
        );
        if (scheduled is null)
        {
            throw new BusinessRuleViolationException(
                $"TASK_NOT_SCHEDULED: la tarea {input.TaskCode} no está programada para el día {weekday}."
            );
        }

        // 5. Check-in diario (rollup).
        var checkin = await dbContext.DailyCheckIns.FirstOrDefaultAsync(
            c => c.EnrollmentId == enrollment.Id && c.LocalDate == input.LocalDate,
            ct
        );
        if (checkin is null)
        {
            checkin = new DailyCheckIn
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollment.Id,
                ProgramWeekId = week.Id,
                LocalDate = input.LocalDate,
                Weekday = weekday,
                MoodScore = input.MoodScore,
                Barriers = input.Barriers,
            };
            dbContext.DailyCheckIns.Add(checkin);
        }
        else
        {
            if (input.MoodScore.HasValue)
            {
                checkin.MoodScore = input.MoodScore;
            }

            if (input.Barriers is not null)
            {
                checkin.Barriers = input.Barriers;
            }
        }

        // 6. Registro emocional de primera clase (SPEC §3.10) para la tarea emocional.
        Guid? emotionalRecordId = null;
        if (input.TaskCode == TaskCode.emocional)
        {
            var emotional = await dbContext.EmotionalRecords.FirstOrDefaultAsync(
                r =>
                    r.ProgramEnrollmentId == enrollment.Id
                    && r.RecordedLocalDate == input.LocalDate,
                ct
            );
            if (emotional is null)
            {
                // SPEC §3.10: el registro emocional exige un mood_score real
                // (1..5, NOT NULL). El repositorio NUNCA fabrica datos
                // clínicos: sin moodScore la tarea emocional no puede
                // persistirse (el validador B4 lo exigirá client-side).
                if (!input.MoodScore.HasValue)
                {
                    throw new UnprocessableEntityException(
                        "MOOD_SCORE_REQUIRED: la tarea emocional requiere moodScore (1..5)."
                    );
                }

                emotional = new EmotionalRecord
                {
                    Id = Guid.NewGuid(),
                    PatientId = enrollment.PatientId,
                    ProgramEnrollmentId = enrollment.Id,
                    RecordedLocalDate = input.LocalDate,
                    MoodScore = input.MoodScore.Value,
                    Barriers = input.Barriers,
                };
                dbContext.EmotionalRecords.Add(emotional);
            }
            else
            {
                if (input.MoodScore.HasValue)
                {
                    emotional.MoodScore = input.MoodScore.Value;
                }

                if (input.Barriers is not null)
                {
                    emotional.Barriers = input.Barriers;
                }
            }

            // Save intermedio: la completación referencia el Id del registro.
            await dbContext.SaveChangesAsync(ct);
            emotionalRecordId = emotional.Id;
        }

        // 7b. T-76: resolver content FKs ANTES de persistir TaskCompletion.
        //     El resolvedor corre dentro de la transacción (consistente con el
        //     FOR UPDATE del enrollment). Si no hay resolver (tests) o no hay
        //     contenido activo, los FKs quedan null (el input los trae null
        //     por defecto). XP se otorga siempre (default MVP: sin contenido
        //     no bloquea la completación).
        Guid? resolvedNutritionPlanId = input.NutritionPlanId;
        short? resolvedNutritionPlanDayNumber = input.NutritionPlanDayNumber;
        Guid? resolvedExerciseRoutineId = input.ExerciseRoutineId;

        if (_programContentResolver is not null)
        {
            var resolution = await _programContentResolver.ResolveAsync(
                enrollment.PatientId,
                input.LocalDate,
                ct
            );

            if (resolution is { })
            {
                // nut: nutrition_plan_id y day_number del resolvedor.
                // Si el plan no tiene día para el weekday → ambos null (AC-13).
                resolvedNutritionPlanId = resolution.NutritionPlanId;
                resolvedNutritionPlanDayNumber = resolution.NutritionPlanDayNumber.HasValue
                    ? (short?)resolution.NutritionPlanDayNumber.Value
                    : null;

                // ejercicio: exercise_routine_id del resolvedor.
                resolvedExerciseRoutineId = resolution.ExerciseRoutineId;
            }
            else
            {
                // Sin contenido activo → FKs null.
                resolvedNutritionPlanId = null;
                resolvedNutritionPlanDayNumber = null;
                resolvedExerciseRoutineId = null;
            }
        }

        // 7. Completación (puntos congelados del snapshot; la regla del
        // catálogo puede ajustarlos en el paso 8).
        var now = DateTime.UtcNow;
        var completion = new TaskCompletion
        {
            Id = Guid.NewGuid(),
            EnrollmentId = enrollment.Id,
            ProgramWeekId = week.Id,
            DailyCheckinId = checkin.Id,
            LocalDate = input.LocalDate,
            Weekday = weekday,
            TaskCode = input.TaskCode,
            PointsAwarded = 0, // se fija en el paso 8 con la resolución de la regla
            ClientRequestId = input.ClientRequestId,
            CompletedAt = now,
            ClientCompletedAt = input.ClientCompletedAt,
            SourceRefType = input.SourceRefType,
            ContentFingerprint = input.ContentFingerprint,
            // T-76: FKs de contenido resueltos por el resolvedor (o null si no
            // hay contenido activo). Si el input ya traía valores, se preservan
            // (backwards-compatible); si el resolvedor los calculó, prevalecen.
            NutritionPlanId = resolvedNutritionPlanId,
            NutritionPlanDayNumber = resolvedNutritionPlanDayNumber,
            ExerciseRoutineId = resolvedExerciseRoutineId,
            MediaId = input.MediaId,
            VitalSignsBatchId = input.VitalSignsBatchId,
            NutribioticProductId = input.NutribioticProductId,
            EmotionalRecordId = emotionalRecordId,
        };
        dbContext.TaskCompletions.Add(completion);

        // 8. XP exactamente una vez (SPEC §6.4). La regla del catálogo se
        // resuelve ANTES de otorgar (SPEC §14.3): con regla TASK_* activa y
        // vigente → puntos = regla.BaseXp ?? puntos del snapshot (las TASK_*
        // tienen BaseXp null → se conservan los puntos de la plantilla), se
        // aplica el multiplier y se verifican los topes MaxPerDay/MaxPerWeek.
        // Sin regla o regla inactiva/vencida → fallback al comportamiento
        // actual (puntos del snapshot, sin límites, rule_code null).
        var (taskPoints, taskRuleCode, taskMultiplierUsed) = await ResolveXpAwardAsync(
            enrollment.Id,
            enrollment.Timezone,
            XpRuleCodes.ForTask(input.TaskCode),
            scheduled.Points,
            input.LocalDate,
            ct
        );

        completion.PointsAwarded = taskPoints;

        var balance = await CurrentBalanceAsync(enrollment.Id, ct);
        var newBalance = balance + taskPoints;
        dbContext.XpLedgerEntries.Add(
            new XpLedgerEntry
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollment.Id,
                Amount = taskPoints,
                Reason = XpReason.TaskCompletion,
                SourceRefType = input.SourceRefType,
                SourceRefId = completion.Id,
                RuleCode = taskRuleCode,
                MultiplierUsed = taskMultiplierUsed,
                BalanceAfter = newBalance,
                AwardedAt = now,
            }
        );

        await dbContext.SaveChangesAsync(ct);

        // 9. Día perfecto: todas las tareas programadas del día completadas
        //    (SPEC §6.3); el bonus se otorga una única vez (dedupe parcial de
        //    xp_ledger con source_ref_id = daily_checkin.id).
        var scheduledForDay = snapshotTasks.Where(t => t.Weekday == weekday).ToList();
        var completedForDay = await dbContext
            .TaskCompletions.AsNoTracking()
            .Where(t => t.EnrollmentId == enrollment.Id && t.LocalDate == input.LocalDate)
            .Select(t => t.TaskCode.ToString())
            .ToListAsync(ct);

        var isPerfect =
            scheduledForDay.Count > 0
            && scheduledForDay.All(t => completedForDay.Contains(t.TaskCode));

        // Umbral de racha configurable (SPEC §17, B): un día "cumple el umbral"
        // si completó al menos <c>template.StreakMinTasks</c> tareas (default 1
        // → cualquier día con ≥1 tarea mantiene la racha; con umbral 3, un día
        // de 2 tareas no la mantiene). El día perfecto sigue siendo "todas las
        // tareas programadas" y se usa sin cambios para el bonus y la concesión
        // de congelamientos (SPEC §6.3/§6.6).
        var meetsThreshold = completedForDay.Count >= template.StreakMinTasks;

        checkin.TotalPoints += taskPoints;
        checkin.UpdatedAt = now;

        var dailyBonus = 0;

        // Estado de racha tras el día (0/false si hoy no cumplió el umbral):
        // lo usa el motor de hitos (SPEC §16, B) solo cuando la racha creció.
        (var newStreak, var streakGrewToday) = (0, false);

        if (isPerfect && !checkin.IsPerfectDay)
        {
            // SPEC §14: el bonus de día perfecto también pasa por el catálogo
            // (regla DAY_BONUS, base 50 por defecto si no existe/está inactiva).
            var (bonusPoints, bonusRuleCode, bonusMultiplierUsed) = await ResolveXpAwardAsync(
                enrollment.Id,
                enrollment.Timezone,
                XpRuleCodes.DayBonus,
                50,
                input.LocalDate,
                ct
            );

            checkin.IsPerfectDay = true;
            checkin.BonusAwarded = bonusPoints;
            newBalance += bonusPoints;
            dailyBonus = bonusPoints;

            dbContext.XpLedgerEntries.Add(
                new XpLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    EnrollmentId = enrollment.Id,
                    Amount = bonusPoints,
                    Reason = XpReason.DailyBonus,
                    SourceRefType = "DailyBonus",
                    SourceRefId = checkin.Id,
                    RuleCode = bonusRuleCode,
                    MultiplierUsed = bonusMultiplierUsed,
                    BalanceAfter = newBalance,
                    AwardedAt = now,
                }
            );

            // Notificación gamificada de día perfecto (SPEC §20, C.5): best-
            // effort — el servicio nunca lanza ni rompe la transacción (AC-42).
            if (_gamifiedNotificationService is not null)
            {
                await _gamifiedNotificationService.NotifyAsync(
                    enrollment.PatientId,
                    "day_complete",
                    "✅ Día perfecto",
                    $"✅ Día perfecto · +{bonusPoints} XP",
                    "normal",
                    ct
                );
            }
        }

        // Racha por umbral (SPEC §17, B): el día aporta a la racha si cumple el
        // umbral (antes: solo días perfectos, SPEC §6.6). Re-ejecutar el mismo
        // día es no-op (<see cref="UpdateStreakAsync"/> guarda
        // last_active_date == hoy).
        if (meetsThreshold)
        {
            (newStreak, streakGrewToday) = await UpdateStreakAsync(
                enrollment.Id,
                input.LocalDate,
                template.EssentialTaskCodes,
                ct
            );
        }

        // Flush del check-in recién actualizado: MaybeCompleteWeekAsync consulta
        // la BD (la query de conteo no materializa entidades, no hay fix-up del
        // change tracker) y el día perfecto de HOY debe estar visible.
        await dbContext.SaveChangesAsync(ct);

        // Hito de racha (SPEC §16, B): si la racha creció hoy hasta un día
        // hito (7/11/22/50) se otorga la XP del hito UNA vez por inscripción
        // (dedupe parcial del libro mayor) y se activa el multiplicador x2
        // (hitos 11/22/50). Corre DESPUÉS del flush para que el balance del
        // libro mayor incluya el bonus de día perfecto recién escrito.
        if (streakGrewToday && newStreak > 0)
        {
            await AwardStreakMilestoneIfReachedAsync(
                enrollment.Id,
                enrollment.PatientId,
                enrollment.Timezone,
                newStreak,
                input.LocalDate,
                ct
            );
        }

        // Racha propia del nutribiótico (SPEC §19, B — "Paso 7a"): la tarea
        // nutribiotico mantiene su PROPIA racha consecutiva, independiente de
        // la racha general (un día perdido la rompe; los congelamientos NO la
        // protegen — AC-38). Corre SOLO en la primera escritura de la tarea
        // (el replay idempotente ya retornó arriba), dentro de la transacción
        // con la inscripción bloqueada FOR UPDATE, y después del flush para
        // que el balance del libro mayor incluya la tarea y el bonus de día.
        if (input.TaskCode == TaskCode.nutribiotico)
        {
            await UpdateNbStreakAsync(
                enrollment.Id,
                enrollment.PatientId,
                enrollment.Timezone,
                input.LocalDate,
                completion.Id,
                ct
            );
        }

        // 10. Semana perfecta → avance (SPEC §6.7).
        await MaybeCompleteWeekAsync(enrollment, template, week, ct);

        await dbContext.SaveChangesAsync(ct);

        // Notificación gamificada de subida de nivel (SPEC §20, C.4): se
        // compara el nivel ANTES y DESPUÉS de todos los otorgamientos del día
        // (tarea + bonus de día perfecto + hitos de racha/nutribiótico). Corre
        // DESPUÉS del flush para que <see cref="CurrentBalanceAsync"/> vea el
        // libro mayor completo (las filas recién añadidas no están en BD hasta
        // el SaveChanges). Best-effort — nunca lanza (AC-42).
        if (_gamifiedNotificationService is not null)
        {
            var levelBefore = XpLevels.ForBalance(balance).Level;
            var levelAfter = XpLevels
                .ForBalance(await CurrentBalanceAsync(enrollment.Id, ct))
                .Level;
            if (levelBefore != levelAfter)
            {
                await _gamifiedNotificationService.NotifyAsync(
                    enrollment.PatientId,
                    "level_up",
                    $"⭐ ¡Nivel {levelAfter}!",
                    $"⭐ ¡Subiste a Nivel {levelAfter}!",
                    "high",
                    ct
                );
            }
        }

        // Motor de adaptaciones (SPEC §6.8, T-23): reglas deterministas
        // evaluadas tras cada completación dentro de la misma transacción.
        // El balance "anterior" es el previo a esta escritura; el actual lo
        // relee el motor (incluye tarea + bonus + hitos del mismo flujo).
        await EvaluateAdaptationsAsync(enrollment.Id, input.LocalDate, balance, ct);

        var streak = await dbContext
            .StreakStates.AsNoTracking()
            .FirstOrDefaultAsync(s => s.EnrollmentId == enrollment.Id, ct);

        return new CompleteTaskResult(
            CompleteTaskOutcome.Created,
            completion.Id,
            taskPoints,
            newBalance,
            checkin.IsPerfectDay,
            dailyBonus,
            streak?.CurrentStreak ?? 0,
            streak?.FreezesRemaining ?? 0,
            checkin.TotalPoints + checkin.BonusAwarded,
            // Máximo del día: misma computación que el replay (SPEC §7.2: el
            // body del replay debe ser idéntico a la primera escritura).
            ComputeDayPointsMax(scheduledForDay)
        );
    }

    // ---------------------------------------------------------------- Racha

    /// <summary>
    /// Actualiza la racha al completar un día que cumple el umbral (SPEC §17, B):
    /// - Día consecutivo al último activo → <c>current_streak + 1</c>.
    /// - Hueco (días perdidos) con congelamiento disponible Y una tarea
    ///   esencial completada en el día perdido → consume uno (racha intacta,
    ///   <c>last_active_date = hoy</c>). Regla "el rescate requiere una tarea
    ///   esencial" (SPEC §17, C, referencia ADRED): sin tarea esencial el
    ///   congelamiento NO se consume (queda en inventario) y la racha se rompe
    ///   (AC-32).
    /// - Hueco sin congelamiento → <c>current_streak = 0</c> y
    ///   <c>last_break_date = ayer</c> (el día que quebró la racha).
    /// Otorga 1 congelamiento cada <c>FreezeGrantEveryPerfectDays</c> días
    /// perfectos consecutivos (tope 3, OQ-3) — sin cambios (SPEC §17, C).
    /// Los writes de racha usan <c>ExecuteUpdate</c> para no pisar FKs vía
    /// tracking de navegaciones. Devuelve el valor nuevo de
    /// <c>current_streak</c> y si creció HOY (lo usa el motor de hitos de
    /// racha, SPEC §16, B: solo un día que crece puede alcanzar un hito).
    /// </summary>
    private async Task<(int Current, bool GrewToday)> UpdateStreakAsync(
        Guid enrollmentId,
        DateOnly today,
        IReadOnlyList<string> essentialTaskCodes,
        CancellationToken ct
    )
    {
        var streak = await dbContext
            .StreakStates.AsNoTracking()
            .FirstOrDefaultAsync(s => s.EnrollmentId == enrollmentId, ct);

        if (streak is null)
        {
            dbContext.StreakStates.Add(
                new StreakState
                {
                    EnrollmentId = enrollmentId,
                    CurrentStreak = 1,
                    LongestStreak = 1,
                    LastActiveDate = today,
                }
            );
            return (1, true);
        }

        var current = streak.CurrentStreak;
        var longest = streak.LongestStreak;
        var lastActive = streak.LastActiveDate;
        var freezesRemaining = streak.FreezesRemaining;
        var freezesUsedTotal = streak.FreezesUsedTotal;
        DateOnly? lastBreak = streak.LastBreakDate;
        var streakGrewToday = false;

        if (lastActive is null)
        {
            current = 1;
            streakGrewToday = true;
        }
        else if (today == lastActive.Value.AddDays(1))
        {
            current += 1;
            streakGrewToday = true;
        }
        else if (today == lastActive.Value)
        {
            return (current, false); // mismo día: no hay nada que actualizar
        }
        else
        {
            var missedDay = today.AddDays(-1);

            // Rescate con congelamiento (SPEC §17, C): solo si el día perdido
            // completó al menos una tarea esencial de la plantilla. Sin tarea
            // esencial el congelamiento NO se consume (permanece en inventario)
            // y la racha se rompe (AC-32).
            if (
                freezesRemaining > 0
                && await HadEssentialTaskAsync(enrollmentId, missedDay, essentialTaskCodes, ct)
            )
            {
                freezesRemaining -= 1;
                freezesUsedTotal += 1;
                dbContext.StreakFreezes.Add(
                    new StreakFreeze
                    {
                        Id = Guid.NewGuid(),
                        EnrollmentId = enrollmentId,
                        Kind = StreakFreezeKind.Consumed,
                        UsedOnLocalDate = missedDay,
                        GrantedReason = "StreakFreeze",
                        CreatedAt = DateTime.UtcNow,
                    }
                );
                // La racha se preserva (no se incrementa hoy tras un quiebre).
            }
            else
            {
                current = 0;
                lastBreak = missedDay;
            }
        }

        lastActive = today;

        if (current > longest)
        {
            longest = current;
        }

        // Otorga 1 congelamiento al alcanzar un múltiplo de N días perfectos
        // CONSECUTIVOS, solo si la racha creció HOY (SPEC §6.6: "cada 7 días
        // perfectos consecutivos"). Si un congelamiento se consumió preservando
        // la racha en un múltiplo (AC-09), no se re-otorga en el mismo valor:
        // el inventario sí decrementa.
        if (
            streakGrewToday
            && current > 0
            && current % _freezeGrantEveryPerfectDays == 0
            && freezesRemaining < MaxFreezes
        )
        {
            freezesRemaining += 1;
            dbContext.StreakFreezes.Add(
                new StreakFreeze
                {
                    Id = Guid.NewGuid(),
                    EnrollmentId = enrollmentId,
                    Kind = StreakFreezeKind.Granted,
                    GrantedAt = DateTime.UtcNow,
                    GrantedReason = "PerfectWeekBonus",
                    CreatedAt = DateTime.UtcNow,
                }
            );
        }

        await dbContext
            .StreakStates.Where(s => s.EnrollmentId == enrollmentId)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.CurrentStreak, current)
                        .SetProperty(x => x.LongestStreak, longest)
                        .SetProperty(x => x.LastActiveDate, lastActive)
                        .SetProperty(x => x.FreezesRemaining, freezesRemaining)
                        .SetProperty(x => x.FreezesUsedTotal, freezesUsedTotal)
                        .SetProperty(x => x.LastBreakDate, lastBreak)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                ct
            );

        return (current, streakGrewToday);
    }

    /// <summary>
    /// Regla "el rescate requiere una tarea esencial" (SPEC §17, C, referencia
    /// ADRED): un día bajo el umbral solo puede rescatarse con un congelamiento
    /// si se completó al menos una tarea cuyo código está en el catálogo
    /// esencial de la plantilla (default <c>nut</c> / <c>ejercicio</c> /
    /// <c>nutribiotico</c>). Lista esencial vacía = sin restricción
    /// (comportamiento previo: cualquier día perdido puede rescatarse con
    /// congelamiento).
    /// </summary>
    private async Task<bool> HadEssentialTaskAsync(
        Guid enrollmentId,
        DateOnly localDate,
        IReadOnlyList<string> essentialTaskCodes,
        CancellationToken ct
    )
    {
        if (essentialTaskCodes.Count == 0)
        {
            return true;
        }

        return await dbContext
            .TaskCompletions.AsNoTracking()
            .AnyAsync(
                t =>
                    t.EnrollmentId == enrollmentId
                    && t.LocalDate == localDate
                    && essentialTaskCodes.Contains(t.TaskCode.ToString()),
                ct
            );
    }

    // ---------------------------------------------------------------- Semanas

    /// <summary>
    /// Activa una semana Locked cuyo rango contiene la fecha (rollover por
    /// fecha, default OQ-5). Marca Completed las semanas anteriores vencidas
    /// (terminaron sin semana perfecta, SPEC §6.7) y toma el snapshot desde la
    /// plantilla vigente (AC-15: las ediciones de plantilla aplican a la
    /// próxima semana, no a la actual).
    /// </summary>
    private async Task ActivateWeekAsync(
        ProgramEnrollment enrollment,
        ProgramTemplate template,
        ProgramWeek week,
        DateOnly localDate,
        CancellationToken ct
    )
    {
        // Rollover de semanas vencidas (end < fecha): se completan TODAS las
        // anteriores sin importar su estado (Locked o Active). La semana 1 nace
        // Active (SPEC §5.2) y una semana imperfecta debe igualmente completarse
        // al avanzar (SPEC §6.7); el filtro anterior (solo Locked) dejaba la
        // semana 1 colgada en Active y bloqueaba el avance con
        // DATE_OUTSIDE_ACTIVE_WEEK. Las ya Completed se excluyen (idempotencia).
        await dbContext
            .ProgramWeeks.Where(w =>
                w.EnrollmentId == enrollment.Id
                && w.WeekNumber < week.WeekNumber
                && w.WeekEndDateLocal < localDate
                && w.Status != ProgramWeekStatus.Completed
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(w => w.Status, ProgramWeekStatus.Completed)
                        .SetProperty(w => w.CompletedAt, DateTime.UtcNow)
                        .SetProperty(w => w.UpdatedAt, DateTime.UtcNow),
                ct
            );

        // Defensa: solo cuentan como pendientes las semanas anteriores cuyo
        // rango aún no venció (WeekEndDateLocal >= fecha). Con semanas
        // contiguas esto no ocurre; protege contra saltos de fecha inválidos.
        var priorIncomplete = await dbContext.ProgramWeeks.AnyAsync(
            w =>
                w.EnrollmentId == enrollment.Id
                && w.WeekNumber < week.WeekNumber
                && w.WeekEndDateLocal >= localDate
                && w.Status != ProgramWeekStatus.Completed,
            ct
        );
        if (priorIncomplete)
        {
            throw new UnprocessableEntityException(
                "DATE_OUTSIDE_ACTIVE_WEEK: la fecha corresponde a una semana futura aún bloqueada."
            );
        }

        var now = DateTime.UtcNow;
        week.Status = ProgramWeekStatus.Active;
        week.ActivatedAt = now;
        week.TasksSnapshot = await BuildSnapshotFromTemplateAsync(template.Id, ct);
        week.TemplateVersionAtStart = template.Version;
        week.UpdatedAt = now;

        enrollment.CurrentWeekNumber = week.WeekNumber;
        enrollment.UpdatedAt = now;
    }

    /// <summary>
    /// Si los 7 días de la semana quedaron perfectos, la semana pasa a
    /// Completed, el puntero avanza y la siguiente se activa (SPEC §6.7);
    /// en la última semana la inscripción se completa.
    /// </summary>
    private async Task MaybeCompleteWeekAsync(
        ProgramEnrollment enrollment,
        ProgramTemplate template,
        ProgramWeek week,
        CancellationToken ct
    )
    {
        var perfectWeekdays = await dbContext
            .DailyCheckIns.AsNoTracking()
            .Where(c => c.ProgramWeekId == week.Id && c.IsPerfectDay)
            .Select(c => c.Weekday)
            .Distinct()
            .CountAsync(ct);

        if (perfectWeekdays < 7)
        {
            return;
        }

        var now = DateTime.UtcNow;
        week.Status = ProgramWeekStatus.Completed;
        week.CompletedAt = now;
        week.UpdatedAt = now;

        if (week.WeekNumber < template.TotalWeeks)
        {
            var nextWeek = await dbContext.ProgramWeeks.FirstOrDefaultAsync(
                w => w.EnrollmentId == enrollment.Id && w.WeekNumber == week.WeekNumber + 1,
                ct
            );
            if (nextWeek is not null)
            {
                nextWeek.Status = ProgramWeekStatus.Active;
                nextWeek.ActivatedAt = now;
                nextWeek.TasksSnapshot = await BuildSnapshotFromTemplateAsync(template.Id, ct);
                nextWeek.TemplateVersionAtStart = template.Version;
                nextWeek.UpdatedAt = now;
                enrollment.CurrentWeekNumber = nextWeek.WeekNumber;
            }
        }
        else
        {
            enrollment.Status = ProgramEnrollmentStatus.Completed;
            enrollment.CompletedAt = now;
        }

        enrollment.UpdatedAt = now;
    }

    // ---------------------------------------------------------------- Lecturas

    public async Task<ProgramSnapshotDto?> GetSnapshotAsync(
        Guid enrollmentId,
        DateOnly todayLocalDate,
        CancellationToken ct = default
    )
    {
        // Proyección única: inscripción + plantilla + racha + balance (sin N+1).
        var row = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => new
            {
                e.Id,
                e.PatientId,
                e.StartLocalDate,
                e.CurrentWeekNumber,
                TemplateId = e.Template!.Id,
                TemplateCode = e.Template.Code,
                TemplateName = e.Template.Name,
                TemplateTotalWeeks = e.Template.TotalWeeks,
                // Umbral de racha + tareas esenciales de la plantilla (SPEC §17, D):
                // campos aditivos del snapshot para la UI del móvil.
                TemplateStreakMinTasks = e.Template.StreakMinTasks,
                TemplateEssentialTaskCodes = e.Template.EssentialTaskCodes,
                StreakCurrent = (int?)e.StreakState!.CurrentStreak,
                StreakLongest = (int?)e.StreakState.LongestStreak,
                FreezesRemaining = (int?)e.StreakState.FreezesRemaining,
                MultiplierActive = (decimal?)e.StreakState.MultiplierActive,
                MultiplierEndsAt = e.StreakState.MultiplierEndsAt,
                // Racha propia del nutribiótico (SPEC §19, D): campos aditivos
                // del snapshot para la UI del móvil (Paso 7a).
                NbStreak = (int?)e.StreakState.NbCurrentStreak,
                NbLongestStreak = (int?)e.StreakState.NbLongestStreak,
                XpBalance = e
                    .XpLedgerEntries
                    // Excluye las XP pendientes de validación (SPEC §15, E): una
                    // fila cuya regla requiere_validation = true y aún no tiene
                    // validated_by no cuenta en el total hasta aprobarse. Las
                    // CLINICAL_IMPROVE/STABLE/ALL_UP tienen validated_by null
                    // pero requires_validation = false → sí cuentan.
                    .Where(x =>
                        x.ValidatedBy != null || x.Rule == null || !x.Rule.RequiresValidation
                    )
                    .OrderByDescending(x => x.BalanceAfter)
                    .Select(x => (int?)x.BalanceAfter)
                    .FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        var week = await dbContext
            .ProgramWeeks.AsNoTracking()
            .FirstOrDefaultAsync(
                w => w.EnrollmentId == enrollmentId && w.WeekNumber == row.CurrentWeekNumber,
                ct
            );
        if (week is null)
        {
            return null;
        }

        var weekday = ToIsoWeekday(todayLocalDate);
        var snapshotTasks = ParseSnapshot(week.TasksSnapshot);
        var todayScheduled = snapshotTasks
            .Where(t => t.Weekday == weekday)
            .OrderBy(t => t.SortOrder)
            .ToList();

        var todayCompletions = await dbContext
            .TaskCompletions.AsNoTracking()
            .Where(t => t.EnrollmentId == enrollmentId && t.LocalDate == todayLocalDate)
            .Select(t => new
            {
                t.TaskCode,
                t.CompletedAt,
                t.MediaId,
            })
            .ToListAsync(ct);

        // Contenido multimedia del snapshot (SPEC §7.1): título/duración/
        // miniatura de app.media_items para los mediaId de las tareas de hoy
        // (podcast). Una sola query set por los ids distintos (sin N+1); los
        // medios no publicados se omiten (SPEC §6.9: el móvil muestra
        // content_unavailable). P1: ThumbnailUrl transporta la storage key; la
        // URL final la resuelve la capa API (B5).
        //
        // Fallback template-level (SPEC §4.4): las tareas PENDIENTES de hoy
        // muestran el media_id de weekly_day_templates (una sola query set por
        // (template, weekday)); así el móvil ve el contenido del podcast del
        // día antes de completarlo. La completación (media_id real del cliente)
        // tiene prioridad sobre el fallback.
        var fallbackByTask = await dbContext
            .WeeklyDayTemplates.AsNoTracking()
            .Where(d => d.TemplateId == row.TemplateId && d.Weekday == weekday)
            .Select(d => new { TaskCode = d.TaskCode.ToString(), d.MediaId })
            .ToListAsync(ct);

        var mediaIds = todayCompletions
            .Where(c => c.MediaId.HasValue)
            .Select(c => c.MediaId!.Value)
            .Concat(fallbackByTask.Where(f => f.MediaId.HasValue).Select(f => f.MediaId!.Value))
            .Distinct()
            .ToList();
        var mediaById =
            mediaIds.Count == 0
                ? new Dictionary<Guid, (string Title, int? DurationSecs, string? ThumbnailKey)>()
                : (
                    await dbContext
                        .MediaItems.AsNoTracking()
                        .Where(m => mediaIds.Contains(m.Id) && m.Status == MediaStatus.Published)
                        .Select(m => new
                        {
                            m.Id,
                            m.Title,
                            m.DurationSecs,
                            m.ThumbnailKey,
                        })
                        .ToListAsync(ct)
                ).ToDictionary(m => m.Id, m => (m.Title, m.DurationSecs, m.ThumbnailKey));

        var fallbackMediaByTask = fallbackByTask
            .Where(f => f.MediaId.HasValue)
            .GroupBy(f => f.TaskCode)
            .ToDictionary(g => g.Key, g => g.First().MediaId!.Value);

        // T-75: resolver contenido de nutrición y rutina para hoy.
        // Una sola llamada al resolver (anti N+1): el resultado se reutiliza
        // para todas las tareas nut/ejercicio del día.
        ProgramContentResolution? contentResolution = null;
        if (_programContentResolver is not null)
        {
            contentResolution = await _programContentResolver.ResolveAsync(
                patientId: row.PatientId,
                localDate: todayLocalDate,
                ct
            );
        }

        // Pre-cargar nombres de plan/rutina para el contenido del snapshot
        // (anti N+1: batch por IDs resueltos).
        string? resolvedPlanName = null;
        string? resolvedRoutineName = null;
        if (contentResolution is { })
        {
            if (contentResolution.NutritionPlanId is { } planId)
            {
                resolvedPlanName = await dbContext
                    .NutritionPlans.AsNoTracking()
                    .Where(p => p.Id == planId)
                    .Select(p => (string?)p.Name)
                    .FirstOrDefaultAsync(ct);
            }
            if (contentResolution.ExerciseRoutineId is { } routineId)
            {
                resolvedRoutineName = await dbContext
                    .ExerciseRoutines.AsNoTracking()
                    .Where(r => r.Id == routineId)
                    .Select(r => (string?)r.Name)
                    .FirstOrDefaultAsync(ct);
            }
        }

        var checkin = await dbContext
            .DailyCheckIns.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.EnrollmentId == enrollmentId && c.LocalDate == todayLocalDate,
                ct
            );

        var calendar = await BuildCalendarDaysAsync(
            enrollmentId,
            todayLocalDate.AddDays(-6),
            todayLocalDate,
            todayLocalDate,
            ct
        );

        var todayTasks = todayScheduled
            .Select(t =>
            {
                var done = todayCompletions.FirstOrDefault(c =>
                    c.TaskCode.ToString() == t.TaskCode
                );
                var catalog = ProgramTaskCatalog.For(Enum.Parse<TaskCode>(t.TaskCode));
                var taskCode = Enum.Parse<TaskCode>(t.TaskCode);

                TodayTaskContentDto? content = null;
                bool contentUnavailable = false;

                if (taskCode == TaskCode.podcast)
                {
                    // Podcast: contenido multimedia existente (sin cambios).
                    if (done?.MediaId is { } mediaId)
                    {
                        content = mediaById.TryGetValue(mediaId, out var media)
                            ? new TodayTaskContentDto(
                                mediaId,
                                media.Title,
                                media.DurationSecs,
                                media.ThumbnailKey
                            )
                            : new TodayTaskContentDto(mediaId, null, null, null);
                    }
                    else if (fallbackMediaByTask.TryGetValue(t.TaskCode, out var fallbackMediaId))
                    {
                        content = mediaById.TryGetValue(fallbackMediaId, out var media)
                            ? new TodayTaskContentDto(
                                fallbackMediaId,
                                media.Title,
                                media.DurationSecs,
                                media.ThumbnailKey
                            )
                            : new TodayTaskContentDto(fallbackMediaId, null, null, null);
                    }
                }
                else if (taskCode == TaskCode.nut)
                {
                    // T-75: nut → campos nutritionPlan* del resolvedor.
                    if (contentResolution is { NutritionPlanId: { } planId })
                    {
                        content = new TodayTaskContentDto(
                            MediaId: null,
                            Title: resolvedPlanName,
                            DurationSecs: null,
                            ThumbnailUrl: null,
                            NutritionPlanId: planId,
                            NutritionPlanName: resolvedPlanName,
                            NutritionPlanDayNumber: contentResolution.NutritionPlanDayNumber,
                            ExerciseRoutineId: null,
                            ExerciseRoutineName: null,
                            ContentUnavailable: false
                        );
                    }
                    else
                    {
                        // Sin plan activo para hoy → contentUnavailable.
                        contentUnavailable = true;
                        content = new TodayTaskContentDto(
                            MediaId: null,
                            Title: null,
                            DurationSecs: null,
                            ThumbnailUrl: null,
                            ContentUnavailable: true
                        );
                    }
                }
                else if (taskCode == TaskCode.ejercicio)
                {
                    // T-75: ejercicio → campos exerciseRoutine* del resolvedor.
                    if (contentResolution is { ExerciseRoutineId: { } routineId })
                    {
                        content = new TodayTaskContentDto(
                            MediaId: null,
                            Title: resolvedRoutineName,
                            DurationSecs: null,
                            ThumbnailUrl: null,
                            NutritionPlanId: null,
                            NutritionPlanName: null,
                            NutritionPlanDayNumber: null,
                            ExerciseRoutineId: routineId,
                            ExerciseRoutineName: resolvedRoutineName,
                            ContentUnavailable: false
                        );
                    }
                    else
                    {
                        // Sin rutina activa para hoy → contentUnavailable.
                        contentUnavailable = true;
                        content = new TodayTaskContentDto(
                            MediaId: null,
                            Title: null,
                            DurationSecs: null,
                            ThumbnailUrl: null,
                            ContentUnavailable: true
                        );
                    }
                }

                return new TodayTaskDto(
                    taskCode,
                    catalog.Title,
                    catalog.Short,
                    t.Points,
                    done is null ? "Pending" : "Completed",
                    done?.CompletedAt,
                    content,
                    contentUnavailable
                );
            })
            .ToList();

        var xpBalance = row.XpBalance ?? 0;
        var level = XpLevels.ForBalance(xpBalance);
        var streakCurrent = row.StreakCurrent ?? 0;
        var nextMilestone = streakCurrent > 0 ? 7 - (streakCurrent % 7) : 7;
        if (nextMilestone == 0)
        {
            nextMilestone = 7;
        }

        // Multiplicador x2 del snapshot (SPEC §16, D): vigente → valor real
        // (2.0) con su fin y horas restantes (floor); vencido o nunca activado
        // → 1.0, null y 0. La lectura NUNCA escribe (el reset lazy del estado
        // ocurre en el próximo otorgamiento, SPEC §16, C.1).
        var now = DateTime.UtcNow;
        DateTime? multiplierEndsAt = row.MultiplierEndsAt is { } ends && ends > now ? ends : null;
        var multiplierActive = multiplierEndsAt is null ? 1.0m : (row.MultiplierActive ?? 1.0m);
        var multiplierRemainingHours = multiplierEndsAt is null
            ? 0
            : (int)Math.Floor((multiplierEndsAt.Value - now).TotalHours);

        // Próximo hito de la racha del nutribiótico (SPEC §19, D): el primer
        // hito de la tabla (7/14/30/60/90) por encima de la racha actual; null
        // cuando ya se llegó a 90 (no hay hito siguiente). La lectura nunca
        // escribe.
        var nbStreak = row.NbStreak ?? 0;
        var nbLongestStreak = row.NbLongestStreak ?? 0;
        var nextNbMilestone = FindNextNbMilestone(nbStreak) is { } next
            ? new NbNextMilestoneDto(next.Days, next.BaseXp, next.Days - nbStreak)
            : null;

        return new ProgramSnapshotDto(
            enrollmentId,
            new ProgramSnapshotTemplateDto(
                row.TemplateId,
                row.TemplateCode,
                row.TemplateName,
                row.TemplateTotalWeeks,
                row.CurrentWeekNumber,
                week.Status,
                week.WeekStartDateLocal,
                week.WeekEndDateLocal,
                row.TemplateStreakMinTasks,
                row.TemplateEssentialTaskCodes
            ),
            todayLocalDate,
            todayTasks,
            (checkin?.TotalPoints ?? 0) + (checkin?.BonusAwarded ?? 0),
            checkin?.IsPerfectDay != true,
            // Máximo alcanzable del día: puntos base + bonus (SPEC §7.1).
            todayScheduled.Sum(t => t.Points) + 50,
            new XpInfoDto(xpBalance, level.Level, level.NextLevelAt),
            new StreakInfoDto(
                streakCurrent,
                row.StreakLongest ?? 0,
                row.FreezesRemaining ?? 0,
                multiplierActive,
                multiplierEndsAt,
                multiplierRemainingHours,
                nbStreak,
                nbLongestStreak,
                nextNbMilestone
            ),
            nextMilestone,
            calendar
        );
    }

    public async Task<DateOnly?> GetPatientLocalTodayAsync(
        Guid enrollmentId,
        CancellationToken ct = default
    )
    {
        var timezone = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => (string?)e.Timezone)
            .FirstOrDefaultAsync(ct);

        return timezone is null ? null : PatientLocalToday(timezone);
    }

    public async Task<ProgramCalendarDto> GetCalendarAsync(
        Guid enrollmentId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default
    )
    {
        var enrollment =
            await dbContext
                .ProgramEnrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId)
                .Select(e => new
                {
                    e.StartLocalDate,
                    e.Timezone,
                    TotalWeeks = e.Template!.TotalWeeks,
                })
                .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Inscripción {enrollmentId} no encontrada.");

        var checkins = await dbContext
            .DailyCheckIns.AsNoTracking()
            .Where(c => c.EnrollmentId == enrollmentId && c.LocalDate >= from && c.LocalDate <= to)
            .Select(c => new
            {
                c.LocalDate,
                c.IsPerfectDay,
                c.TotalPoints,
                c.BonusAwarded,
            })
            .ToListAsync(ct);

        var completions = await dbContext
            .TaskCompletions.AsNoTracking()
            .Where(t => t.EnrollmentId == enrollmentId && t.LocalDate >= from && t.LocalDate <= to)
            .OrderBy(t => t.LocalDate)
            .ThenBy(t => t.TaskCode)
            .Select(t => new { t.LocalDate, TaskCode = t.TaskCode.ToString() })
            .ToListAsync(ct);

        var todayLocal = PatientLocalToday(enrollment.Timezone);
        var days = new List<CalendarDayDetailDto>();
        var perfectDays = 0;
        var missedDays = 0;
        var totalXp = 0;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var checkin = checkins.FirstOrDefault(c => c.LocalDate == date);
            var dayCodes = completions
                .Where(t => t.LocalDate == date)
                .Select(t => t.TaskCode)
                .ToList();

            var weekNumber = ((date.DayNumber - enrollment.StartLocalDate.DayNumber) / 7) + 1;
            if (weekNumber < 1)
            {
                weekNumber = 1;
            }
            else if (weekNumber > enrollment.TotalWeeks)
            {
                weekNumber = enrollment.TotalWeeks;
            }

            if (checkin?.IsPerfectDay == true)
            {
                perfectDays++;
            }
            else if (dayCodes.Count == 0 && date < todayLocal)
            {
                missedDays++;
            }

            var points = (checkin?.TotalPoints ?? 0) + (checkin?.BonusAwarded ?? 0);
            totalXp += points;

            days.Add(
                new CalendarDayDetailDto(
                    date,
                    ToIsoWeekday(date),
                    weekNumber,
                    checkin?.IsPerfectDay ?? false,
                    points,
                    checkin?.BonusAwarded ?? 0,
                    dayCodes
                )
            );
        }

        return new ProgramCalendarDto(
            from,
            to,
            days,
            new CalendarSummaryDto(perfectDays, missedDays, totalXp)
        );
    }

    public async Task<ProgramPathDto> GetPathAsync(
        Guid enrollmentId,
        CancellationToken ct = default
    )
    {
        var enrollment =
            await dbContext
                .ProgramEnrollments.AsNoTracking()
                .Where(e => e.Id == enrollmentId)
                .Select(e => new { e.StartLocalDate, TotalWeeks = e.Template!.TotalWeeks })
                .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException($"Inscripción {enrollmentId} no encontrada.");

        var weeks = await dbContext
            .ProgramWeeks.AsNoTracking()
            .Where(w => w.EnrollmentId == enrollmentId)
            .Select(w => new
            {
                w.Id,
                w.WeekNumber,
                w.Status,
            })
            .ToListAsync(ct);

        var weekRollups = await dbContext
            .DailyCheckIns.AsNoTracking()
            .Where(c => c.EnrollmentId == enrollmentId)
            .GroupBy(c => c.ProgramWeekId)
            .Select(g => new
            {
                WeekId = g.Key,
                Points = g.Sum(c => c.TotalPoints + c.BonusAwarded),
                PerfectWeekdays = g.Count(c => c.IsPerfectDay),
            })
            .ToListAsync(ct);

        var rollupByWeekId = weekRollups.ToDictionary(r => r.WeekId);
        var weekByNumber = weeks.ToDictionary(w => w.WeekNumber);

        var result = new List<ProgramPathWeekDto>(enrollment.TotalWeeks);
        for (var weekNumber = 1; weekNumber <= enrollment.TotalWeeks; weekNumber++)
        {
            var start = enrollment.StartLocalDate.AddDays((weekNumber - 1) * 7);
            var hasWeek = weekByNumber.TryGetValue(weekNumber, out var week);
            var status = hasWeek ? week!.Status : ProgramWeekStatus.Locked;
            bool? isPerfectWeek = null;
            var points = 0;

            if (hasWeek && rollupByWeekId.TryGetValue(week!.Id, out var rollup))
            {
                points = rollup.Points;
                if (week.Status == ProgramWeekStatus.Completed)
                {
                    isPerfectWeek = rollup.PerfectWeekdays == 7;
                }
            }

            result.Add(
                new ProgramPathWeekDto(
                    weekNumber,
                    status,
                    isPerfectWeek,
                    points,
                    start,
                    start.AddDays(6)
                )
            );
        }

        return new ProgramPathDto(result);
    }

    // ---------------------------------------------------------------- Plantillas

    public async Task<(IReadOnlyList<ProgramTemplate> Items, int Total)> ListTemplatesAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.ProgramTemplates.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => t.Name.Contains(search) || t.Code.Contains(search));
        }

        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<TemplateStatus>(status, ignoreCase: true, out var parsedStatus)
        )
        {
            query = query.Where(t => t.Status == parsedStatus);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<ProgramTemplate?> GetTemplateAsync(Guid id, CancellationToken ct = default) =>
        await dbContext
            .ProgramTemplates.AsNoTracking()
            .Include(t => t.DayTemplates.OrderBy(d => d.Weekday).ThenBy(d => d.SortOrder))
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<ProgramTemplate?> GetTemplateByCodeAsync(
        string code,
        CancellationToken ct = default
    ) =>
        await dbContext
            .ProgramTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Code == code, ct);

    public async Task<ProgramTemplate> UpsertTemplateAsync(
        ProgramTemplate template,
        IReadOnlyList<WeeklyDayTemplate> dayTemplates,
        Guid? actorId = null,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var existing = await dbContext
                    .ProgramTemplates.Include(t => t.DayTemplates)
                    .FirstOrDefaultAsync(t => t.Id == template.Id, ct);

                var now = DateTime.UtcNow;
                if (existing is null)
                {
                    template.Id = template.Id == Guid.Empty ? Guid.NewGuid() : template.Id;
                    template.CreatedBy = actorId ?? template.CreatedBy;
                    template.CreatedAt = now;
                    // El parámetro dayTemplates es la fuente de verdad: si el
                    // caller precargó la colección, se ignora para no duplicar.
                    template.DayTemplates.Clear();
                    dbContext.ProgramTemplates.Add(template);
                    AddDayTemplates(template.Id, dayTemplates, actorId);
                }
                else
                {
                    existing.Code = template.Code;
                    existing.Name = template.Name;
                    existing.Description = template.Description;
                    existing.TotalWeeks = template.TotalWeeks;
                    existing.Status = template.Status;
                    existing.Version = template.Version;
                    existing.PublishedAt = template.PublishedAt;
                    existing.UpdatedBy = actorId;
                    existing.UpdatedAt = now;

                    dbContext.WeeklyDayTemplates.RemoveRange(existing.DayTemplates);
                    AddDayTemplates(existing.Id, dayTemplates, actorId);
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return existing ?? template;
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                await transaction.RollbackAsync(ct);
                throw new BusinessRuleViolationException(
                    "TEMPLATE_CODE_EXISTS: ya existe una plantilla con ese código."
                );
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<IReadOnlyList<WeeklyDayTemplate>> ReplaceWeekdayTasksAsync(
        Guid templateId,
        IReadOnlyList<WeeklyDayTemplate> tasks,
        Guid? actorId = null,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var exists = await dbContext
                    .ProgramTemplates.AsNoTracking()
                    .AnyAsync(t => t.Id == templateId, ct);
                if (!exists)
                {
                    throw new NotFoundException($"Plantilla {templateId} no encontrada.");
                }

                await dbContext
                    .WeeklyDayTemplates.Where(d => d.TemplateId == templateId)
                    .ExecuteDeleteAsync(ct);

                AddDayTemplates(templateId, tasks, actorId);
                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return tasks;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<EnrollmentWeekDetailDto> ReplaceEnrollmentWeekTasksAsync(
        Guid enrollmentId,
        int weekNumber,
        IReadOnlyList<WeeklyDayTemplate> tasks,
        Guid? actorId = null,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var enrollment = await dbContext
                    .ProgramEnrollments.AsNoTracking()
                    .Include(e => e.Template)
                    .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct);
                if (enrollment is null)
                {
                    throw new NotFoundException($"Inscripción {enrollmentId} no encontrada.");
                }

                var totalWeeks = enrollment.Template?.TotalWeeks ?? 83;
                if (weekNumber < 1 || weekNumber > totalWeeks)
                {
                    throw new UnprocessableEntityException(
                        $"WEEK_NUMBER_OUT_OF_RANGE: el número de semana {weekNumber} está fuera del rango [1..{totalWeeks}]."
                    );
                }

                var week = await dbContext.ProgramWeeks.FirstOrDefaultAsync(
                    w => w.EnrollmentId == enrollmentId && w.WeekNumber == weekNumber,
                    ct
                );
                if (week is null)
                {
                    throw new NotFoundException(
                        $"Semana {weekNumber} de la inscripción {enrollmentId} no encontrada."
                    );
                }

                var now = DateTime.UtcNow;
                week.TasksSnapshot = BuildSnapshot(tasks);
                week.UpdatedAt = now;
                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                var detail = await GetEnrollmentWeekDetailAsync(
                    enrollmentId,
                    weekNumber,
                    actorId ?? Guid.Empty,
                    ct
                );
                return detail!;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    private void AddDayTemplates(
        Guid templateId,
        IEnumerable<WeeklyDayTemplate> tasks,
        Guid? actorId
    )
    {
        foreach (var task in tasks)
        {
            task.Id = Guid.NewGuid();
            task.TemplateId = templateId;
            task.CreatedBy = actorId;
            task.CreatedAt = DateTime.UtcNow;
            // Evita el fix-up de la navegación Template al re-agregar filas
            // desmaterializadas (identity conflict si la plantilla ya está
            // rastreada por el contexto).
            task.Template = null;
            dbContext.WeeklyDayTemplates.Add(task);
        }
    }

    // ---------------------------------------------------------------- Adaptaciones

    public async Task<(
        IReadOnlyList<AdaptationRecommendation> Items,
        int Total
    )> ListAdaptationsAsync(
        Guid? enrollmentId,
        AdaptationStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.AdaptationRecommendations.AsNoTracking().AsQueryable();

        if (enrollmentId.HasValue)
        {
            query = query.Where(a => a.EnrollmentId == enrollmentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<AdaptationRecommendation?> GetAdaptationAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .AdaptationRecommendations.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    /// <summary>
    /// Transiciones de la máquina de estados (SPEC §5.6). Cuando la transición
    /// deja la recomendación en <c>Applied</c> y <paramref name="auditActionOnApply"/>
    /// no es nulo, la fila semántica (p. ej. <c>AdaptationApplied</c>) se escribe
    /// en <c>audit.activity_logs</c> DENTRO de la misma transacción que el cambio
    /// de estado (AC-17): todo-o-nada. NpgsqlRetryingExecutionStrategy no soporta
    /// transacciones manuales fuera de su unidad retriable (mismo patrón que
    /// <c>CompleteTaskAsync</c>).
    /// </summary>
    public async Task<AdaptationRecommendation> DecideAdaptationAsync(
        Guid adaptationId,
        AdaptationDecisionAction action,
        Guid? actorId = null,
        string? auditActionOnApply = null,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var adaptation =
                    await dbContext.AdaptationRecommendations.FirstOrDefaultAsync(
                        a => a.Id == adaptationId,
                        ct
                    )
                    ?? throw new NotFoundException(
                        $"Recomendación de adaptación {adaptationId} no encontrada."
                    );

                var now = DateTime.UtcNow;
                switch (action)
                {
                    case AdaptationDecisionAction.Approve
                        when adaptation.Status == AdaptationStatus.Pending:
                        adaptation.Status = AdaptationStatus.Approved;
                        adaptation.DecidedBy = actorId;
                        adaptation.DecidedAt = now;
                        break;
                    case AdaptationDecisionAction.Approve:
                        throw new BusinessRuleViolationException(
                            "ADAPTATION_STATE: solo las recomendaciones Pending pueden aprobarse."
                        );
                    case AdaptationDecisionAction.Reject
                        when adaptation.Status == AdaptationStatus.Pending:
                        adaptation.Status = AdaptationStatus.Rejected;
                        adaptation.DecidedBy = actorId;
                        adaptation.DecidedAt = now;
                        break;
                    case AdaptationDecisionAction.Reject:
                        throw new BusinessRuleViolationException(
                            "ADAPTATION_STATE: solo las recomendaciones Pending pueden rechazarse."
                        );
                    case AdaptationDecisionAction.Apply
                        when adaptation.Status == AdaptationStatus.Approved:
                        adaptation.Status = AdaptationStatus.Applied;
                        adaptation.AppliedAt = now;
                        break;
                    case AdaptationDecisionAction.Apply:
                        throw new BusinessRuleViolationException(
                            "ADAPTATION_STATE: solo las recomendaciones Approved pueden aplicarse."
                        );
                    default:
                        throw new BusinessRuleViolationException(
                            "ADAPTATION_STATE: transición no permitida."
                        );
                }

                adaptation.UpdatedAt = now;
                await dbContext.SaveChangesAsync(ct);

                // AC-17: fila semántica en la misma transacción que la transición
                // (ExecuteSqlInterpolatedAsync se enlista en la transacción activa).
                // Si el commit falla, ni la transición ni la fila quedan a medias.
                if (
                    action == AdaptationDecisionAction.Apply
                    && adaptation.Status == AdaptationStatus.Applied
                    && !string.IsNullOrWhiteSpace(auditActionOnApply)
                )
                {
                    await WriteAuditRowAsync(
                        auditActionOnApply,
                        "app",
                        "adaptation_recommendations",
                        adaptation.Id,
                        actorId,
                        ct
                    );
                }

                await transaction.CommitAsync(ct);
                return adaptation;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// Marca como <c>Superseded</c> las recomendaciones <c>Pending</c> previas
    /// del mismo <c>(kind, target_entity_id)</c> al crear una nueva (SPEC §5.6:
    /// "una Pending nueva reemplaza a la anterior en la misma transacción").
    /// La fila recién creada se excluye explícitamente para no auto-superarla;
    /// el caller crea la nueva y llama a este método dentro de su transacción
    /// (ExecuteUpdate = una sola escritura atómica). Devuelve cuántas quedaron
    /// superadas. Transición automática del motor: no registra decided_by.
    /// </summary>
    public async Task<int> SupersedePendingAsync(
        Guid enrollmentId,
        AdaptationKind kind,
        Guid targetEntityId,
        Guid newRecommendationId,
        CancellationToken ct = default
    )
    {
        var now = DateTime.UtcNow;
        return await dbContext
            .AdaptationRecommendations.Where(a =>
                a.EnrollmentId == enrollmentId
                && a.Kind == kind
                && a.TargetEntityId == targetEntityId
                && a.Status == AdaptationStatus.Pending
                && a.Id != newRecommendationId
            )
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(a => a.Status, AdaptationStatus.Superseded)
                        .SetProperty(a => a.UpdatedAt, now),
                ct
            );
    }

    /// <summary>
    /// Motor de adaptaciones (SPEC §6.8, T-23): evalúa las reglas deterministas
    /// tras cada completación y persiste las propuestas DENTRO de la misma
    /// transacción de la escritura que las disparó.
    ///
    /// - Propuestas con aprobación clínica (<c>DifficultyChange</c>) → quedan
    ///   <c>Pending</c> para la cola del ERP y superan las <c>Pending</c>
    ///   previas del mismo (kind, target) (SPEC §5.6).
    /// - Refrescos de contenido (<c>RequiresApproval = false</c>) → se
    ///   auto-aplican (<c>Applied</c> + <c>applied_at</c>) y escriben la fila
    ///   semántica <c>AdaptationApplied</c> en <c>audit.activity_logs</c> (AC-17)
    ///   en la misma transacción.
    /// - Dedupe temporal de 7 días por (kind, target, inscripción): una regla ya
    ///   atendida no vuelve a dispararse en cada completación.
    /// </summary>
    private async Task EvaluateAdaptationsAsync(
        Guid enrollmentId,
        DateOnly todayLocalDate,
        int previousBalance,
        CancellationToken ct
    )
    {
        // Ventana de 7 días calendario (incluye hoy): los días sin check-in
        // entran como no perfectos/sin mood; el motor es una función pura.
        var from = todayLocalDate.AddDays(-6);
        var checkins = await dbContext
            .DailyCheckIns.AsNoTracking()
            .Where(c =>
                c.EnrollmentId == enrollmentId
                && c.LocalDate >= from
                && c.LocalDate <= todayLocalDate
            )
            .Select(c => new
            {
                c.LocalDate,
                c.IsPerfectDay,
                c.MoodScore,
            })
            .ToListAsync(ct);

        var window = new List<AdaptationDayWindow>(7);
        for (var date = from; date <= todayLocalDate; date = date.AddDays(1))
        {
            var checkin = checkins.FirstOrDefault(c => c.LocalDate == date);
            window.Add(
                new AdaptationDayWindow(date, checkin?.IsPerfectDay ?? false, checkin?.MoodScore)
            );
        }

        var currentBalance = await CurrentBalanceAsync(enrollmentId, ct);
        var proposals = _programAdaptationEngine.Evaluate(
            new AdaptationEvaluationInput(
                enrollmentId,
                todayLocalDate,
                previousBalance,
                currentBalance,
                window
            )
        );

        if (proposals.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var dedupeWindowStart = now.AddDays(-7);

        foreach (var proposal in proposals)
        {
            // Dedupe: la misma regla ya fue atendida recientemente.
            var alreadyAttended = await dbContext
                .AdaptationRecommendations.AsNoTracking()
                .AnyAsync(
                    a =>
                        a.EnrollmentId == enrollmentId
                        && a.Kind == proposal.Kind
                        && a.TargetEntityId == proposal.TargetEntityId
                        && a.CreatedAt >= dedupeWindowStart
                        && a.Status != AdaptationStatus.Superseded
                        && a.Status != AdaptationStatus.Rejected,
                    ct
                );
            if (alreadyAttended)
            {
                continue;
            }

            var recommendation = new AdaptationRecommendation
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                Kind = proposal.Kind,
                TargetEntityType = proposal.TargetEntityType,
                TargetEntityId = proposal.TargetEntityId,
                Payload = proposal.Payload,
                Reason = proposal.Reason,
                Status = proposal.RequiresApproval
                    ? AdaptationStatus.Pending
                    : AdaptationStatus.Applied,
                RequiresApproval = proposal.RequiresApproval,
                RequestedBy = null, // motor de reglas: sin actor (auditoría)
                AppliedAt = proposal.RequiresApproval ? null : now,
                CreatedAt = now,
            };
            dbContext.AdaptationRecommendations.Add(recommendation);

            // Las que requieren aprobación superan las Pending previas del
            // mismo (kind, target) en la misma transacción (SPEC §5.6).
            if (proposal.RequiresApproval)
            {
                await SupersedePendingAsync(
                    enrollmentId,
                    proposal.Kind,
                    proposal.TargetEntityId,
                    recommendation.Id,
                    ct
                );
            }

            await dbContext.SaveChangesAsync(ct);

            // AC-17: fila semántica en la misma transacción que la aplicación.
            if (!proposal.RequiresApproval)
            {
                await WriteAuditRowAsync(
                    "AdaptationApplied",
                    "app",
                    "adaptation_recommendations",
                    recommendation.Id,
                    null,
                    ct
                );
            }
        }
    }

    public async Task WriteAuditRowAsync(
        string action,
        string schemaName,
        string tableName,
        Guid recordId,
        Guid? actorId = null,
        CancellationToken ct = default
    )
    {
        // El trigger de auditoría cubre el DML de las tablas del módulo; esta
        // fila es un evento semántico explícito (AC-17) y se inserta por SQL
        // parametrizado (precedente: FOR UPDATE en CompleteTaskAsync). El rol
        // app_user tiene INSERT sobre audit.activity_logs (migración inicial).
        // actor_type: USER cuando hay actor, SYSTEM en transiciones automáticas.
        var actorType = actorId.HasValue ? "USER" : "SYSTEM";
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO audit.activity_logs
                (occurred_at, action, schema_name, table_name, record_id,
                 actor_type, user_id)
            VALUES
                (now(), {action}, {schemaName}, {tableName}, {recordId.ToString()},
                 {actorType}, {actorId})
            """,
            ct
        );
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<CompleteTaskResult> BuildReplayResultAsync(
        Guid enrollmentId,
        TaskCompletion existing,
        CancellationToken ct
    )
    {
        var balance = await CurrentBalanceAsync(enrollmentId, ct);
        var streak = await dbContext
            .StreakStates.AsNoTracking()
            .FirstOrDefaultAsync(s => s.EnrollmentId == enrollmentId, ct);
        var checkin = await dbContext
            .DailyCheckIns.AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.EnrollmentId == enrollmentId && c.LocalDate == existing.LocalDate,
                ct
            );

        var dayMax = await DayPointsMaxAsync(enrollmentId, existing.LocalDate, ct);

        return new CompleteTaskResult(
            CompleteTaskOutcome.Replay,
            existing.Id,
            existing.PointsAwarded,
            balance,
            checkin?.IsPerfectDay ?? false,
            checkin?.BonusAwarded ?? 0,
            streak?.CurrentStreak ?? 0,
            streak?.FreezesRemaining ?? 0,
            (checkin?.TotalPoints ?? 0) + (checkin?.BonusAwarded ?? 0),
            dayMax
        );
    }

    private async Task<int> CurrentBalanceAsync(Guid enrollmentId, CancellationToken ct) =>
        await dbContext
            .XpLedgerEntries.AsNoTracking()
            .Where(x => x.EnrollmentId == enrollmentId)
            .OrderByDescending(x => x.BalanceAfter)
            .Select(x => (int?)x.BalanceAfter)
            .FirstOrDefaultAsync(ct)
        ?? 0;

    private async Task<int> DayPointsMaxAsync(
        Guid enrollmentId,
        DateOnly localDate,
        CancellationToken ct
    )
    {
        var week = await dbContext
            .ProgramWeeks.AsNoTracking()
            .FirstOrDefaultAsync(
                w =>
                    w.EnrollmentId == enrollmentId
                    && w.WeekStartDateLocal <= localDate
                    && localDate <= w.WeekEndDateLocal,
                ct
            );
        if (week is null)
        {
            return 0;
        }

        var weekday = ToIsoWeekday(localDate);
        return ComputeDayPointsMax(
            ParseSnapshot(week.TasksSnapshot).Where(t => t.Weekday == weekday)
        );
    }

    /// <summary>
    /// Máximo de puntos alcanzable del día: suma de la base del snapshot + el
    /// bonus de día perfecto (+50, SPEC §6.5). Única computación compartida por
    /// la primera escritura y el replay (SPEC §7.2: 700 + 50 = 750) para que el
    /// body del replay sea idéntico al de la primera respuesta.
    /// </summary>
    private static int ComputeDayPointsMax(IEnumerable<SnapshotTask> dayTasks) =>
        dayTasks.Sum(t => t.Points) + 50;

    // ---------------------------------------------------------------- Catálogo de reglas XP (SPEC §14)

    /// <summary>
    /// Resuelve la regla del catálogo vigente para un código (SPEC §14.3): una
    /// regla <c>Active</c> dentro de su ventana <see cref="XpRule.ValidFrom"/>..
    /// <see cref="XpRule.ValidUntil"/> gana sobre el comportamiento por defecto.
    /// La referencia de "hoy" es la fecha UTC del servidor: el catálogo es
    /// configuración global de administración (no por paciente), por lo que no
    /// depende de la zona del paciente. Devuelve null si la regla no existe o
    /// no está vigente (fallback documentado en <see cref="ResolveXpAwardAsync"/>).
    /// </summary>
    private async Task<XpRule?> ResolveActiveRuleAsync(string ruleCode, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await dbContext
            .XpRules.AsNoTracking()
            .FirstOrDefaultAsync(
                r =>
                    r.Code == ruleCode
                    && r.Active
                    && r.ValidFrom <= today
                    && (r.ValidUntil == null || r.ValidUntil >= today),
                ct
            );
    }

    /// <summary>
    /// Aplica la precedencia del catálogo de reglas XP a un otorgamiento
    /// (SPEC §14.3): puntos = <c>regla.BaseXp ?? defaultPoints</c> (para las
    /// reglas <c>TASK_*</c> el BaseXp es null → se conservan los puntos del
    /// snapshot/plantilla), total = floor(base × multiplier × multiplicador del
    /// paciente, SPEC §16, C.2), y se verifican los topes anti-fraude
    /// <see cref="XpRule.MaxPerDay"/> / <see cref="XpRule.MaxPerWeek"/>
    /// contando las entradas previas de <c>app.xp_ledger</c> para la misma
    /// inscripción + regla en la ventana (fecha local del paciente → rango UTC,
    /// respetando DST).
    ///
    /// <b>Fallback explícito</b>: si la regla no existe o está inactiva/vencida,
    /// devuelve (<paramref name="defaultPoints"/> × multiplicador del paciente,
    /// null, multiplicador del paciente) — comportamiento actual sin límites y
    /// sin <c>rule_code</c> en el libro mayor (el multiplicador del paciente
    /// aplica a TODOS los otorgamientos, sin excepciones, SPEC §16, C.4). La
    /// llamada ocurre dentro de la transacción de completación (inscripción
    /// bloqueada FOR UPDATE), por lo que los conteos son consistentes bajo
    /// concurrencia; las filas previas a la existencia del catálogo tienen
    /// <c>rule_code</c> null y no cuentan para los topes (prospective only).
    /// </summary>
    private async Task<(int Points, string? RuleCode, decimal MultiplierUsed)> ResolveXpAwardAsync(
        Guid enrollmentId,
        string timezone,
        string ruleCode,
        int defaultPoints,
        DateOnly localDate,
        CancellationToken ct
    )
    {
        // Multiplicador del paciente (SPEC §16, C.1): vigente → aplica a todo
        // otorgamiento; vencido/null → 1.0 con reset lazy (ExecuteUpdate en la
        // misma transacción del otorgamiento).
        var patientMultiplier = await ResolvePatientMultiplierAsync(enrollmentId, ct);

        var rule = await ResolveActiveRuleAsync(ruleCode, ct);
        if (rule is null)
        {
            // Fallback (SPEC §14.3): sin regla o regla inactiva/vencida →
            // comportamiento actual (puntos por defecto, sin límites) + el
            // multiplicador del paciente vigente.
            return ((int)Math.Floor(defaultPoints * patientMultiplier), null, patientMultiplier);
        }

        var basePoints = rule.BaseXp ?? defaultPoints;
        var effectiveMultiplier = rule.Multiplier * patientMultiplier;
        var total = (int)Math.Floor(basePoints * effectiveMultiplier);

        if (rule.MaxPerDay.HasValue || rule.MaxPerWeek.HasValue)
        {
            var tz = ResolveTimeZone(timezone);
            var dayStartUtc = LocalDateToUtcStart(localDate, tz);
            var dayEndUtc = LocalDateToUtcStart(localDate.AddDays(1), tz);
            var weekStart = localDate.AddDays(-(ToIsoWeekday(localDate) - 1));
            var weekStartUtc = LocalDateToUtcStart(weekStart, tz);
            var weekEndUtc = LocalDateToUtcStart(weekStart.AddDays(7), tz);

            if (rule.MaxPerDay.HasValue)
            {
                var usedToday = await dbContext
                    .XpLedgerEntries.AsNoTracking()
                    .CountAsync(
                        x =>
                            x.EnrollmentId == enrollmentId
                            && x.RuleCode == rule.Code
                            && x.AwardedAt >= dayStartUtc
                            && x.AwardedAt < dayEndUtc,
                        ct
                    );
                if (usedToday >= rule.MaxPerDay.Value)
                {
                    throw new BusinessRuleViolationException(
                        $"XP_DAILY_LIMIT_REACHED: la regla {rule.Code} permite máximo "
                            + $"{rule.MaxPerDay.Value} otorgamiento(s) por día."
                    );
                }
            }

            if (rule.MaxPerWeek.HasValue)
            {
                var usedThisWeek = await dbContext
                    .XpLedgerEntries.AsNoTracking()
                    .CountAsync(
                        x =>
                            x.EnrollmentId == enrollmentId
                            && x.RuleCode == rule.Code
                            && x.AwardedAt >= weekStartUtc
                            && x.AwardedAt < weekEndUtc,
                        ct
                    );
                if (usedThisWeek >= rule.MaxPerWeek.Value)
                {
                    throw new BusinessRuleViolationException(
                        $"XP_WEEKLY_LIMIT_REACHED: la regla {rule.Code} permite máximo "
                            + $"{rule.MaxPerWeek.Value} otorgamiento(s) por semana."
                    );
                }
            }
        }

        return (total, rule.Code, effectiveMultiplier);
    }

    /// <summary>
    /// Multiplicador de XP activo del paciente (SPEC §16, C.1): si
    /// <c>multiplier_ends_at</c> es null o ya venció (comparado contra el reloj
    /// del servidor), se trata como 1.0 y se resetea lazy (<c>ExecuteUpdate</c>
    /// a <c>multiplier_active = 1.0</c> / <c>multiplier_ends_at = null</c>) en
    /// la misma transacción del otorgamiento. Si está vigente, devuelve el
    /// valor almacenado (2.0). Solo se invoca dentro de transacciones de
    /// otorgamiento (completación con FOR UPDATE, XP clínica, hitos de racha).
    /// </summary>
    private async Task<decimal> ResolvePatientMultiplierAsync(
        Guid enrollmentId,
        CancellationToken ct
    )
    {
        var state = await dbContext
            .StreakStates.AsNoTracking()
            .Where(s => s.EnrollmentId == enrollmentId)
            .Select(s => new { s.MultiplierActive, s.MultiplierEndsAt })
            .FirstOrDefaultAsync(ct);

        if (state is null)
        {
            return 1.0m;
        }

        if (state.MultiplierEndsAt is null || state.MultiplierEndsAt <= DateTime.UtcNow)
        {
            // Reset lazy del multiplicador vencido (SPEC §16, C.1): el estado
            // vuelve a 1.0 sin esperar a que una lectura lo detecte.
            await dbContext
                .StreakStates.Where(s => s.EnrollmentId == enrollmentId)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.MultiplierActive, 1.0m)
                            .SetProperty(x => x.MultiplierEndsAt, (DateTime?)null)
                            .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                    ct
                );
            return 1.0m;
        }

        return state.MultiplierActive >= 1.0m ? state.MultiplierActive : 1.0m;
    }

    // ------------------------------------------------------- Hitos de racha + multiplicador (SPEC §16)

    /// <summary>
    /// Tabla de hitos de racha (SPEC §16, B): día hito → razón del libro mayor
    /// (= código de regla), regla del catálogo, XP base (semilla §14.2) y
    /// duración en horas del multiplicador x2 activado al alcanzarlo (0 = el
    /// hito no activa multiplicador). Día 7 → sin multiplicador; 11 → 24h;
    /// 22 → 48h; 50 → 72h.
    /// </summary>
    private static readonly IReadOnlyList<StreakMilestone> StreakMilestones =
    [
        new(7, XpReason.STREAK_7, XpRuleCodes.Streak7, 100, 0),
        new(11, XpReason.STREAK_11, XpRuleCodes.Streak11, 200, 24),
        new(22, XpReason.STREAK_22, XpRuleCodes.Streak22, 500, 48),
        new(50, XpReason.STREAK_50, XpRuleCodes.Streak50, 1500, 72),
    ];

    /// <summary>Hito de racha cuyo día coincide exactamente con la racha actual (null si no es hito).</summary>
    private static StreakMilestone? FindStreakMilestone(int streakDay) =>
        StreakMilestones.FirstOrDefault(m => m.Day == streakDay);

    /// <summary>
    /// Motor de hitos de racha (SPEC §16, B): cuando la racha creció HOY hasta
    /// un día hito (7/11/22/50) otorga la XP del hito UNA vez por inscripción y
    /// activa el multiplicador x2 en los hitos que lo definen (11/22/50):
    ///
    /// 1. La XP del hito se otorga con la resolución del catálogo
    ///    (<c>rule_code = STREAK_{days}</c>, <c>source_ref_type =
    ///    'streak_milestone'</c>, <c>source_ref_id = streak_states.enrollment_id</c>,
    ///    <c>reason = 'STREAK_{days}'</c>); el dedupe parcial
    ///    <c>(source_ref_type, source_ref_id, reason)</c> del libro mayor la
    ///    hace idempotente (si la racha se rompió y se reconstruyó, no se
    ///    vuelve a otorgar).
    /// 2. Si el hito define multiplicador, se activa ANTES del otorgamiento
    ///    (<c>multiplier_active = 2.0</c>, <c>multiplier_ends_at = now + horas</c>):
    ///    la XP del hito se otorga con el x2 recién activado (SPEC §16, C.4: el
    ///    multiplicador aplica a todo, sin excepciones). Si ya había un
    ///    multiplicador vigente, el nuevo lo SOBRESCRIBE (se extiende desde
    ///    ahora, SPEC §16, B.2).
    /// 3. El mismo chequeo (XP del hito ya otorgada) guarda la activación: un
    ///    hito repetido no re-activa el multiplicador.
    ///
    /// Corre dentro de la transacción de completación (inscripción bloqueada
    /// FOR UPDATE); el <c>ExecuteUpdate</c> del multiplicador y la inserción
    /// del libro mayor son atómicos con el resto del día perfecto.
    /// </summary>
    private async Task AwardStreakMilestoneIfReachedAsync(
        Guid enrollmentId,
        Guid patientId,
        string timezone,
        int currentStreak,
        DateOnly localDate,
        CancellationToken ct
    )
    {
        var milestone = FindStreakMilestone(currentStreak);
        if (milestone is null)
        {
            return;
        }

        // Guardia de idempotencia compartida (SPEC §16, B.3): si la XP del
        // hito ya se otorgó en esta inscripción, tampoco se re-activa el
        // multiplicador.
        var alreadyAwarded = await dbContext
            .XpLedgerEntries.AsNoTracking()
            .AnyAsync(
                x =>
                    x.EnrollmentId == enrollmentId
                    && x.SourceRefType == StreakMilestoneSourceRefType
                    && x.SourceRefId == enrollmentId
                    && x.Reason == milestone.Reason,
                ct
            );
        if (alreadyAwarded)
        {
            return;
        }

        var now = DateTime.UtcNow;

        if (milestone.MultiplierHours > 0)
        {
            await dbContext
                .StreakStates.Where(s => s.EnrollmentId == enrollmentId)
                .ExecuteUpdateAsync(
                    s =>
                        s.SetProperty(x => x.MultiplierActive, 2.0m)
                            .SetProperty(
                                x => x.MultiplierEndsAt,
                                now.AddHours(milestone.MultiplierHours)
                            )
                            .SetProperty(x => x.UpdatedAt, now),
                    ct
                );
        }

        var (points, ruleCode, multiplierUsed) = await ResolveXpAwardAsync(
            enrollmentId,
            timezone,
            milestone.RuleCode,
            milestone.BaseXp,
            localDate,
            ct
        );
        var balance = await CurrentBalanceAsync(enrollmentId, ct);

        dbContext.XpLedgerEntries.Add(
            new XpLedgerEntry
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                Amount = points,
                Reason = milestone.Reason,
                SourceRefType = StreakMilestoneSourceRefType,
                SourceRefId = enrollmentId,
                RuleCode = ruleCode,
                MultiplierUsed = multiplierUsed,
                BalanceAfter = balance + points,
                AwardedAt = now,
            }
        );

        // Notificación gamificada del hito de racha (SPEC §20, C.1): best-
        // effort (nunca lanza, AC-42). En los hitos con multiplicador (11/22/50)
        // la activación x2 se pliega en el mensaje (SPEC §20, C.2 —
        // "multiplier_expiring" es FUTURO y necesita scheduler, no se implementa).
        if (_gamifiedNotificationService is not null)
        {
            var message =
                milestone.MultiplierHours > 0
                    ? $"🏆 ¡{milestone.Day} días! +{points} XP · ¡x2 por {milestone.MultiplierHours}h!"
                    : $"🏆 ¡{milestone.Day} días! +{points} XP";

            await _gamifiedNotificationService.NotifyAsync(
                patientId,
                "milestone_reached",
                $"🏆 ¡{milestone.Day} días!",
                message,
                "high",
                ct
            );
        }
    }

    private const string StreakMilestoneSourceRefType = "streak_milestone";

    private sealed record StreakMilestone(
        int Day,
        XpReason Reason,
        string RuleCode,
        int BaseXp,
        int MultiplierHours
    );

    // ------------------------------------------------- Racha propia del nutribiótico (SPEC §19, "Paso 7a")

    /// <summary>
    /// Tabla de hitos de la racha propia del nutribiótico (SPEC §19, B): día
    /// hito → razón del libro mayor (= código de regla) y XP base de la semilla
    /// §19.2 (<c>NB_STREAK_7/14/30/60/90</c>, categoría <c>nutriobiotic</c>,
    /// topes 1/día y 1/semana). A diferencia de los hitos de la racha general
    /// (SPEC §16, una vez por inscripción), cada corrida de 7/14/... días
    /// RE-OTORGA su hito al alcanzarlo (AC-39): el dedupe parcial usa la
    /// completación que dispara el hito (única por corrida), no la inscripción.
    /// </summary>
    private static readonly IReadOnlyList<NbMilestone> NbMilestones =
    [
        new(7, XpReason.NB_STREAK_7, XpRuleCodes.NbStreak7, 50),
        new(14, XpReason.NB_STREAK_14, XpRuleCodes.NbStreak14, 100),
        new(30, XpReason.NB_STREAK_30, XpRuleCodes.NbStreak30, 250),
        new(60, XpReason.NB_STREAK_60, XpRuleCodes.NbStreak60, 500),
        new(90, XpReason.NB_STREAK_90, XpRuleCodes.NbStreak90, 1000),
    ];

    /// <summary>Hito de la racha del nutribiótico cuyo día coincide exactamente con la racha actual (null si no es hito).</summary>
    private static NbMilestone? FindNbMilestone(short streakDay) =>
        NbMilestones.FirstOrDefault(m => m.Days == streakDay);

    /// <summary>Próximo hito de la racha del nutribiótico por encima de la racha actual (null si ya llegó a 90).</summary>
    private static NbMilestone? FindNextNbMilestone(int currentStreak) =>
        NbMilestones.FirstOrDefault(m => m.Days > currentStreak);

    /// <summary>
    /// Motor de la racha propia del nutribiótico (SPEC §19, B): actualiza la
    /// racha CONSECUTIVA de la tarea nutribiotico en <c>streak_states</c> y,
    /// si el nuevo conteo cae exactamente en un hito (7/14/30/60/90), otorga la
    /// XP del hito por el camino de resolución del catálogo.
    ///
    /// Reglas (SPEC §19):
    /// 1. Consecutivo al último día con nutribiotico → <c>nb_current_streak + 1</c>;
    ///    cualquier hueco (día perdido) → se reinicia a 1. Los congelamientos
    ///    NO protegen esta racha (AC-38): es independiente de la racha general
    ///    y de su rescate (SPEC §17, C).
    /// 2. Se actualiza SIEMPRE en la primera escritura de la tarea
    ///    (<c>ExecuteUpdate</c>, convención del repositorio); el replay
    ///    idempotente nunca llega acá.
    /// 3. Hito alcanzado → <c>rule_code = NB_STREAK_{days}</c>,
    ///    <c>source_ref_type = 'nb_milestone'</c>, <c>source_ref_id =
    ///    task_completions.id</c> (la completación que disparó el hito) y
    ///    <c>reason = 'NB_STREAK_{days}'</c>. El dedupe parcial del libro mayor
    ///    es idempotente POR CORRIDA: cada nueva corrida re-otorga su hito al
    ///    alcanzarlo (AC-39), porque la completación es única por corrida.
    ///    El multiplicador del paciente (SPEC §16, C) aplica como en todo
    ///    otorgamiento.
    /// 4. Los topes <c>max_per_day = 1</c>/<c>max_per_week = 1</c> de las
    ///    reglas pueden rechazar un re-otorgamiento (nueva corrida dentro de la
    ///    misma semana): el hito se omite sin romper la completación (la racha
    ///    y la tarea quedan intactas).
    /// </summary>
    private async Task UpdateNbStreakAsync(
        Guid enrollmentId,
        Guid patientId,
        string timezone,
        DateOnly today,
        Guid completionId,
        CancellationToken ct
    )
    {
        var state = await dbContext
            .StreakStates.AsNoTracking()
            .Where(s => s.EnrollmentId == enrollmentId)
            .Select(s => new
            {
                s.NbCurrentStreak,
                s.NbLongestStreak,
                s.NbLastCompletedDate,
            })
            .FirstOrDefaultAsync(ct);

        if (state is null)
        {
            // Sin fila de racha (no debería ocurrir: se crea al inscribir).
            return;
        }

        // Consecutivo vs reinicio: la racha del nutribiótico NO distingue
        // días perfectos ni usa congelamientos — solo pregunta si ayer se
        // completó la tarea. Un día perdido rompe la corrida (AC-38).
        var newCount =
            state.NbLastCompletedDate == today.AddDays(-1)
                ? (short)(state.NbCurrentStreak + 1)
                : (short)1;

        var newLongest = Math.Max(state.NbLongestStreak, newCount);

        await dbContext
            .StreakStates.Where(s => s.EnrollmentId == enrollmentId)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(x => x.NbCurrentStreak, newCount)
                        .SetProperty(x => x.NbLongestStreak, newLongest)
                        .SetProperty(x => x.NbLastCompletedDate, today)
                        .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                ct
            );

        var milestone = FindNbMilestone(newCount);
        if (milestone is null)
        {
            return;
        }

        // Otorgamiento del hito (SPEC §19, B.3): el camino del catálogo
        // (ResolveXpAwardAsync) aplica la regla NB_STREAK_{days}, el
        // multiplicador del paciente y los topes 1/día y 1/semana. Un tope
        // alcanzado omite el hito (nunca rompe la completación).
        int points;
        string? ruleCode;
        decimal multiplierUsed;
        try
        {
            (points, ruleCode, multiplierUsed) = await ResolveXpAwardAsync(
                enrollmentId,
                timezone,
                milestone.RuleCode,
                milestone.BaseXp,
                today,
                ct
            );
        }
        catch (BusinessRuleViolationException ex) when (IsXpLimitViolation(ex))
        {
            return;
        }

        if (points <= 0)
        {
            return;
        }

        var balance = await CurrentBalanceAsync(enrollmentId, ct);
        dbContext.XpLedgerEntries.Add(
            new XpLedgerEntry
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                Amount = points,
                Reason = milestone.Reason,
                SourceRefType = NbMilestoneSourceRefType,
                SourceRefId = completionId,
                RuleCode = ruleCode,
                MultiplierUsed = multiplierUsed,
                BalanceAfter = balance + points,
                AwardedAt = DateTime.UtcNow,
            }
        );

        // Notificación gamificada del hito de la racha del nutribiótico
        // (SPEC §20, C.3): best-effort (nunca lanza, AC-42).
        if (_gamifiedNotificationService is not null)
        {
            await _gamifiedNotificationService.NotifyAsync(
                patientId,
                "nb_milestone",
                "💊 ¡Nutriobiótico!",
                $"💊 ¡{milestone.Days} días tomando tu Nutriobiótico!",
                "high",
                ct
            );
        }
    }

    private const string NbMilestoneSourceRefType = "nb_milestone";

    /// <summary>
    /// Los topes anti-fraude del catálogo (SPEC §14.3) rechazan con 409 un
    /// otorgamiento que excede <c>max_per_day</c>/<c>max_per_week</c>. Para los
    /// hitos del nutribiótico ese rechazo es un "no-otorgar" esperado (nueva
    /// corrida dentro de la misma semana, SPEC §19, B.4), no un error de la
    /// completación.
    /// </summary>
    private static bool IsXpLimitViolation(BusinessRuleViolationException ex) =>
        ex.Message.StartsWith("XP_DAILY_LIMIT_REACHED")
        || ex.Message.StartsWith("XP_WEEKLY_LIMIT_REACHED");

    private sealed record NbMilestone(int Days, XpReason Reason, string RuleCode, int BaseXp);

    // --- T-77: Helpers para el configurador de contenido ---

    public async Task<(string Code, string Name)?> GetPlanNameAsync(
        Guid planId,
        CancellationToken ct = default
    )
    {
        var name = await dbContext
            .NutritionPlans.AsNoTracking()
            .Where(p => p.Id == planId)
            .Select(p => (string?)p.Name)
            .FirstOrDefaultAsync(ct);
        return name is not null ? (name, name) : null;
    }

    public async Task<(string Code, string Name)?> GetRoutineNameAsync(
        Guid routineId,
        CancellationToken ct = default
    )
    {
        var name = await dbContext
            .ExerciseRoutines.AsNoTracking()
            .Where(r => r.Id == routineId)
            .Select(r => (string?)r.Name)
            .FirstOrDefaultAsync(ct);
        return name is not null ? (name, name) : null;
    }

    private async Task<JsonElement> BuildSnapshotFromTemplateAsync(
        Guid templateId,
        CancellationToken ct
    )
    {
        var days = await dbContext
            .WeeklyDayTemplates.AsNoTracking()
            .Where(d => d.TemplateId == templateId)
            .OrderBy(d => d.Weekday)
            .ThenBy(d => d.SortOrder)
            .ToListAsync(ct);
        return BuildSnapshot(days);
    }

    /// <summary>Serialize el snapshot con el shape de la SPEC §3.4 (jsonb).</summary>
    private static JsonElement BuildSnapshot(IEnumerable<WeeklyDayTemplate> days) =>
        JsonSerializer.SerializeToElement(
            days.Select(d => new
            {
                weekday = (int)d.Weekday,
                task_code = d.TaskCode.ToString(),
                points = d.Points,
                sort_order = d.SortOrder,
                routine_id = d.RoutineId,
                nutrition_plan_id = d.NutritionPlanId,
            })
        );

    private static JsonElement EmptySnapshot() =>
        JsonSerializer.SerializeToElement(Array.Empty<object>());

    private static IReadOnlyList<SnapshotTask> ParseSnapshot(JsonElement snapshot)
    {
        var result = new List<SnapshotTask>();
        if (snapshot.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in snapshot.EnumerateArray())
        {
            Guid? routineId =
                item.TryGetProperty("routine_id", out var rProp)
                && rProp.ValueKind == JsonValueKind.String
                && Guid.TryParse(rProp.GetString(), out var rGuid)
                    ? rGuid
                    : null;
            Guid? nutPlanId =
                item.TryGetProperty("nutrition_plan_id", out var nProp)
                && nProp.ValueKind == JsonValueKind.String
                && Guid.TryParse(nProp.GetString(), out var nGuid)
                    ? nGuid
                    : null;

            result.Add(
                new SnapshotTask(
                    (short)item.GetProperty("weekday").GetInt32(),
                    item.GetProperty("task_code").GetString() ?? string.Empty,
                    item.GetProperty("points").GetInt32(),
                    item.GetProperty("sort_order").GetInt32(),
                    routineId,
                    nutPlanId
                )
            );
        }

        return result;
    }

    private async Task<IReadOnlyList<CalendarDayDto>> BuildCalendarDaysAsync(
        Guid enrollmentId,
        DateOnly from,
        DateOnly to,
        DateOnly today,
        CancellationToken ct
    )
    {
        var checkins = await dbContext
            .DailyCheckIns.AsNoTracking()
            .Where(c => c.EnrollmentId == enrollmentId && c.LocalDate >= from && c.LocalDate <= to)
            .Select(c => new
            {
                c.LocalDate,
                c.IsPerfectDay,
                c.TotalPoints,
                c.BonusAwarded,
            })
            .ToListAsync(ct);

        var completedDates = await dbContext
            .TaskCompletions.AsNoTracking()
            .Where(t => t.EnrollmentId == enrollmentId && t.LocalDate >= from && t.LocalDate <= to)
            .Select(t => t.LocalDate)
            .Distinct()
            .ToListAsync(ct);

        var result = new List<CalendarDayDto>();
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var checkin = checkins.FirstOrDefault(c => c.LocalDate == date);
            var hasCompletions = completedDates.Contains(date);

            string status;
            if (checkin?.IsPerfectDay == true)
            {
                status = "Completed";
            }
            else if (hasCompletions)
            {
                status = "InProgress";
            }
            else if (date < today)
            {
                status = "Missed";
            }
            else
            {
                status = "Pending";
            }

            result.Add(
                new CalendarDayDto(
                    date,
                    ToIsoWeekday(date),
                    checkin?.IsPerfectDay ?? false,
                    (checkin?.TotalPoints ?? 0) + (checkin?.BonusAwarded ?? 0),
                    status
                )
            );
        }

        return result;
    }

    // ---------------------------------------------------------------- Motor de puntajes (SPEC §13)

    public async Task<HealthScoreDto?> GetOrComputeHealthScoreAsync(
        Guid patientId,
        ScoreTrigger trigger,
        bool force = false,
        DateOnly? periodEndLocalDate = null,
        CancellationToken ct = default
    )
    {
        var enrollment = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EnrollmentWindow(e.Id, e.PatientId, e.Timezone))
            .FirstOrDefaultAsync(ct);
        if (enrollment is null)
        {
            return null;
        }

        // Período (SPEC §13.2): los últimos 7 días locales, ampliados hacia
        // atrás hasta la semana del programa si esta extiende más allá.
        var todayLocal = PatientLocalToday(enrollment.Timezone);

        // Recálculo manual clínico (SPEC §13.7.2): el fin del período puede
        // venir en el body. Una fecha futura respecto del hoy local del
        // paciente es 422 INVALID_PERIOD (la autoridad del tiempo local es
        // este repositorio, nunca DateTime.UtcNow del handler).
        if (periodEndLocalDate is { } overrideEnd && overrideEnd > todayLocal)
        {
            throw new UnprocessableEntityException(
                $"INVALID_PERIOD: periodEndLocalDate {overrideEnd:yyyy-MM-dd} es futura "
                    + $"(hoy local del paciente: {todayLocal:yyyy-MM-dd})."
            );
        }

        var periodEnd = periodEndLocalDate ?? todayLocal;
        var rollingStart = periodEnd.AddDays(-6);
        var week = await ResolveLatestWeekAsync(enrollment.EnrollmentId, ct);
        var periodStart =
            week is not null && week.WeekStartDateLocal < rollingStart
                ? week.WeekStartDateLocal
                : rollingStart;

        var existing = await dbContext
            .HealthScores.AsNoTracking()
            .FirstOrDefaultAsync(
                h =>
                    h.PatientId == patientId
                    && h.PeriodStart == periodStart
                    && h.PeriodEnd == periodEnd,
                ct
            );

        // Fila fresca del período (period_end == hoy): cache hit, sin recalcular
        // (SPEC §13.3). force = recálculo manual clínico → siempre recalcula.
        if (existing is not null && !force)
        {
            return ToHealthScoreDto(existing);
        }

        var input = await BuildHealthScoreInputAsync(enrollment, periodStart, periodEnd, ct);
        var context = new ScoreCalculationContext(patientId, periodStart, periodEnd, trigger);
        var result = _healthScoreCalculator.Calculate(input, context);

        // score_previous: el de la fila existente (historial conservado) o el
        // del período anterior persistido (la fila que NO es la actual).
        var previousScore =
            existing?.ScorePrevious
            ?? await dbContext
                .HealthScores.AsNoTracking()
                .Where(h => h.PatientId == patientId && h.PeriodEnd < periodStart)
                .OrderByDescending(h => h.PeriodEnd)
                .Select(h => (int?)h.Score)
                .FirstOrDefaultAsync(ct);

        var trend = DeriveTrend(result.Total, previousScore);
        var now = DateTime.UtcNow;

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (existing is not null)
            {
                existing.Score = result.Total;
                existing.ScoreAdherence = result.Adherence;
                existing.ScoreClinical = result.Clinical;
                existing.ScoreNutrition = result.Nutrition;
                existing.ScorePsychology = result.Psychology;
                existing.ScoreExercise = result.Exercise;
                existing.Trend = trend;
                existing.CalculatedAt = now;
                existing.UpdatedAt = now;
                dbContext.HealthScores.Update(existing);
                await dbContext.SaveChangesAsync(ct);
            }
            else
            {
                dbContext.HealthScores.Add(
                    new HealthScore
                    {
                        Id = Guid.NewGuid(),
                        PatientId = patientId,
                        Score = result.Total,
                        ScorePrevious = previousScore,
                        ScoreAdherence = result.Adherence,
                        ScoreClinical = result.Clinical,
                        ScoreNutrition = result.Nutrition,
                        ScorePsychology = result.Psychology,
                        ScoreExercise = result.Exercise,
                        Trend = trend,
                        PeriodStart = periodStart,
                        PeriodEnd = periodEnd,
                        CalculatedAt = now,
                        CreatedAt = now,
                    }
                );
                try
                {
                    await dbContext.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    // Carrera: otra petición persistió la fila del período;
                    // la unicidad (patient_id, period_start, period_end) la
                    // protege. Se devuelve el valor recién calculado (correcto).
                }
            }
        });

        return new HealthScoreDto(
            result.Total,
            previousScore,
            trend.ToString(),
            new HealthScoreDimensionsDto(
                result.Adherence,
                result.Clinical,
                result.Nutrition,
                result.Psychology,
                result.Exercise
            )
        );
    }

    public async Task<TransformationScoreDto?> GetOrComputeTransformationScoreAsync(
        Guid patientId,
        ScoreTrigger trigger,
        bool force = false,
        CancellationToken ct = default
    )
    {
        var enrollment = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EnrollmentWindow(e.Id, e.PatientId, e.Timezone))
            .FirstOrDefaultAsync(ct);
        if (enrollment is null)
        {
            return null;
        }

        // Semana vigente: la más reciente de la inscripción (la actual si está
        // activa; si ya pasó a Completed, la más reciente Completed, SPEC §13.2).
        var week = await ResolveLatestWeekAsync(enrollment.EnrollmentId, ct);
        if (week is null)
        {
            return null;
        }

        var weekNumber = week.WeekNumber;
        var existing = await dbContext
            .TransformationScores.AsNoTracking()
            .FirstOrDefaultAsync(t => t.PatientId == patientId && t.WeekNumber == weekNumber, ct);

        if (existing is not null && !force)
        {
            return ToTransformationScoreDto(existing);
        }

        var indicators = await LoadTransformationIndicatorsAsync(
            enrollment.PatientId,
            enrollment.Timezone,
            week.WeekStartDateLocal,
            week.WeekEndDateLocal,
            ct
        );
        var context = new ScoreCalculationContext(
            patientId,
            week.WeekStartDateLocal,
            week.WeekEndDateLocal,
            trigger
        );
        var result = _transformationScoreCalculator.Calculate(indicators, context);

        var previousScore =
            existing?.ScorePrevious
            ?? await dbContext
                .TransformationScores.AsNoTracking()
                .Where(t => t.PatientId == patientId && t.WeekNumber < weekNumber)
                .OrderByDescending(t => t.WeekNumber)
                .Select(t => (int?)t.Score)
                .FirstOrDefaultAsync(ct);

        var trend = DeriveTrend(result.Score, previousScore);
        var detail = result.Detail.ToDictionary(d => d.Key, d => ToIndicatorDetailDto(d.Value));
        var now = DateTime.UtcNow;

        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (existing is not null)
            {
                existing.Score = result.Score;
                existing.Detail = JsonSerializer.SerializeToElement(detail);
                existing.OverallTrend = trend;
                existing.CalculatedAt = now;
                existing.UpdatedAt = now;
                dbContext.TransformationScores.Update(existing);
                await dbContext.SaveChangesAsync(ct);
            }
            else
            {
                dbContext.TransformationScores.Add(
                    new TransformationScore
                    {
                        Id = Guid.NewGuid(),
                        PatientId = patientId,
                        Score = result.Score,
                        ScorePrevious = previousScore,
                        WeekNumber = weekNumber,
                        Detail = JsonSerializer.SerializeToElement(detail),
                        OverallTrend = trend,
                        CalculatedAt = now,
                        CreatedAt = now,
                    }
                );
                await dbContext.SaveChangesAsync(ct);
            }
        });

        return new TransformationScoreDto(
            result.Score,
            previousScore,
            trend.ToString(),
            weekNumber,
            detail
        );
    }

    public async Task<IReadOnlyList<ClinicalBaselineDto>> ListClinicalBaselinesAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .ClinicalBaselines.AsNoTracking()
            .Where(b => b.PatientId == patientId)
            .OrderBy(b => b.Metric!.Code)
            .Select(b => new ClinicalBaselineDto(
                b.Id,
                b.PatientId,
                b.MetricId,
                b.Metric!.Code,
                b.Value,
                b.UnitId,
                b.Unit!.Symbol,
                b.FavorableDirection,
                b.TargetValue,
                b.MeasuredAt,
                b.SetBy,
                b.CreatedAt
            ))
            .ToListAsync(ct);

    public async Task<ClinicalBaselineDto> UpsertClinicalBaselineAsync(
        ClinicalBaselineWrite input,
        IReadOnlyList<string> callerRoles,
        CancellationToken ct = default
    )
    {
        // AC-22: set_by es obligatorio (nunca se infiere ni se auto-asigna).
        if (input.SetBy == Guid.Empty)
        {
            throw new UnprocessableEntityException(
                "SET_BY_REQUIRED: la línea base clínica exige un set_by (usuario clínico de auth.users)."
            );
        }

        // AC-22: el llamador debe tener un rol clínico; un paciente auto-
        // asignándose una línea base → 403 FORBIDDEN (semántica exacta).
        if (!callerRoles.Any(role => ClinicianRoles.Contains(role)))
        {
            throw new ForbiddenException(
                "BASELINE_SET_BY_REQUIRES_CLINICIAN: solo un clínico puede fijar una línea base "
                    + "(Physician, Nutritionist, Psychologist, ClinicalDirector o Admin)."
            );
        }

        var metric =
            await dbContext
                .MeasurementMetrics.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == input.MetricId, ct)
            ?? throw new NotFoundException($"Métrica {input.MetricId} no encontrada.");

        if (input.Value <= 0m)
        {
            throw new UnprocessableEntityException(
                "BASELINE_VALUE_MUST_BE_POSITIVE: el valor de la línea base debe ser mayor que 0."
            );
        }

        // target_value: positivo y dentro de límites razonables (rango de
        // referencia de la métrica en la misma unidad, con tolerancia ±10%).
        if (input.TargetValue is { } target)
        {
            if (target <= 0m)
            {
                throw new UnprocessableEntityException(
                    "TARGET_VALUE_MUST_BE_POSITIVE: el target clínico debe ser mayor que 0."
                );
            }

            if (target > 1_000_000m)
            {
                throw new UnprocessableEntityException(
                    "TARGET_VALUE_OUT_OF_BOUNDS: el target clínico excede los límites razonables."
                );
            }

            var range = await dbContext
                .MeasurementReferenceRanges.AsNoTracking()
                .Where(r =>
                    r.MetricId == input.MetricId
                    && r.IsActive
                    && r.UnitId == input.UnitId
                    && r.MinValue.HasValue
                    && r.MaxValue.HasValue
                )
                .OrderByDescending(r => r.Priority)
                .FirstOrDefaultAsync(ct);
            if (
                range is not null
                && (target < range.MinValue!.Value * 0.9m || target > range.MaxValue!.Value * 1.1m)
            )
            {
                throw new UnprocessableEntityException(
                    "TARGET_VALUE_OUT_OF_BOUNDS: el target clínico está fuera de los límites razonables de la métrica."
                );
            }
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            var existing = await dbContext.ClinicalBaselines.FirstOrDefaultAsync(
                b => b.PatientId == input.PatientId && b.MetricId == input.MetricId,
                ct
            );

            if (existing is not null)
            {
                existing.Value = input.Value;
                existing.UnitId = input.UnitId;
                existing.FavorableDirection = input.FavorableDirection;
                existing.TargetValue = input.TargetValue;
                existing.MeasuredAt = input.MeasuredAt;
                existing.SetBy = input.SetBy;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                existing = new ClinicalBaseline
                {
                    Id = Guid.NewGuid(),
                    PatientId = input.PatientId,
                    MetricId = input.MetricId,
                    Value = input.Value,
                    UnitId = input.UnitId,
                    FavorableDirection = input.FavorableDirection,
                    TargetValue = input.TargetValue,
                    MeasuredAt = input.MeasuredAt,
                    SetBy = input.SetBy,
                    CreatedAt = DateTime.UtcNow,
                };
                dbContext.ClinicalBaselines.Add(existing);
            }

            await dbContext.SaveChangesAsync(ct);

            return await dbContext
                .ClinicalBaselines.AsNoTracking()
                .Where(b => b.Id == existing.Id)
                .Select(b => new ClinicalBaselineDto(
                    b.Id,
                    b.PatientId,
                    b.MetricId,
                    b.Metric!.Code,
                    b.Value,
                    b.UnitId,
                    b.Unit!.Symbol,
                    b.FavorableDirection,
                    b.TargetValue,
                    b.MeasuredAt,
                    b.SetBy,
                    b.CreatedAt
                ))
                .FirstAsync(ct);
        });
    }

    public async Task<decimal?> GetLatestMeasurementAsync(
        Guid patientId,
        Guid metricId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default
    )
    {
        var timezone = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .Select(e => (string?)e.Timezone)
            .FirstOrDefaultAsync(ct);
        if (timezone is null)
        {
            return null;
        }

        var tz = ResolveTimeZone(timezone);
        var fromUtc = LocalDateToUtcStart(fromDate, tz);
        var toExclusiveUtc = LocalDateToUtcStart(toDate.AddDays(1), tz);

        return await dbContext
            .ClinicalMeasurements.AsNoTracking()
            .Where(m =>
                m.PatientId == patientId
                && m.MetricId == metricId
                && m.ObservedAt >= fromUtc
                && m.ObservedAt < toExclusiveUtc
            )
            .OrderByDescending(m => m.ObservedAt)
            .Select(m => (decimal?)m.Value)
            .FirstOrDefaultAsync(ct);
    }

    // ------------------------------------------------------- XP clínica (SPEC §15)

    /// <summary>
    /// Motor de XP clínica (SPEC §15, C). Se invoca SOLO desde
    /// <c>POST /scores/calculate</c> (el GET /scores nunca otorga XP clínica):
    /// tras persistir la fila de <c>health_scores</c> del período, evalúa cada
    /// línea base con medición en la misma ventana que el Índice de
    /// Transformación y clasifica por dirección favorable + |Δ%|:
    ///
    /// - favorable y |Δ%| ≥ umbral (default 5%) → revisión <c>pending</c> en
    ///   <c>app.clinical_xp_reviews</c> (NO otorga XP aún; la decide un clínico).
    /// - favorable y 1% ≤ |Δ%| &lt; umbral → auto-otorga <c>CLINICAL_IMPROVE</c>.
    /// - |Δ%| &lt; 1% → auto-otorga <c>CLINICAL_STABLE</c>.
    /// - desfavorable → NO otorga nada (0 XP; nunca penaliza, SPEC §15, AC-27).
    /// - todas las métricas con medición favorables → auto-otorga
    ///   <c>CLINICAL_WEEKLY_ALL_UP</c> una vez por período.
    ///
    /// Idempotencia (SPEC §15, C.3): el dedupe parcial
    /// <c>uq_xp_ledger_source_dedupe (source_ref_type, source_ref_id, reason)</c>
    /// con <c>source_ref_type = 'clinical_period'</c> y <c>source_ref_id =
    /// health_scores.id</c> limita a un otorgamiento por regla y período; ante
    /// violación de unicidad (carrera) se omite (nunca doble XP).
    /// </summary>
    public async Task<ClinicalXpEvaluationResult> EvaluateClinicalXpAwardsAsync(
        Guid patientId,
        DateOnly? periodEndLocalDate = null,
        CancellationToken ct = default
    )
    {
        var enrollment = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EnrollmentWindow(e.Id, e.PatientId, e.Timezone))
            .FirstOrDefaultAsync(ct);
        if (enrollment is null)
        {
            return ClinicalXpEvaluationResult.Empty;
        }

        // Mismo período que el Índice de Salud (SPEC §13.2): la fila de
        // health_scores ya fue persistida por GetOrComputeHealthScoreAsync
        // (force) justo antes de esta llamada.
        var todayLocal = PatientLocalToday(enrollment.Timezone);
        if (periodEndLocalDate is { } overrideEnd && overrideEnd > todayLocal)
        {
            throw new UnprocessableEntityException(
                $"INVALID_PERIOD: periodEndLocalDate {overrideEnd:yyyy-MM-dd} es futura "
                    + $"(hoy local del paciente: {todayLocal:yyyy-MM-dd})."
            );
        }

        var periodEnd = periodEndLocalDate ?? todayLocal;
        var rollingStart = periodEnd.AddDays(-6);
        var week = await ResolveLatestWeekAsync(enrollment.EnrollmentId, ct);
        var periodStart =
            week is not null && week.WeekStartDateLocal < rollingStart
                ? week.WeekStartDateLocal
                : rollingStart;

        var healthScore = await dbContext
            .HealthScores.AsNoTracking()
            .FirstOrDefaultAsync(
                h =>
                    h.PatientId == patientId
                    && h.PeriodStart == periodStart
                    && h.PeriodEnd == periodEnd,
                ct
            );
        if (healthScore is null)
        {
            return ClinicalXpEvaluationResult.Empty;
        }

        // Pares (línea base, medición más reciente del período) con el id y
        // nombre de la métrica (misma data que el calculador de Transformación).
        var indicators = await LoadClinicalPeriodIndicatorsAsync(
            enrollment.PatientId,
            enrollment.Timezone,
            periodStart,
            periodEnd,
            ct
        );
        if (indicators.Count == 0)
        {
            return ClinicalXpEvaluationResult.Empty;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var reviewsCreated = 0;
                var awardedRules = new List<string>();
                var totalXp = 0;
                var anyMeasuredMetric = false;
                var allFavorable = true;

                foreach (var indicator in indicators)
                {
                    var delta = indicator.Current - indicator.Baseline;
                    // baseline > 0 siempre (validación de la línea base: value > 0).
                    var deltaPct =
                        indicator.Baseline == 0m ? 0m : delta / indicator.Baseline * 100m;
                    var magnitude = Math.Abs(deltaPct);
                    var favorable = deltaPct * (int)indicator.FavorableDirection > 0m;

                    // Desfavorable → 0 XP (nunca penaliza, AC-27) y rompe el
                    // "all up" del período.
                    if (!favorable || magnitude < 1m)
                    {
                        allFavorable = false;
                    }

                    if (!favorable)
                    {
                        continue;
                    }

                    anyMeasuredMetric = true;

                    if (magnitude >= _clinicalSignificantThresholdPct)
                    {
                        // Mejoría significativa: revisión pending (una por
                        // período × métrica, único (patient, health_score,
                        // metric)); NO se otorga XP hasta la decisión clínica.
                        var exists = await dbContext
                            .ClinicalXpReviews.AsNoTracking()
                            .AnyAsync(
                                r =>
                                    r.PatientId == patientId
                                    && r.HealthScoreId == healthScore.Id
                                    && r.MetricId == indicator.MetricId,
                                ct
                            );
                        if (!exists)
                        {
                            dbContext.ClinicalXpReviews.Add(
                                new ClinicalXpReview
                                {
                                    Id = Guid.NewGuid(),
                                    PatientId = patientId,
                                    HealthScoreId = healthScore.Id,
                                    MetricId = indicator.MetricId,
                                    RuleCode = XpRuleCodes.ClinicalSignificant,
                                    DeltaPct = Math.Round(
                                        deltaPct,
                                        3,
                                        MidpointRounding.AwayFromZero
                                    ),
                                    Status = ClinicalXpReviewStatus.pending,
                                    CreatedAt = DateTime.UtcNow,
                                }
                            );
                            reviewsCreated++;
                        }
                    }
                    else if (magnitude >= 1m)
                    {
                        // Mejoría favorable moderada: auto-otorga CLINICAL_IMPROVE.
                        var awarded = await AwardPeriodXpAsync(
                            enrollment.EnrollmentId,
                            enrollment.Timezone,
                            healthScore.Id,
                            XpRuleCodes.ClinicalImprove,
                            XpReason.CLINICAL_IMPROVE,
                            50,
                            null,
                            ct
                        );
                        if (awarded > 0)
                        {
                            totalXp += awarded;
                            awardedRules.Add(XpRuleCodes.ClinicalImprove);
                        }
                    }
                    else
                    {
                        // Estable (< 1%): auto-otorga CLINICAL_STABLE.
                        var awarded = await AwardPeriodXpAsync(
                            enrollment.EnrollmentId,
                            enrollment.Timezone,
                            healthScore.Id,
                            XpRuleCodes.ClinicalStable,
                            XpReason.CLINICAL_STABLE,
                            20,
                            null,
                            ct
                        );
                        if (awarded > 0)
                        {
                            totalXp += awarded;
                            awardedRules.Add(XpRuleCodes.ClinicalStable);
                        }
                    }
                }

                // Semana clínica "todo en verde": TODAS las métricas con
                // medición en el período son favorables (mejoraron). Una sola
                // vez por período (dedupe del libro mayor).
                if (anyMeasuredMetric && allFavorable)
                {
                    var awarded = await AwardPeriodXpAsync(
                        enrollment.EnrollmentId,
                        enrollment.Timezone,
                        healthScore.Id,
                        XpRuleCodes.ClinicalWeeklyAllUp,
                        XpReason.CLINICAL_WEEKLY_ALL_UP,
                        150,
                        null,
                        ct
                    );
                    if (awarded > 0)
                    {
                        totalXp += awarded;
                        awardedRules.Add(XpRuleCodes.ClinicalWeeklyAllUp);
                    }
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return new ClinicalXpEvaluationResult(
                    reviewsCreated,
                    totalXp,
                    awardedRules.Distinct().ToList()
                );
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Carrera (otra petición ya evaluó el período): el dedupe
                // parcial del libro mayor y el único de revisión protegen;
                // se omite sin doble otorgamiento (SPEC §15, C.3).
                await transaction.RollbackAsync(ct);
                return ClinicalXpEvaluationResult.Empty;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<(
        IReadOnlyList<ClinicalReviewDto> Items,
        int Total
    )> ListPendingClinicalReviewsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = dbContext
            .ClinicalXpReviews.AsNoTracking()
            .Where(r => r.Status == ClinicalXpReviewStatus.pending);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(r => new ClinicalReviewDto(
                r.Id,
                r.PatientId,
                r.MetricId,
                r.Metric!.Code,
                r.Metric!.Name,
                r.DeltaPct,
                r.RuleCode,
                r.Status,
                r.HealthScore!.PeriodStart,
                r.HealthScore!.PeriodEnd,
                r.CreatedAt,
                r.DecidedBy,
                r.DecidedAt
            ))
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// Decisión de una revisión clínica de XP (SPEC §15, D). Guardia clínica
    /// AC-22 (misma que <see cref="UpsertClinicalBaselineAsync"/>): el actor
    /// debe existir y tener un rol clínico; un paciente decidiendo su propia XP
    /// significativa → <see cref="ForbiddenException"/> (403). Aprobada → se
    /// otorga <c>CLINICAL_SIGNIFICANT</c> con <c>validated_by</c>/<c>validated_at</c>
    /// (cuenta en los totales desde la aprobación); rechazada → sin XP. Ya
    /// decidida → 409 <c>REVIEW_ALREADY_DECIDED</c>.
    /// </summary>
    public async Task<ClinicalReviewDto> DecideClinicalXpReviewAsync(
        Guid reviewId,
        bool approve,
        Guid? actorId,
        IReadOnlyList<string> callerRoles,
        CancellationToken ct = default
    )
    {
        // Guardia AC-22: rol clínico obligatorio (nunca un paciente).
        if (actorId is null || !callerRoles.Any(role => ClinicianRoles.Contains(role)))
        {
            throw new ForbiddenException(
                "CLINICAL_REVIEW_REQUIRES_CLINICIAN: solo un clínico puede decidir una "
                    + "revisión de XP (Physician, Nutritionist, Psychologist, ClinicalDirector o Admin)."
            );
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var review =
                    await dbContext.ClinicalXpReviews.FirstOrDefaultAsync(r => r.Id == reviewId, ct)
                    ?? throw new NotFoundException(
                        $"Revisión clínica de XP {reviewId} no encontrada."
                    );

                if (review.Status != ClinicalXpReviewStatus.pending)
                {
                    throw new BusinessRuleViolationException(
                        "REVIEW_ALREADY_DECIDED: la revisión clínica de XP ya fue decidida."
                    );
                }

                var now = DateTime.UtcNow;
                review.DecidedBy = actorId;
                review.DecidedAt = now;
                review.UpdatedAt = now;

                if (approve)
                {
                    review.Status = ClinicalXpReviewStatus.approved;

                    // Inscripción activa del paciente (destino del otorgamiento).
                    // Si ya no existe inscripción activa (p. ej. retirada tras
                    // crear la revisión), la revisión se aprueba igual pero sin
                    // XP: no hay libro mayor al que acreditar.
                    var enrollment = await dbContext
                        .ProgramEnrollments.AsNoTracking()
                        .Where(e =>
                            e.PatientId == review.PatientId
                            && e.Status == ProgramEnrollmentStatus.Active
                        )
                        .OrderByDescending(e => e.CreatedAt)
                        .Select(e => new EnrollmentWindow(e.Id, e.PatientId, e.Timezone))
                        .FirstOrDefaultAsync(ct);

                    if (enrollment is not null)
                    {
                        // Otorgamiento validado: validated_by/validated_at =
                        // decisión del clínico (SPEC §15, E: cuenta en totales).
                        await AwardPeriodXpAsync(
                            enrollment.EnrollmentId,
                            enrollment.Timezone,
                            review.HealthScoreId,
                            XpRuleCodes.ClinicalSignificant,
                            XpReason.CLINICAL_SIGNIFICANT,
                            100,
                            actorId,
                            ct
                        );
                    }
                }
                else
                {
                    review.Status = ClinicalXpReviewStatus.rejected;
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return await dbContext
                    .ClinicalXpReviews.AsNoTracking()
                    .Where(r => r.Id == review.Id)
                    .Select(r => new ClinicalReviewDto(
                        r.Id,
                        r.PatientId,
                        r.MetricId,
                        r.Metric!.Code,
                        r.Metric!.Name,
                        r.DeltaPct,
                        r.RuleCode,
                        r.Status,
                        r.HealthScore!.PeriodStart,
                        r.HealthScore!.PeriodEnd,
                        r.CreatedAt,
                        r.DecidedBy,
                        r.DecidedAt
                    ))
                    .FirstAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    // ------------------------------------------------------- Nutrición granular (SPEC §18)

    /// <summary>
    /// Fuente de referencia del libro mayor para la XP granular por log de
    /// comida/hidratación (SPEC §18, B): <c>source_ref_type = 'habit_log'</c> y
    /// <c>source_ref_id = habit_check.id</c> — un otorgamiento por log (dedupe
    /// parcial <c>(source_ref_type, source_ref_id, reason)</c>).
    /// </summary>
    private const string HabitLogSourceRefType = "habit_log";

    /// <summary>
    /// Fuente de referencia del libro mayor para los otorgamientos semanales de
    /// nutrición (SPEC §18, C): <c>source_ref_type = 'nutrition_period'</c> y
    /// <c>source_ref_id = health_scores.id</c> — un otorgamiento por regla y
    /// período clínico, misma idempotencia que la XP clínica.
    /// </summary>
    private const string NutritionPeriodSourceRefType = "nutrition_period";

    /// <summary>Puntos base por defecto del log granular de comida (SPEC §18, A).</summary>
    private const int NutritionMealDefaultPoints = 10;

    /// <summary>Puntos base por defecto del log granular de hidratación (SPEC §18, A).</summary>
    private const int NutritionHydrationDefaultPoints = 5;

    /// <summary>Puntos base por defecto de la adherencia semanal ≥ 85% (SPEC §18, A).</summary>
    private const int NutritionWeek85BaseXp = 75;

    /// <summary>Puntos base por defecto de la recuperación nutricional (+20pp, SPEC §18, A).</summary>
    private const int NutritionRecoveryBaseXp = 50;

    /// <summary>Umbral de adherencia semanal para <c>NUTRITION_WEEK_85</c> (85%).</summary>
    private const int NutritionWeekThresholdPct = 85;

    /// <summary>Mejora mínima de adherencia vs el período anterior para <c>NUTRITION_RECOVERY</c> (+20 puntos).</summary>
    private const int NutritionRecoveryMinImprovementPct = 20;

    /// <summary>
    /// Registra una comida/hidratación del paciente (SPEC §18, B): dentro de
    /// una transacción con la inscripción activa bloqueada <c>FOR UPDATE</c>
    /// (serializa los otorgamientos de XP), crea el <c>app.habit_checks</c> de
    /// <c>(paciente, plantilla de hábito, fecha local)</c> y otorga la XP
    /// granular por el camino de resolución del catálogo (SPEC §14.3 + §16:
    /// multiplicador del paciente y topes <c>max_per_day</c>/<c>max_per_week</c>
    /// — 4/día para comidas, 1/día para hidratación, sembrados en §18, A).
    ///
    /// El <c>localDate</c> opcional se resuelve contra el hoy local del paciente
    /// (una fecha futura → 422 <c>INVALID_DATE</c>, misma autoridad de tiempo
    /// que el resto del módulo). Un log duplicado (la comida/hidratación de esa
    /// fecha ya está registrada) → 409 <c>HABIT_ALREADY_LOGGED</c> (semántica
    /// de "ya registrado" del módulo, precedente <c>REVIEW_ALREADY_DECIDED</c>);
    /// la XP nunca se duplica — el único de <c>habit_checks</c> y el dedupe
    /// parcial <c>('habit_log', habit_check.id, reason)</c> del libro mayor son
    /// el backstop de carrera. La tarea <c>nut</c> del programa NO se
    /// auto-completa aquí: el log granular es aditivo y su flujo queda intacto
    /// en <c>CompleteTaskAsync</c>.
    /// </summary>
    public async Task<NutritionLogResultDto> LogNutritionAsync(
        Guid patientId,
        MealCode mealCode,
        DateOnly? localDate = null,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                // Inscripción activa del paciente, bloqueada FOR UPDATE (misma
                // técnica que CompleteTaskCoreAsync: Npgsql 10 no expone
                // .ForUpdate(); el lock se toma por SQL directo).
                var enrollment =
                    await dbContext
                        .ProgramEnrollments.FromSqlInterpolated(
                            $"SELECT * FROM app.program_enrollments WHERE patient_id = {patientId} AND status = 'Active' ORDER BY created_at DESC LIMIT 1 FOR UPDATE"
                        )
                        .FirstOrDefaultAsync(ct)
                    ?? throw new NotFoundException(
                        $"NO_ACTIVE_ENROLLMENT: no existe una inscripción activa para el paciente {patientId}."
                    );

                // Plantilla de hábito del código de comida (des/alm/mer/cen/agua).
                var template =
                    await dbContext
                        .HabitTemplates.AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Code == mealCode.ToString(), ct)
                    ?? throw new UnprocessableEntityException(
                        $"MEAL_CODE_UNKNOWN: la plantilla de hábito {mealCode} no está sembrada."
                    );

                // Fecha local del paciente; una fecha futura es 422 INVALID_DATE
                // (el repositorio es la autoridad del tiempo local, nunca
                // DateTime.UtcNow del handler).
                var todayLocal = PatientLocalToday(enrollment.Timezone);
                var logDate = localDate ?? todayLocal;
                if (logDate > todayLocal)
                {
                    throw new UnprocessableEntityException(
                        $"INVALID_DATE: la fecha {logDate:yyyy-MM-dd} es futura "
                            + $"(hoy local del paciente: {todayLocal:yyyy-MM-dd})."
                    );
                }

                // Upsert idempotente (único por (paciente, plantilla, fecha)):
                // si la comida/hidratación de esa fecha ya está registrada, es
                // un intento duplicado → 409 HABIT_ALREADY_LOGGED.
                var existing = await dbContext
                    .HabitChecks.AsNoTracking()
                    .FirstOrDefaultAsync(
                        h =>
                            h.PatientId == patientId
                            && h.HabitTemplateId == template.Id
                            && h.LocalDate == logDate,
                        ct
                    );
                if (existing is not null)
                {
                    throw new BusinessRuleViolationException(
                        "HABIT_ALREADY_LOGGED: la comida/hidratación de esa fecha ya fue registrada."
                    );
                }

                var check = new HabitCheck
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    HabitTemplateId = template.Id,
                    LocalDate = logDate,
                    IsDone = true,
                };
                dbContext.HabitChecks.Add(check);

                // XP granular por el camino del catálogo: la categoría de la
                // plantilla decide la regla (comidas 'alimentacion' →
                // NUTRITION_MEAL_COMPLETE; agua → NUTRITION_HYDRATION). El
                // ResolveXpAwardAsync aplica el multiplicador del paciente y
                // los topes max_per_day/max_per_week (SPEC §14.3 + §16).
                var isMeal = template.Category == "alimentacion";
                var ruleCode = isMeal
                    ? XpRuleCodes.NutritionMealComplete
                    : XpRuleCodes.NutritionHydration;
                var defaultPoints = isMeal
                    ? NutritionMealDefaultPoints
                    : NutritionHydrationDefaultPoints;
                var (points, resolvedRuleCode, multiplierUsed) = await ResolveXpAwardAsync(
                    enrollment.Id,
                    enrollment.Timezone,
                    ruleCode,
                    defaultPoints,
                    logDate,
                    ct
                );

                var balance = await CurrentBalanceAsync(enrollment.Id, ct);
                var now = DateTime.UtcNow;
                dbContext.XpLedgerEntries.Add(
                    new XpLedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        EnrollmentId = enrollment.Id,
                        Amount = points,
                        Reason = isMeal
                            ? XpReason.NUTRITION_MEAL_COMPLETE
                            : XpReason.NUTRITION_HYDRATION,
                        SourceRefType = HabitLogSourceRefType,
                        SourceRefId = check.Id,
                        RuleCode = resolvedRuleCode,
                        MultiplierUsed = multiplierUsed,
                        BalanceAfter = balance + points,
                        AwardedAt = now,
                    }
                );

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return new NutritionLogResultDto(
                    check.Id,
                    template.Code,
                    logDate,
                    check.IsDone,
                    points,
                    balance + points
                );
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Carrera: otra petición registró la misma comida/hidratación.
                // El único de habit_checks y el dedupe parcial del libro mayor
                // protegen; el duplicado se reporta con la misma semántica 409.
                await transaction.RollbackAsync(ct);
                throw new BusinessRuleViolationException(
                    "HABIT_ALREADY_LOGGED: la comida/hidratación de esa fecha ya fue registrada."
                );
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// Motor de otorgamientos semanales de nutrición (SPEC §18, C). Se invoca
    /// SOLO desde <c>POST /scores/calculate</c> (el GET /scores nunca otorga
    /// XP), tras persistir la fila de <c>health_scores</c> del período:
    ///
    /// 1. Adherencia del período = MISMA fuente que la dimensión de nutrición
    ///    del Índice de Salud (SPEC §13.4.3: <c>app.habit_checks</c> categoría
    ///    <c>alimentacion</c>); reutiliza <see cref="GetNutritionLogAsync"/> —
    ///    no duplica la query. Sin logs en el período → adherencia 0 (sin
    ///    otorgamiento, nunca penaliza, AC-35).
    /// 2. Adherencia ≥ 85% → auto-otorga <c>NUTRITION_WEEK_85</c> (75 XP) una
    ///    vez por período.
    /// 3. Adherencia − adherencia del período anterior ≥ 20 puntos porcentuales
    ///    (la fila previa de <c>health_scores</c>) → auto-otorga
    ///    <c>NUTRITION_RECOVERY</c> (50 XP) una vez por período.
    ///
    /// Idempotencia (SPEC §18, C): el dedupe parcial
    /// <c>uq_xp_ledger_source_dedupe (source_ref_type, source_ref_id, reason)</c>
    /// con <c>source_ref_type = 'nutrition_period'</c> y <c>source_ref_id =
    /// health_scores.id</c> limita a un otorgamiento por regla y período; ante
    /// violación de unicidad (carrera) se omite (nunca doble XP). Respeta el
    /// multiplicador del paciente (SPEC §16, C.4) vía
    /// <see cref="AwardPeriodXpAsync"/>.
    /// </summary>
    public async Task<NutritionWeeklyAwardsResult> EvaluateNutritionAwardsAsync(
        Guid patientId,
        DateOnly? periodEndLocalDate = null,
        CancellationToken ct = default
    )
    {
        var enrollment = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EnrollmentWindow(e.Id, e.PatientId, e.Timezone))
            .FirstOrDefaultAsync(ct);
        if (enrollment is null)
        {
            return NutritionWeeklyAwardsResult.Empty;
        }

        // Mismo período que el Índice de Salud (SPEC §13.2): la fila de
        // health_scores ya fue persistida por GetOrComputeHealthScoreAsync
        // (force) justo antes de esta llamada.
        var todayLocal = PatientLocalToday(enrollment.Timezone);
        if (periodEndLocalDate is { } overrideEnd && overrideEnd > todayLocal)
        {
            throw new UnprocessableEntityException(
                $"INVALID_PERIOD: periodEndLocalDate {overrideEnd:yyyy-MM-dd} es futura "
                    + $"(hoy local del paciente: {todayLocal:yyyy-MM-dd})."
            );
        }

        var periodEnd = periodEndLocalDate ?? todayLocal;
        var rollingStart = periodEnd.AddDays(-6);
        var week = await ResolveLatestWeekAsync(enrollment.EnrollmentId, ct);
        var periodStart =
            week is not null && week.WeekStartDateLocal < rollingStart
                ? week.WeekStartDateLocal
                : rollingStart;

        var healthScore = await dbContext
            .HealthScores.AsNoTracking()
            .FirstOrDefaultAsync(
                h =>
                    h.PatientId == patientId
                    && h.PeriodStart == periodStart
                    && h.PeriodEnd == periodEnd,
                ct
            );
        if (healthScore is null)
        {
            return NutritionWeeklyAwardsResult.Empty;
        }

        // Adherencia del período (SPEC §18, C): reutiliza GetNutritionLogAsync
        // (la misma fuente de la dimensión nutrition del Índice de Salud,
        // SPEC §13.4.3). Sin logs de alimentación en el período → adherencia 0
        // → no hay cumplimiento semanal que premiar (sin penalización).
        var nutrition = await GetNutritionLogAsync(patientId, periodStart, periodEnd, ct);
        if (nutrition is null || nutrition.Total == 0)
        {
            return NutritionWeeklyAwardsResult.Empty;
        }

        var adherence = (int)
            Math.Round(
                nutrition.Achieved * 100d / nutrition.Total,
                0,
                MidpointRounding.AwayFromZero
            );

        // Adherencia del período anterior = dimensión de nutrición de la fila
        // previa de health_scores (el período anterior persistido, SPEC §18, C.3).
        var previousAdherence = await dbContext
            .HealthScores.AsNoTracking()
            .Where(h => h.PatientId == patientId && h.PeriodEnd < periodStart)
            .OrderByDescending(h => h.PeriodEnd)
            .Select(h => (int?)h.ScoreNutrition)
            .FirstOrDefaultAsync(ct);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var awardedRules = new List<string>();
                var totalXp = 0;

                // 85% de adherencia en el período → NUTRITION_WEEK_85 (AC-35).
                if (adherence >= NutritionWeekThresholdPct)
                {
                    var awarded = await AwardPeriodXpAsync(
                        enrollment.EnrollmentId,
                        enrollment.Timezone,
                        healthScore.Id,
                        XpRuleCodes.NutritionWeek85,
                        XpReason.NUTRITION_WEEK_85,
                        NutritionWeek85BaseXp,
                        null,
                        ct,
                        sourceRefType: NutritionPeriodSourceRefType
                    );
                    if (awarded > 0)
                    {
                        totalXp += awarded;
                        awardedRules.Add(XpRuleCodes.NutritionWeek85);
                    }
                }

                // Recuperación: adherencia ≥ +20pp vs el período anterior
                // (AC-36). Sin período anterior no hay recuperación que medir.
                if (
                    previousAdherence is not null
                    && adherence - previousAdherence.Value >= NutritionRecoveryMinImprovementPct
                )
                {
                    var awarded = await AwardPeriodXpAsync(
                        enrollment.EnrollmentId,
                        enrollment.Timezone,
                        healthScore.Id,
                        XpRuleCodes.NutritionRecovery,
                        XpReason.NUTRITION_RECOVERY,
                        NutritionRecoveryBaseXp,
                        null,
                        ct,
                        sourceRefType: NutritionPeriodSourceRefType
                    );
                    if (awarded > 0)
                    {
                        totalXp += awarded;
                        awardedRules.Add(XpRuleCodes.NutritionRecovery);
                    }
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return new NutritionWeeklyAwardsResult(totalXp, awardedRules.Distinct().ToList());
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Carrera (otra petición ya evaluó el período): el dedupe
                // parcial del libro mayor protege; se omite sin doble
                // otorgamiento (SPEC §18, C).
                await transaction.RollbackAsync(ct);
                return NutritionWeeklyAwardsResult.Empty;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    // ------------------------------------------------------- Detección de debilidades (SPEC §21, "Paso 7c")

    /// <summary>
    /// Código canónico de la métrica de glucosa en <c>app.measurement_metrics</c>
    /// (SPEC §21, B.3 — WK_CLIN_GLUCOSE_HIGH). REQUIRES_CLINICAL_VALIDATION:
    /// umbral propuesto, pendiente de confirmación del comité clínico.
    /// </summary>
    private const string GlucoseMetricCode = "glucose";

    /// <summary>
    /// Código canónico de la métrica de % grasa corporal (SPEC §21, B.4 —
    /// WK_CLIN_BODYFAT_UP).
    /// </summary>
    private const string BodyFatMetricCode = "body_fat";

    /// <summary>
    /// Códigos de regla que el motor de detección considera "activas" para el
    /// dedupe AC-43: se omite la detección si ya existe una fila en cualquiera
    /// de estos estados con el mismo código del paciente.
    /// </summary>
    private static readonly WeaknessStatus[] ActiveWeaknessStatuses =
    [
        WeaknessStatus.open,
        WeaknessStatus.acknowledged,
        WeaknessStatus.in_intervention,
    ];

    /// <summary>
    /// Reúne el paquete semanal del paciente para el motor de detección
    /// (SPEC §21, B): MISMA ventana que el Índice de Salud (SPEC §13.2) con
    /// queries set-based (sin N+1). Los indicadores sin fuente física quedan
    /// null → sus reglas no disparan (nunca penaliza por ausencia de datos).
    /// Devuelve null si el paciente no tiene inscripción activa.
    /// </summary>
    public async Task<PatientWeeklyData?> BuildPatientWeeklyDataAsync(
        Guid patientId,
        DateOnly? periodEndLocalDate = null,
        CancellationToken ct = default
    )
    {
        var enrollment = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EnrollmentWindow(e.Id, e.PatientId, e.Timezone))
            .FirstOrDefaultAsync(ct);
        if (enrollment is null)
        {
            return null;
        }

        var todayLocal = PatientLocalToday(enrollment.Timezone);
        if (periodEndLocalDate is { } overrideEnd && overrideEnd > todayLocal)
        {
            throw new UnprocessableEntityException(
                $"INVALID_PERIOD: periodEndLocalDate {overrideEnd:yyyy-MM-dd} es futura "
                    + $"(hoy local del paciente: {todayLocal:yyyy-MM-dd})."
            );
        }

        var periodEnd = periodEndLocalDate ?? todayLocal;
        var rollingStart = periodEnd.AddDays(-6);
        var week = await ResolveLatestWeekAsync(enrollment.EnrollmentId, ct);
        var periodStart =
            week is not null && week.WeekStartDateLocal < rollingStart
                ? week.WeekStartDateLocal
                : rollingStart;

        // Adherencia nutricional (SPEC §21, B.1/B.2): MISMA fuente que la
        // dimensión nutrition del Health Score y que los premios semanales de
        // SPEC §18 — reutiliza GetNutritionLogAsync, no duplica la query.
        var nutrition = await GetNutritionLogAsync(
            enrollment.PatientId,
            periodStart,
            periodEnd,
            ct
        );
        decimal? nutritionAdherence = null;
        if (nutrition is { Total: > 0 })
        {
            nutritionAdherence = Math.Round(
                nutrition.Achieved * 100m / nutrition.Total,
                0,
                MidpointRounding.AwayFromZero
            );
        }

        // Indicadores clínicos (glucosa + % grasa) desde líneas base y la
        // última medición del período (misma data que los calculadores).
        var baselines = await dbContext
            .ClinicalBaselines.AsNoTracking()
            .Where(b => b.PatientId == patientId)
            .Select(b => new
            {
                b.MetricId,
                Code = b.Metric!.Code,
                b.Value,
            })
            .ToListAsync(ct);
        var latestByMetric = await LoadLatestMeasurementPerMetricAsync(
            enrollment.PatientId,
            enrollment.Timezone,
            periodStart,
            periodEnd,
            ct
        );

        decimal? glucoseCurrent = null;
        string? glucoseTrend = null;
        Guid? glucoseMetricId = null;
        var glucoseBaseline = baselines.FirstOrDefault(b => b.Code == GlucoseMetricCode);
        if (
            glucoseBaseline is not null
            && latestByMetric.TryGetValue(glucoseBaseline.MetricId, out var glucose)
        )
        {
            glucoseCurrent = glucose;
            glucoseMetricId = glucoseBaseline.MetricId;
            glucoseTrend =
                glucose > glucoseBaseline.Value ? "up"
                : glucose < glucoseBaseline.Value ? "down"
                : "stable";
        }

        decimal? bodyFatDelta = null;
        Guid? bodyFatMetricId = null;
        var bodyFatBaseline = baselines.FirstOrDefault(b => b.Code == BodyFatMetricCode);
        if (
            bodyFatBaseline is not null
            && latestByMetric.TryGetValue(bodyFatBaseline.MetricId, out var bodyFat)
        )
        {
            bodyFatDelta = bodyFat - bodyFatBaseline.Value;
            bodyFatMetricId = bodyFatBaseline.MetricId;
        }

        // Motivación (SPEC §21, B.5): proxy del último registro emocional del
        // período — el módulo solo persiste mood 1..5, se escala ×2 a la escala
        // 1..10 del motor (documentado en SPEC §21, B.5).
        decimal? motivationScore = null;
        var latestMood = await dbContext
            .EmotionalRecords.AsNoTracking()
            .Where(r =>
                r.PatientId == patientId
                && r.RecordedLocalDate >= periodStart
                && r.RecordedLocalDate <= periodEnd
            )
            .OrderByDescending(r => r.RecordedLocalDate)
            .Select(r => (short?)r.MoodScore)
            .FirstOrDefaultAsync(ct);
        if (latestMood is { } mood)
        {
            motivationScore = mood * 2m;
        }

        // Estrés y sueño (SPEC §21, B.6/B.7): sin fuente física en el esquema
        // actual → null → reglas latentes (no disparan). Se dejan en el paquete
        // para cuando una fuente futura alimente el dato (contrato forward).
        decimal? stressScore = null;
        decimal? avgSleepHours = null;

        // Adherencia semanal (SPEC §21, B.8): dimensión adherence de la fila de
        // health_scores del período (persistida por POST /scores/calculate
        // justo antes de la detección, SPEC §13.4.1).
        decimal? weeklyAdherence = null;
        var healthScoreAdherence = await dbContext
            .HealthScores.AsNoTracking()
            .Where(h =>
                h.PatientId == patientId && h.PeriodStart == periodStart && h.PeriodEnd == periodEnd
            )
            .Select(h => (int?)h.ScoreAdherence)
            .FirstOrDefaultAsync(ct);
        if (healthScoreAdherence is { } adherence)
        {
            weeklyAdherence = adherence;
        }

        // Constancia del nutribiótico a 7 días (SPEC §21, B.9): días con la
        // tarea completada en la ventana / 7.
        var nbDays = await dbContext
            .TaskCompletions.AsNoTracking()
            .Where(t =>
                t.EnrollmentId == enrollment.EnrollmentId
                && t.TaskCode == TaskCode.nutribiotico
                && t.LocalDate >= periodStart
                && t.LocalDate <= periodEnd
            )
            .Select(t => t.LocalDate)
            .Distinct()
            .CountAsync(ct);
        decimal? nbAdherence7d = Math.Round(nbDays * 100m / 7m, 0, MidpointRounding.AwayFromZero);

        // Cumplimiento de ejercicio (SPEC §21, B.10): días con la tarea en la
        // ventana / días del período.
        var exerciseDays = await dbContext
            .TaskCompletions.AsNoTracking()
            .Where(t =>
                t.EnrollmentId == enrollment.EnrollmentId
                && t.TaskCode == TaskCode.ejercicio
                && t.LocalDate >= periodStart
                && t.LocalDate <= periodEnd
            )
            .Select(t => t.LocalDate)
            .Distinct()
            .CountAsync(ct);
        var dayCount = periodEnd.DayNumber - periodStart.DayNumber + 1;
        decimal? exerciseCompletionPct = Math.Round(
            exerciseDays * 100m / dayCount,
            0,
            MidpointRounding.AwayFromZero
        );

        return new PatientWeeklyData(
            enrollment.PatientId,
            nutritionAdherence,
            glucoseCurrent,
            glucoseTrend,
            glucoseMetricId,
            bodyFatDelta,
            bodyFatMetricId,
            motivationScore,
            stressScore,
            avgSleepHours,
            weeklyAdherence,
            nbAdherence7d,
            exerciseCompletionPct
        );
    }

    /// <summary>
    /// Persiste las debilidades NUEVAS detectadas (SPEC §21, C — AC-43): omite
    /// los descriptores cuyo código ya tiene una fila
    /// <c>open</c>/<c>acknowledged</c>/<c>in_intervention</c> del mismo
    /// paciente (sin duplicados mientras estén abiertas). Las filas se crean
    /// con <c>source = 'ai'</c> y <c>status = 'open'</c>; si un clínico valida
    /// después, el origen permanece <c>ai</c> (SPEC §21, A). Devuelve cuántas
    /// filas nuevas se persistieron.
    /// </summary>
    public async Task<IReadOnlyList<Weakness>> PersistDetectedWeaknessesAsync(
        Guid patientId,
        IReadOnlyList<WeaknessDescriptor> descriptors,
        CancellationToken ct = default
    )
    {
        if (descriptors.Count == 0)
        {
            return [];
        }

        var activeCodes = await dbContext
            .Weaknesses.AsNoTracking()
            .Where(w => w.PatientId == patientId && ActiveWeaknessStatuses.Contains(w.Status))
            .Select(w => w.Code)
            .Distinct()
            .ToListAsync(ct);

        var fresh = descriptors.Where(d => !activeCodes.Contains(d.Code)).ToList();
        if (fresh.Count == 0)
        {
            return [];
        }

        var now = DateTime.UtcNow;
        var newWeaknesses = fresh
            .Select(d => new Weakness
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                Code = d.Code,
                Category = d.Category,
                Severity = d.Severity,
                Title = d.Title,
                Description = d.Description,
                DetectedAt = now,
                MetricId = d.MetricId,
                IndicatorValue = d.IndicatorValue,
                Source = WeaknessSource.ai,
                Status = WeaknessStatus.open,
                CreatedAt = now,
            })
            .ToList();

        dbContext.Weaknesses.AddRange(newWeaknesses);
        await dbContext.SaveChangesAsync(ct);

        // Devolver las debilidades con sus IDs generados para que el servicio
        // de detección pueda crear intervenciones derivadas (SPEC §22, "Paso 7d").
        return newWeaknesses;
    }

    /// <summary>
    /// Debilidades del paciente (SPEC §21, D): listado paginado ordenado por
    /// <c>detected_at</c> descendente (la más reciente primero), con el total
    /// de la consulta.
    /// </summary>
    public async Task<(IReadOnlyList<WeaknessDto> Items, int Total)> ListWeaknessesAsync(
        Guid patientId,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.Weaknesses.AsNoTracking().Where(w => w.PatientId == patientId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(w => w.DetectedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(w => ToWeaknessDto(w))
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// Cola clínica de debilidades abiertas (SPEC §21, D): listado paginado de
    /// las filas <c>status = 'open'</c> ordenadas por <c>detected_at</c>
    /// ascendente (FIFO de la cola clínica, la más antigua primero — misma
    /// semántica que las revisiones de XP pendientes).
    /// </summary>
    public async Task<(IReadOnlyList<WeaknessDto> Items, int Total)> ListOpenWeaknessesAsync(
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.Weaknesses.AsNoTracking().Where(w => w.Status == WeaknessStatus.open);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(w => w.DetectedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(w => ToWeaknessDto(w))
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// Transición de estado de una debilidad (SPEC §21, D — AC-44). Guardia
    /// clínica AC-22 (misma que la línea base clínica y la revisión de XP): el
    /// actor debe existir y tener un rol clínico; un paciente cambiando el
    /// estado de su propia debilidad → 403 FORBIDDEN. Estados válidos:
    /// <c>acknowledged</c>, <c>in_intervention</c>, <c>resolved</c> (fija
    /// <c>resolved_at</c>) y <c>dismissed</c>. Transición idempotente: aplicar
    /// el mismo estado devuelve la fila sin error. Desconocida → 404.
    /// </summary>
    public async Task<WeaknessDto> UpdateWeaknessStatusAsync(
        Guid weaknessId,
        WeaknessStatus status,
        Guid? actorId,
        IReadOnlyList<string> callerRoles,
        CancellationToken ct = default
    )
    {
        // Guardia AC-22: rol clínico obligatorio (nunca un paciente).
        if (actorId is null || !callerRoles.Any(role => ClinicianRoles.Contains(role)))
        {
            throw new ForbiddenException(
                "WEAKNESS_STATUS_REQUIRES_CLINICIAN: solo un clínico puede cambiar el "
                    + "estado de una debilidad (Physician, Nutritionist, Psychologist, "
                    + "ClinicalDirector o Admin)."
            );
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var weakness =
                    await dbContext.Weaknesses.FirstOrDefaultAsync(w => w.Id == weaknessId, ct)
                    ?? throw new NotFoundException($"Debilidad {weaknessId} no encontrada.");

                var now = DateTime.UtcNow;
                weakness.Status = status;
                weakness.UpdatedAt = now;
                weakness.ResolvedAt = status == WeaknessStatus.resolved ? now : null;

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return await dbContext
                    .Weaknesses.AsNoTracking()
                    .Where(w => w.Id == weakness.Id)
                    .Select(w => ToWeaknessDto(w))
                    .FirstAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    private static WeaknessDto ToWeaknessDto(Weakness w) =>
        new(
            w.Id,
            w.PatientId,
            w.Code,
            w.Category.ToString(),
            w.Severity.ToString(),
            w.Title,
            w.Description,
            w.DetectedAt,
            w.MetricId,
            w.IndicatorValue,
            w.Source.ToString(),
            w.Status.ToString(),
            w.AssignedTo,
            w.ResolvedAt,
            w.CreatedAt,
            w.UpdatedAt
        );

    private static InterventionDto ToInterventionDto(Intervention i) =>
        new(
            i.Id,
            i.PatientId,
            i.WeaknessId,
            i.Type.ToString(),
            i.Title,
            i.Description,
            i.Status.ToString(),
            i.Severity,
            i.AssignedTo,
            i.RecommendedAt,
            i.AcceptedAt,
            i.CompletedAt,
            i.PatientAction,
            i.Result,
            i.XpAwardedTotal,
            i.CreatedAt,
            i.UpdatedAt
        );

    // --- Intervenciones (SPEC §22, "Paso 7d") ---

    /// <summary>
    /// Crea una intervención desde una debilidad (SPEC §22, C — AC-46): verifica
    /// que no exista ya una intervención vinculada a esa debilidad (una por
    /// debilidad). La debilidad se transiciona a <c>in_intervention</c> si estaba
    /// en <c>open</c>/<c>acknowledged</c>. Otorga <c>WEAKNESS_ASSESS</c> (+20)
    /// una vez (dedupe parcial <c>'intervention'</c>). Devuelve la intervención
    /// creada o la existente si ya había una.
    /// </summary>
    public async Task<Intervention> EnsureInterventionFromWeaknessAsync(
        Guid patientId,
        Guid weaknessId,
        InterventionType type,
        string title,
        string? description,
        Guid? actorId,
        CancellationToken ct = default
    )
    {
        // Verificar si ya existe una intervención para esta debilidad (AC-46).
        var existing = await dbContext
            .Interventions.AsNoTracking()
            .FirstOrDefaultAsync(i => i.WeaknessId == weaknessId, ct);
        if (existing is not null)
        {
            return existing;
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var now = DateTime.UtcNow;

                // Transicionar la debilidad a in_intervention si estaba abierta.
                var weakness = await dbContext.Weaknesses.FirstOrDefaultAsync(
                    w => w.Id == weaknessId,
                    ct
                );
                if (
                    weakness is not null
                    && weakness.Status is WeaknessStatus.open or WeaknessStatus.acknowledged
                )
                {
                    weakness.Status = WeaknessStatus.in_intervention;
                    weakness.UpdatedAt = now;
                }

                // Determinar la severidad de la intervención desde la debilidad.
                var severity = weakness?.Severity.ToString() ?? "medium";

                var intervention = new Intervention
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    WeaknessId = weaknessId,
                    Type = type,
                    Title = title,
                    Description = description,
                    Status = InterventionStatus.detected,
                    Severity = severity,
                    CreatedAt = now,
                };

                dbContext.Interventions.Add(intervention);

                // Otorgar WEAKNESS_ASSESS (+20) una vez por intervención.
                // Necesitamos la inscripción activa del paciente para el
                // otorgamiento de XP.
                var enrollmentId = await dbContext
                    .ProgramEnrollments.Where(e =>
                        e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active
                    )
                    .OrderByDescending(e => e.CreatedAt)
                    .Select(e => (Guid?)e.Id)
                    .FirstOrDefaultAsync(ct);

                if (enrollmentId is not null)
                {
                    var awardResult = await AwardInterventionXpAsync(
                        enrollmentId.Value,
                        intervention.Id,
                        XpRuleCodes.WeaknessAssess,
                        XpReason.WEAKNESS_ASSESS,
                        20,
                        validatedBy: null,
                        sourceRefType: "intervention",
                        ct
                    );
                    intervention.XpAwardedTotal += awardResult;
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return intervention;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// Intervenciones del paciente (SPEC §22, D): listado paginado ordenado por
    /// <c>created_at</c> descendente.
    /// </summary>
    public async Task<(IReadOnlyList<InterventionDto> Items, int Total)> ListInterventionsAsync(
        Guid patientId,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.Interventions.AsNoTracking().Where(i => i.PatientId == patientId);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(i => i.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(i => ToInterventionDto(i))
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// Cola clínica de intervenciones abiertas (SPEC §22, D): listado paginado
    /// de filas con <c>status != 'completed'</c> ordenadas por <c>created_at</c>
    /// ascendente (FIFO).
    /// </summary>
    public async Task<(IReadOnlyList<InterventionDto> Items, int Total)> ListOpenInterventionsAsync(
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .Interventions.AsNoTracking()
            .Where(i => i.Status != InterventionStatus.completed);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(i => i.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(i => ToInterventionDto(i))
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// Paciente acepta una intervención (SPEC §22, D — AC-47): transición
    /// <c>detected→accepted</c>, fija <c>accepted_at</c>, otorga
    /// <c>INTERV_ACCEPT</c> (+15). Si es <c>recovery_mission</c> también
    /// <c>RECOVERY_MISSION</c> (+50). Requiere que la intervención pertenezca
    /// al paciente. Estado inválido → 409.
    /// </summary>
    public async Task<InterventionDto> AcceptInterventionAsync(
        Guid interventionId,
        Guid patientId,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var intervention =
                    await dbContext.Interventions.FirstOrDefaultAsync(
                        i => i.Id == interventionId,
                        ct
                    )
                    ?? throw new NotFoundException($"Intervención {interventionId} no encontrada.");

                if (intervention.PatientId != patientId)
                {
                    throw new NotFoundException($"Intervención {interventionId} no encontrada.");
                }

                if (intervention.Status != InterventionStatus.detected)
                {
                    throw new BusinessRuleViolationException(
                        $"INTERVENTION_WRONG_STATE: la intervención está en estado "
                            + $"'{intervention.Status}' y solo puede aceptarse desde 'detected'."
                    );
                }

                var now = DateTime.UtcNow;
                intervention.Status = InterventionStatus.accepted;
                intervention.AcceptedAt = now;
                intervention.UpdatedAt = now;

                // Otorgar INTERV_ACCEPT (+15).
                var awardResult = await AwardInterventionXpAsync(
                    await GetEnrollmentIdForPatientAsync(patientId, ct),
                    intervention.Id,
                    XpRuleCodes.IntervAccept,
                    XpReason.INTERV_ACCEPT,
                    15,
                    validatedBy: null,
                    sourceRefType: "intervention",
                    ct
                );
                intervention.XpAwardedTotal += awardResult;

                // Si es recovery_mission, también otorgar RECOVERY_MISSION (+50).
                if (intervention.Type == InterventionType.recovery_mission)
                {
                    var recoveryResult = await AwardInterventionXpAsync(
                        await GetEnrollmentIdForPatientAsync(patientId, ct),
                        intervention.Id,
                        XpRuleCodes.RecoveryMission,
                        XpReason.RECOVERY_MISSION,
                        50,
                        validatedBy: null,
                        sourceRefType: "intervention",
                        ct
                    );
                    intervention.XpAwardedTotal += recoveryResult;
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return await dbContext
                    .Interventions.AsNoTracking()
                    .Where(i => i.Id == intervention.Id)
                    .Select(i => ToInterventionDto(i))
                    .FirstAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// Clínico actualiza el estado de una intervención (SPEC §22, D — AC-48):
    /// transiciones con auditoría; <c>completed</c> REQUIRES resultado y otorga
    /// <c>INTERV_COMPLETE</c> (+200, validated_by = clínico) + debilidad
    /// vinculada → <c>resolved</c>. <c>in_progress</c> es el estado por defecto
    /// tras tele-asistencia confirmada. Estado inválido → 409.
    /// </summary>
    public async Task<InterventionDto> UpdateInterventionStatusAsync(
        Guid interventionId,
        InterventionStatus status,
        Guid? actorId,
        IReadOnlyList<string> callerRoles,
        string? result = null,
        Guid? assignedTo = null,
        CancellationToken ct = default
    )
    {
        // Guardia AC-22: rol clínico obligatorio.
        if (actorId is null || !callerRoles.Any(role => ClinicianRoles.Contains(role)))
        {
            throw new ForbiddenException(
                "INTERVENTION_STATUS_REQUIRES_CLINICIAN: solo un clínico puede cambiar el "
                    + "estado de una intervención."
            );
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var intervention =
                    await dbContext.Interventions.FirstOrDefaultAsync(
                        i => i.Id == interventionId,
                        ct
                    )
                    ?? throw new NotFoundException($"Intervención {interventionId} no encontrada.");

                var now = DateTime.UtcNow;

                // Validar transición.
                if (!IsValidInterventionTransition(intervention.Status, status))
                {
                    throw new BusinessRuleViolationException(
                        $"INTERVENTION_WRONG_STATE: transición de '{intervention.Status}' "
                            + $"a '{status}' no es válida."
                    );
                }

                // completed REQUIRES resultado.
                if (status == InterventionStatus.completed && string.IsNullOrWhiteSpace(result))
                {
                    throw new BusinessRuleViolationException(
                        "INTERVENTION_COMPLETED_REQUIRES_RESULT: se requiere un resultado "
                            + "al completar una intervención."
                    );
                }

                intervention.Status = status;
                intervention.UpdatedAt = now;

                if (status == InterventionStatus.recommended)
                    intervention.RecommendedAt = now;
                if (status == InterventionStatus.completed)
                    intervention.CompletedAt = now;

                if (assignedTo.HasValue)
                    intervention.AssignedTo = assignedTo;
                if (result is not null)
                    intervention.Result = result;

                // Si se completó, otorgar INTERV_COMPLETE (+200, validated_by)
                // y marcar la debilidad vinculada como resolved.
                if (status == InterventionStatus.completed)
                {
                    var enrollmentId = await GetEnrollmentIdForPatientAsync(
                        intervention.PatientId,
                        ct
                    );

                    var awardResult = await AwardInterventionXpAsync(
                        enrollmentId,
                        intervention.Id,
                        XpRuleCodes.IntervComplete,
                        XpReason.INTERV_COMPLETE,
                        200,
                        validatedBy: actorId,
                        sourceRefType: "intervention",
                        ct
                    );
                    intervention.XpAwardedTotal += awardResult;

                    // Marcar la debilidad vinculada como resolved (AC-48).
                    if (intervention.WeaknessId.HasValue)
                    {
                        var weakness = await dbContext.Weaknesses.FirstOrDefaultAsync(
                            w => w.Id == intervention.WeaknessId.Value,
                            ct
                        );
                        if (weakness is not null && weakness.Status != WeaknessStatus.resolved)
                        {
                            weakness.Status = WeaknessStatus.resolved;
                            weakness.ResolvedAt = now;
                            weakness.UpdatedAt = now;
                        }
                    }
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return await dbContext
                    .Interventions.AsNoTracking()
                    .Where(i => i.Id == intervention.Id)
                    .Select(i => ToInterventionDto(i))
                    .FirstAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// Hook de telemedicina: teleconsulta agendada (SPEC §22, D — AC-49):
    /// otorga <c>TELE_SCHEDULE</c> (+50) y fija una nota. No cambia el estado.
    /// </summary>
    public async Task<InterventionDto> MarkTeleScheduledAsync(
        Guid interventionId,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var intervention =
                    await dbContext.Interventions.FirstOrDefaultAsync(
                        i => i.Id == interventionId,
                        ct
                    )
                    ?? throw new NotFoundException($"Intervención {interventionId} no encontrada.");

                var now = DateTime.UtcNow;
                intervention.UpdatedAt = now;

                // Otorgar TELE_SCHEDULE (+50).
                var enrollmentId = await GetEnrollmentIdForPatientAsync(intervention.PatientId, ct);
                var awardResult = await AwardInterventionXpAsync(
                    enrollmentId,
                    intervention.Id,
                    XpRuleCodes.TeleSchedule,
                    XpReason.TELE_SCHEDULE,
                    50,
                    validatedBy: null,
                    sourceRefType: "intervention",
                    ct
                );
                intervention.XpAwardedTotal += awardResult;

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return await dbContext
                    .Interventions.AsNoTracking()
                    .Where(i => i.Id == intervention.Id)
                    .Select(i => ToInterventionDto(i))
                    .FirstAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// Hook de telemedicina: asistencia confirmada por el clínico
    /// (SPEC §22, D — AC-49): otorga <c>TELE_ATTEND</c> (+100, validated_by =
    /// clínico) y mueve la intervención a <c>in_progress</c>.
    /// </summary>
    public async Task<InterventionDto> MarkTeleAttendedAsync(
        Guid interventionId,
        Guid clinicianId,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var intervention =
                    await dbContext.Interventions.FirstOrDefaultAsync(
                        i => i.Id == interventionId,
                        ct
                    )
                    ?? throw new NotFoundException($"Intervención {interventionId} no encontrada.");

                var now = DateTime.UtcNow;
                intervention.Status = InterventionStatus.in_progress;
                intervention.UpdatedAt = now;

                // Otorgar TELE_ATTEND (+100, validated_by = clinicianId).
                var enrollmentId = await GetEnrollmentIdForPatientAsync(intervention.PatientId, ct);
                var awardResult = await AwardInterventionXpAsync(
                    enrollmentId,
                    intervention.Id,
                    XpRuleCodes.TeleAttend,
                    XpReason.TELE_ATTEND,
                    100,
                    validatedBy: clinicianId,
                    sourceRefType: "intervention",
                    ct
                );
                intervention.XpAwardedTotal += awardResult;

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return await dbContext
                    .Interventions.AsNoTracking()
                    .Where(i => i.Id == intervention.Id)
                    .Select(i => ToInterventionDto(i))
                    .FirstAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// Hook de telemedicina: cumplimiento evaluado (SPEC §22, D — AC-49):
    /// otorga <c>TELE_COMPLY</c> (+50) y puede avanzar hacia
    /// <c>completed</c>.
    /// </summary>
    public async Task<InterventionDto> MarkTeleComplyAsync(
        Guid interventionId,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var intervention =
                    await dbContext.Interventions.FirstOrDefaultAsync(
                        i => i.Id == interventionId,
                        ct
                    )
                    ?? throw new NotFoundException($"Intervención {interventionId} no encontrada.");

                var now = DateTime.UtcNow;
                intervention.UpdatedAt = now;

                // Otorgar TELE_COMPLY (+50).
                var enrollmentId = await GetEnrollmentIdForPatientAsync(intervention.PatientId, ct);
                var awardResult = await AwardInterventionXpAsync(
                    enrollmentId,
                    intervention.Id,
                    XpRuleCodes.TeleComply,
                    XpReason.TELE_COMPLY,
                    50,
                    validatedBy: null,
                    sourceRefType: "intervention",
                    ct
                );
                intervention.XpAwardedTotal += awardResult;

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                return await dbContext
                    .Interventions.AsNoTracking()
                    .Where(i => i.Id == intervention.Id)
                    .Select(i => ToInterventionDto(i))
                    .FirstAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    /// <summary>
    /// Otorga XP de intervención en el libro mayor con el camino del catálogo.
    /// Devuelve los puntos otorgados (0 si la regla no está vigente o ya se
    /// otorgó — dedupe parcial).
    /// </summary>
    private async Task<int> AwardInterventionXpAsync(
        Guid enrollmentId,
        Guid interventionId,
        string ruleCode,
        XpReason reason,
        int fallbackBaseXp,
        Guid? validatedBy,
        string sourceRefType,
        CancellationToken ct
    )
    {
        var rule = await ResolveActiveRuleAsync(ruleCode, ct);
        if (rule is null)
        {
            return 0;
        }

        // Dedupe: ya otorgado para esta intervención y razón.
        var alreadyAwarded = await dbContext
            .XpLedgerEntries.AsNoTracking()
            .AnyAsync(
                x =>
                    x.EnrollmentId == enrollmentId
                    && x.SourceRefType == sourceRefType
                    && x.SourceRefId == interventionId
                    && x.Reason == reason,
                ct
            );
        if (alreadyAwarded)
        {
            return 0;
        }

        var patientMultiplier = await ResolvePatientMultiplierAsync(enrollmentId, ct);
        var effectiveMultiplier = rule.Multiplier * patientMultiplier;
        var total = (int)Math.Floor((rule.BaseXp ?? fallbackBaseXp) * effectiveMultiplier);
        var balance = await CurrentBalanceAsync(enrollmentId, ct);
        var now = DateTime.UtcNow;

        dbContext.XpLedgerEntries.Add(
            new XpLedgerEntry
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                Amount = total,
                Reason = reason,
                SourceRefType = sourceRefType,
                SourceRefId = interventionId,
                RuleCode = rule.Code,
                MultiplierUsed = effectiveMultiplier,
                BalanceAfter = balance + total,
                AwardedAt = now,
                ValidatedBy = validatedBy,
                ValidatedAt = validatedBy is null ? null : now,
            }
        );

        return total;
    }

    /// <summary>
    /// Resuelve la inscripción activa del paciente (una por paciente, misma
    /// resolución que <see cref="GetEnrollmentIdForPatientAsync"/>).
    /// </summary>
    private async Task<Guid> GetEnrollmentIdForPatientAsync(Guid patientId, CancellationToken ct)
    {
        return await dbContext
            .ProgramEnrollments.Where(e =>
                e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active
            )
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => e.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Valida la transición de estado de una intervención (SPEC §22, D).
    /// </summary>
    private static bool IsValidInterventionTransition(
        InterventionStatus from,
        InterventionStatus to
    )
    {
        return from switch
        {
            InterventionStatus.detected => to
                is InterventionStatus.evaluated
                    or InterventionStatus.recommended
                    or InterventionStatus.accepted,
            InterventionStatus.evaluated => to
                is InterventionStatus.recommended
                    or InterventionStatus.in_progress
                    or InterventionStatus.reevaluation,
            InterventionStatus.recommended => to
                is InterventionStatus.accepted
                    or InterventionStatus.in_progress,
            InterventionStatus.accepted => to
                is InterventionStatus.in_progress
                    or InterventionStatus.reevaluation,
            InterventionStatus.in_progress => to
                is InterventionStatus.completed
                    or InterventionStatus.reevaluation,
            InterventionStatus.reevaluation => to
                is InterventionStatus.evaluated
                    or InterventionStatus.recommended
                    or InterventionStatus.completed,
            InterventionStatus.completed => false, // terminal
            _ => false,
        };
    }

    /// <summary>
    /// Pares (línea base, medición más reciente del período) para el motor de
    /// XP clínica: igual que <see cref="LoadClinicalIndicatorsAsync"/> pero
    /// conservando el id y el nombre de la métrica (necesarios para las filas
    /// de <c>app.clinical_xp_reviews</c> y su wire shape).
    /// </summary>
    private async Task<IReadOnlyList<ClinicalPeriodIndicator>> LoadClinicalPeriodIndicatorsAsync(
        Guid patientId,
        string timezone,
        DateOnly from,
        DateOnly to,
        CancellationToken ct
    )
    {
        var baselines = await dbContext
            .ClinicalBaselines.AsNoTracking()
            .Where(b => b.PatientId == patientId)
            .Select(b => new
            {
                b.MetricId,
                Code = b.Metric!.Code,
                Name = b.Metric.Name,
                b.Value,
                b.FavorableDirection,
            })
            .ToListAsync(ct);
        if (baselines.Count == 0)
        {
            return [];
        }

        var latestByMetric = await LoadLatestMeasurementPerMetricAsync(
            patientId,
            timezone,
            from,
            to,
            ct
        );

        return baselines
            .Where(b => latestByMetric.ContainsKey(b.MetricId))
            .Select(b => new ClinicalPeriodIndicator(
                b.MetricId,
                b.Code,
                b.Name,
                b.Value,
                latestByMetric[b.MetricId],
                b.FavorableDirection
            ))
            .ToList();
    }

    /// <summary>
    /// Otorga una XP de período en el libro mayor (SPEC §15, C.3/D para la
    /// clínica; SPEC §18, C para la nutrición semanal): resuelve la regla
    /// vigente del catálogo (sin regla activa NO otorga — el catálogo es la
    /// fuente de verdad de la XP de período), aplica <c>base_xp × multiplier ×
    /// multiplicador del paciente</c> y escribe la entrada con el
    /// <c>source_ref_type</c> del período (<c>'clinical_period'</c> o
    /// <c>'nutrition_period'</c>) y <c>source_ref_id = health_scores.id</c>. El
    /// dedupe parcial <c>(source_ref_type, source_ref_id, reason)</c> limita a
    /// un otorgamiento por regla y período (el pre-cheque lo evita sin depender
    /// de la excepción de BD). Cuando <paramref name="validatedBy"/> viene
    /// (aprobación de una significativa), fija <c>validated_by</c>/
    /// <c>validated_at</c>: la entrada cuenta en los totales de XP.
    /// Devuelve los puntos otorgados (0 = sin otorgamiento).
    /// </summary>
    private async Task<int> AwardPeriodXpAsync(
        Guid enrollmentId,
        string timezone,
        Guid healthScoreId,
        string ruleCode,
        XpReason reason,
        int fallbackBaseXp,
        Guid? validatedBy,
        CancellationToken ct,
        string sourceRefType = "clinical_period"
    )
    {
        var rule = await ResolveActiveRuleAsync(ruleCode, ct);
        if (rule is null)
        {
            // Fallback documentado (SPEC §15, C.3 y §18, C): sin regla vigente
            // en el catálogo no hay otorgamiento de período (a diferencia de
            // las TASK_*, aquí NO hay comportamiento por defecto: el
            // otorgamiento ES la regla).
            return 0;
        }

        var alreadyAwarded = await dbContext
            .XpLedgerEntries.AsNoTracking()
            .AnyAsync(
                x =>
                    x.EnrollmentId == enrollmentId
                    && x.SourceRefType == sourceRefType
                    && x.SourceRefId == healthScoreId
                    && x.Reason == reason,
                ct
            );
        if (alreadyAwarded)
        {
            return 0;
        }

        // Multiplicador del paciente (SPEC §16, C.4): la XP de período (clínica
        // y nutrición semanal) también se multiplica mientras haya un
        // multiplicador vigente (vencido/null → 1.0 con reset lazy).
        var patientMultiplier = await ResolvePatientMultiplierAsync(enrollmentId, ct);
        var effectiveMultiplier = rule.Multiplier * patientMultiplier;
        var total = (int)Math.Floor((rule.BaseXp ?? fallbackBaseXp) * effectiveMultiplier);
        var balance = await CurrentBalanceAsync(enrollmentId, ct);
        var now = DateTime.UtcNow;

        dbContext.XpLedgerEntries.Add(
            new XpLedgerEntry
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                Amount = total,
                Reason = reason,
                SourceRefType = sourceRefType,
                SourceRefId = healthScoreId,
                RuleCode = rule.Code,
                MultiplierUsed = effectiveMultiplier,
                BalanceAfter = balance + total,
                AwardedAt = now,
                // ValidatedBy/ValidatedAt solo en la aprobación de una regla con
                // requires_validation = true (CLINICAL_SIGNIFICANT). Las reglas
                // sin validación (IMPROVE/STABLE/ALL_UP y las NUTRITION_*) quedan
                // null y cuentan en los totales (SPEC §15, E).
                ValidatedBy = validatedBy,
                ValidatedAt = validatedBy is null ? null : now,
            }
        );

        return total;
    }

    private sealed record ClinicalPeriodIndicator(
        Guid MetricId,
        string MetricCode,
        string MetricName,
        decimal Baseline,
        decimal Current,
        FavorableDirection FavorableDirection
    );

    // ------------------------------------------------------- helpers del motor de puntajes

    /// <summary>
    /// Reúne los datos de ventana del Índice de Salud con queries set-based
    /// (sin N+1): check-ins, congelamientos consumidos, ejercicio, registros
    /// emocionales, hábitos de alimentación, líneas base + mediciones y pesos.
    /// </summary>
    private async Task<HealthScoreInput> BuildHealthScoreInputAsync(
        EnrollmentWindow enrollment,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken ct
    )
    {
        var checkins = await dbContext
            .DailyCheckIns.AsNoTracking()
            .Where(c =>
                c.EnrollmentId == enrollment.EnrollmentId
                && c.LocalDate >= periodStart
                && c.LocalDate <= periodEnd
            )
            .Select(c => new { c.LocalDate, c.IsPerfectDay })
            .ToListAsync(ct);

        var consumedFreezeDates = await dbContext
            .StreakFreezes.AsNoTracking()
            .Where(f =>
                f.EnrollmentId == enrollment.EnrollmentId
                && f.Kind == StreakFreezeKind.Consumed
                && f.UsedOnLocalDate >= periodStart
                && f.UsedOnLocalDate <= periodEnd
            )
            .Select(f => f.UsedOnLocalDate!.Value)
            .Distinct()
            .ToListAsync(ct);

        var exerciseDays = await dbContext
            .TaskCompletions.AsNoTracking()
            .Where(t =>
                t.EnrollmentId == enrollment.EnrollmentId
                && t.TaskCode == TaskCode.ejercicio
                && t.LocalDate >= periodStart
                && t.LocalDate <= periodEnd
            )
            .Select(t => t.LocalDate)
            .Distinct()
            .CountAsync(ct);

        var moodScores = await dbContext
            .EmotionalRecords.AsNoTracking()
            .Where(r =>
                r.PatientId == enrollment.PatientId
                && r.RecordedLocalDate >= periodStart
                && r.RecordedLocalDate <= periodEnd
            )
            .Select(r => r.MoodScore)
            .ToListAsync(ct);

        var nutrition = await GetNutritionLogAsync(
            enrollment.PatientId,
            periodStart,
            periodEnd,
            ct
        );

        var clinicalIndicators = await LoadClinicalIndicatorsAsync(
            enrollment.PatientId,
            enrollment.Timezone,
            periodStart,
            periodEnd,
            ct
        );

        var weights = await LoadWeightsOrDefaultAsync(ct);

        // Estado por día del período (SPEC §13.4.1): perfecto > parcial >
        // rescatado (congelamiento consumido) > perdido.
        var days = new List<AdherenceDayStatus>();
        for (var date = periodStart; date <= periodEnd; date = date.AddDays(1))
        {
            var checkin = checkins.FirstOrDefault(c => c.LocalDate == date);
            if (checkin?.IsPerfectDay == true)
            {
                days.Add(AdherenceDayStatus.Perfect);
            }
            else if (checkin is not null)
            {
                days.Add(AdherenceDayStatus.Partial);
            }
            else if (consumedFreezeDates.Contains(date))
            {
                days.Add(AdherenceDayStatus.Rescued);
            }
            else
            {
                days.Add(AdherenceDayStatus.Missed);
            }
        }

        return new HealthScoreInput(
            weights,
            days,
            clinicalIndicators,
            nutrition,
            moodScores,
            exerciseDays,
            days.Count
        );
    }

    /// <summary>
    /// Hábitos de alimentación del período (SPEC §13.4.3). <c>app.habit_checks</c>
    /// / <c>app.habit_templates</c> aún no existen en el esquema (el módulo de
    /// hábitos no está implementado): la query es forward-compatible y ante
    /// tabla inexistente (42P01) devuelve null → <c>nutrition = 0</c> (default
    /// sin logs). Contrato provisional de columnas: <c>habit_checks(patient_id,
    /// habit_template_id, local_date, is_done)</c> y
    /// <c>habit_templates(id, category)</c> con <c>category = 'alimentacion'</c>.
    /// </summary>
    private async Task<NutritionLog?> GetNutritionLogAsync(
        Guid patientId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct
    )
    {
        try
        {
            var rows = await dbContext
                .Database.SqlQueryRaw<NutritionCountRow>(
                    """
                    SELECT
                        COUNT(*) FILTER (WHERE h.is_done)::int AS "Achieved",
                        COUNT(*)::int AS "Total"
                    FROM app.habit_checks h
                    INNER JOIN app.habit_templates ht ON ht.id = h.habit_template_id
                    WHERE h.patient_id = {0}
                      AND h.local_date >= {1} AND h.local_date <= {2}
                      AND ht.category = 'alimentacion'
                    """,
                    patientId,
                    from,
                    to
                )
                .ToListAsync(ct);

            var row = rows.FirstOrDefault();
            return row is null ? null : new NutritionLog(row.Achieved, row.Total);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            // Tabla de hábitos aún no creada: sin logs → nutrition = 0 (§13.4.3).
            return null;
        }
    }

    /// <summary>
    /// Pares (línea base, medición más reciente del período) para el Índice de
    /// Salud: una sola query set para la última medición por métrica (sin N+1).
    /// </summary>
    private async Task<IReadOnlyList<ClinicalIndicator>> LoadClinicalIndicatorsAsync(
        Guid patientId,
        string timezone,
        DateOnly from,
        DateOnly to,
        CancellationToken ct
    )
    {
        var baselines = await dbContext
            .ClinicalBaselines.AsNoTracking()
            .Where(b => b.PatientId == patientId)
            .Select(b => new
            {
                b.MetricId,
                Code = b.Metric!.Code,
                b.Value,
                b.FavorableDirection,
            })
            .ToListAsync(ct);
        if (baselines.Count == 0)
        {
            return [];
        }

        var latestByMetric = await LoadLatestMeasurementPerMetricAsync(
            patientId,
            timezone,
            from,
            to,
            ct
        );
        if (latestByMetric.Count == 0)
        {
            return [];
        }

        return baselines
            .Where(b => latestByMetric.ContainsKey(b.MetricId))
            .Select(b => new ClinicalIndicator(
                b.Code,
                b.Value,
                latestByMetric[b.MetricId],
                b.FavorableDirection
            ))
            .ToList();
    }

    /// <summary>
    /// Indicadores de la semana para el Índice de Transformación (SPEC §13.5):
    /// líneas base con código de métrica y símbolo de unidad + última medición
    /// de la semana por métrica (una query set).
    /// </summary>
    private async Task<IReadOnlyList<TransformationIndicator>> LoadTransformationIndicatorsAsync(
        Guid patientId,
        string timezone,
        DateOnly from,
        DateOnly to,
        CancellationToken ct
    )
    {
        var baselines = await dbContext
            .ClinicalBaselines.AsNoTracking()
            .Where(b => b.PatientId == patientId)
            .Select(b => new
            {
                b.MetricId,
                Code = b.Metric!.Code,
                b.Value,
                Unit = b.Unit!.Symbol,
                b.FavorableDirection,
            })
            .ToListAsync(ct);
        if (baselines.Count == 0)
        {
            return [];
        }

        var latestByMetric = await LoadLatestMeasurementPerMetricAsync(
            patientId,
            timezone,
            from,
            to,
            ct
        );

        return baselines
            .Where(b => latestByMetric.ContainsKey(b.MetricId))
            .Select(b => new TransformationIndicator(
                b.Code,
                b.Value,
                latestByMetric[b.MetricId],
                b.Unit,
                b.FavorableDirection
            ))
            .ToList();
    }

    /// <summary>
    /// Última medición por métrica en la ventana (fechas locales → instantes
    /// UTC por la zona del paciente). Una sola query agrupada (sin N+1).
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> LoadLatestMeasurementPerMetricAsync(
        Guid patientId,
        string timezone,
        DateOnly from,
        DateOnly to,
        CancellationToken ct
    )
    {
        var tz = ResolveTimeZone(timezone);
        var fromUtc = LocalDateToUtcStart(from, tz);
        var toExclusiveUtc = LocalDateToUtcStart(to.AddDays(1), tz);

        var latest = await dbContext
            .ClinicalMeasurements.AsNoTracking()
            .Where(m =>
                m.PatientId == patientId && m.ObservedAt >= fromUtc && m.ObservedAt < toExclusiveUtc
            )
            .GroupBy(m => m.MetricId)
            .Select(g =>
                g.OrderByDescending(m => m.ObservedAt)
                    .Select(m => new { m.MetricId, m.Value })
                    .FirstOrDefault()
            )
            .ToListAsync(ct);

        return latest.Where(x => x is not null).ToDictionary(x => x!.MetricId, x => x!.Value);
    }

    /// <summary>
    /// Pesos del Índice de Salud; si la tabla está vacía (seed no corrido), se
    /// usan los defaults de la SPEC §13.1.1 (0.30/0.30/0.20/0.10/0.10) para que
    /// el ponderado nunca quede en cero por configuración.
    /// </summary>
    private async Task<IReadOnlyList<ScoreWeight>> LoadWeightsOrDefaultAsync(CancellationToken ct)
    {
        var weights = await dbContext
            .HealthScoreWeights.AsNoTracking()
            .Select(w => new ScoreWeight(w.Dimension, w.Weight))
            .ToListAsync(ct);
        return weights.Count == 0 ? DefaultWeights : weights;
    }

    private static readonly IReadOnlyList<ScoreWeight> DefaultWeights =
    [
        new(ScoreDimension.adherence, 0.30m),
        new(ScoreDimension.clinical, 0.30m),
        new(ScoreDimension.nutrition, 0.20m),
        new(ScoreDimension.psychology, 0.10m),
        new(ScoreDimension.exercise, 0.10m),
    ];

    private static readonly HashSet<string> ClinicianRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Physician",
        "Nutritionist",
        "Psychologist",
        "ClinicalDirector",
        "Admin",
    };

    /// <summary>
    /// Semana vigente de la inscripción (SPEC §13.2): la semana
    /// <c>program_enrollments.current_week_number</c>, o la más reciente
    /// <c>Completed</c> cuando la actual ya pasó (el avance crea la semana
    /// siguiente al completar la anterior). NUNCA la semana de mayor número:
    /// las semanas futuras nacen <c>Locked</c> y su ventana aún no tiene datos.
    /// </summary>
    private async Task<ProgramWeek?> ResolveLatestWeekAsync(Guid enrollmentId, CancellationToken ct)
    {
        var currentWeekNumber = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId)
            .Select(e => (int?)e.CurrentWeekNumber)
            .FirstOrDefaultAsync(ct);
        if (currentWeekNumber is null)
        {
            return null;
        }

        return await dbContext
            .ProgramWeeks.AsNoTracking()
            .Where(w => w.EnrollmentId == enrollmentId && w.WeekNumber <= currentWeekNumber)
            .OrderByDescending(w => w.WeekNumber)
            .FirstOrDefaultAsync(ct);
    }

    private static HealthScoreDto ToHealthScoreDto(HealthScore h) =>
        new(
            h.Score,
            h.ScorePrevious,
            h.Trend.ToString(),
            new HealthScoreDimensionsDto(
                h.ScoreAdherence,
                h.ScoreClinical,
                h.ScoreNutrition,
                h.ScorePsychology,
                h.ScoreExercise
            )
        );

    private static TransformationScoreDto ToTransformationScoreDto(TransformationScore t)
    {
        var detail =
            t.Detail.ValueKind == JsonValueKind.Object
                ? JsonSerializer.Deserialize<Dictionary<string, IndicatorDetailDto>>(
                    t.Detail.GetRawText()
                ) ?? new Dictionary<string, IndicatorDetailDto>()
                : new Dictionary<string, IndicatorDetailDto>();

        return new TransformationScoreDto(
            t.Score,
            t.ScorePrevious,
            t.OverallTrend.ToString(),
            t.WeekNumber,
            detail
        );
    }

    private static IndicatorDetailDto ToIndicatorDetailDto(TransformationIndicatorScore s) =>
        new(s.Baseline, s.Current, s.Unit, s.Delta, s.DeltaPct, s.Favorable, s.Score);

    private static ScoreTrend DeriveTrend(int current, int? previous) =>
        previous is null || current == previous ? ScoreTrend.stable
        : current > previous ? ScoreTrend.up
        : ScoreTrend.down;

    private static TimeZoneInfo ResolveTimeZone(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// Convierte una fecha local del paciente al instante UTC de su inicio
    /// (medianoche local), respetando DST de la zona IANA.
    /// </summary>
    private static DateTime LocalDateToUtcStart(DateOnly date, TimeZoneInfo tz)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    private sealed record EnrollmentWindow(Guid EnrollmentId, Guid PatientId, string Timezone);

    private sealed record NutritionCountRow(int Achieved, int Total);

    private async Task EnsureEnrollmentExistsAsync(Guid enrollmentId, CancellationToken ct)
    {
        var exists = await dbContext
            .ProgramEnrollments.AsNoTracking()
            .AnyAsync(e => e.Id == enrollmentId, ct);
        if (!exists)
        {
            throw new NotFoundException($"Inscripción {enrollmentId} no encontrada.");
        }
    }

    /// <inheritdoc/>
    public async Task<EnrollmentWeekDetailDto?> GetEnrollmentWeekDetailAsync(
        Guid enrollmentId,
        int weekNumber,
        Guid clinicianUserId,
        CancellationToken ct
    )
    {
        // 1. Cargar inscripción (404 si no existe).
        var enrollment = await GetEnrollmentAsync(enrollmentId, ct);
        if (enrollment is null)
        {
            return null;
        }

        // 2. Scoping clínico: DESHABILITADO temporalmente para testing.
        //    Restaurar cuando T-81 (patient_professionals scoping) esté completo.
        //    Patrón actual del módulo: los endpoints de clínico NO enforcean scoping.

        // 3. Cargar la semana del programa (404 si no existe).
        var week = await dbContext
            .ProgramWeeks.AsNoTracking()
            .FirstOrDefaultAsync(
                w => w.EnrollmentId == enrollmentId && w.WeekNumber == weekNumber,
                ct
            );

        if (week is null)
        {
            return null;
        }

        // 4. Parsear tasks_snapshot jsonb → agrupar por weekday.
        //    Fallback: si el snapshot está vacío (inscripciones viejas creadas
        //    antes de que la plantilla tuviera DayTemplates), cargar las tareas
        //    actuales de la plantilla directamente.
        var snapshotTasks = ParseSnapshot(week.TasksSnapshot);
        if (snapshotTasks.Count == 0)
        {
            var templateDays = await dbContext
                .WeeklyDayTemplates.AsNoTracking()
                .Where(d => d.TemplateId == enrollment.TemplateId)
                .OrderBy(d => d.Weekday)
                .ThenBy(d => d.SortOrder)
                .ToListAsync(ct);
            snapshotTasks = templateDays
                .Select(d => new SnapshotTask(
                    d.Weekday,
                    d.TaskCode.ToString(),
                    d.Points,
                    d.SortOrder
                ))
                .ToList();
        }
        var tasksByWeekday = snapshotTasks
            .GroupBy(t => t.Weekday)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.SortOrder).ToList());

        // 5. Query task_completions para la ventana [weekStart, weekEnd].
        var completions = await dbContext
            .TaskCompletions.AsNoTracking()
            .Where(tc =>
                tc.EnrollmentId == enrollmentId
                && tc.LocalDate >= week.WeekStartDateLocal
                && tc.LocalDate <= week.WeekEndDateLocal
            )
            .Select(tc => new
            {
                tc.LocalDate,
                tc.TaskCode,
                tc.PointsAwarded,
                tc.CompletedAt,
            })
            .ToListAsync(ct);

        var completionsLookup = completions.ToDictionary(
            c => (c.LocalDate, TaskCode: c.TaskCode.ToString()),
            c => new { c.PointsAwarded, c.CompletedAt }
        );

        // 6. Query daily_checkins para la ventana.
        var checkins = await dbContext
            .DailyCheckIns.AsNoTracking()
            .Where(ci =>
                ci.EnrollmentId == enrollmentId
                && ci.LocalDate >= week.WeekStartDateLocal
                && ci.LocalDate <= week.WeekEndDateLocal
            )
            .Select(ci => new
            {
                ci.LocalDate,
                ci.TotalPoints,
                ci.BonusAwarded,
                ci.IsPerfectDay,
            })
            .ToListAsync(ct);

        var checkinsLookup = checkins.ToDictionary(ci => ci.LocalDate, ci => ci);

        // 7. Resolver contenido activo (plan/rutina) al inicio de la semana.
        EnrollmentWeekContentRef? planRef = null;
        EnrollmentWeekContentRef? routineRef = null;
        string? routineDetailText = null;
        string? planDetailText = null;

        if (programContentResolver is not null)
        {
            var contentResolution = await programContentResolver.ResolveAsync(
                enrollment.PatientId,
                week.WeekStartDateLocal,
                ct
            );

            if (contentResolution?.NutritionPlanId is { } planId)
            {
                var plan = await dbContext
                    .NutritionPlans.AsNoTracking()
                    .Where(p => p.Id == planId)
                    .Select(p => new { p.Name, p.Description })
                    .FirstOrDefaultAsync(ct);
                if (plan is not null)
                {
                    planRef = new EnrollmentWeekContentRef(planId, plan.Name);
                    planDetailText = plan.Description;
                }
            }

            if (contentResolution?.ExerciseRoutineId is { } routineId)
            {
                var routine = await dbContext
                    .ExerciseRoutines.AsNoTracking()
                    .Where(r => r.Id == routineId)
                    .Select(r => new
                    {
                        r.Name,
                        r.TargetMuscles,
                        r.Category,
                    })
                    .FirstOrDefaultAsync(ct);
                if (routine is not null)
                {
                    routineRef = new EnrollmentWeekContentRef(routineId, routine.Name);
                    routineDetailText = !string.IsNullOrWhiteSpace(routine.TargetMuscles)
                        ? routine.TargetMuscles
                        : routine.Category.ToString();
                }
            }
        }

        // 8. Construir días: iterar las 7 fechas de la semana.
        var days = new List<EnrollmentWeekDayDto>(7);
        for (
            var date = week.WeekStartDateLocal;
            date <= week.WeekEndDateLocal;
            date = date.AddDays(1)
        )
        {
            var weekday = ToIsoWeekday(date);
            var dayLabel = EnrollmentWeekTaskLabels.DayLabel(weekday);

            var scheduled = tasksByWeekday.TryGetValue(weekday, out var tasks) ? tasks : [];

            var maxPoints = scheduled.Sum(t => t.Points);

            var dayTasks = new List<EnrollmentWeekTaskDto>(scheduled.Count);
            foreach (var task in scheduled)
            {
                var taskCode = task.TaskCode;
                var taskLabel = EnrollmentWeekTaskLabels.TaskLabel(taskCode);

                var completed = completionsLookup.TryGetValue((date, taskCode), out var completion);

                Guid? contentRefId = null;
                string? contentName = null;
                string? detailText = null;

                if (string.Equals(taskCode, "ejercicio", StringComparison.OrdinalIgnoreCase))
                {
                    var activeRoutineId = task.RoutineId ?? routineRef?.Id;
                    if (activeRoutineId is { } rId)
                    {
                        contentRefId = rId;
                        if (task.RoutineId is not null)
                        {
                            var r = await dbContext
                                .ExerciseRoutines.AsNoTracking()
                                .Where(x => x.Id == rId)
                                .Select(x => new
                                {
                                    x.Name,
                                    x.TargetMuscles,
                                    x.Category,
                                })
                                .FirstOrDefaultAsync(ct);
                            if (r is not null)
                            {
                                contentName = r.Name;
                                detailText = !string.IsNullOrWhiteSpace(r.TargetMuscles)
                                    ? r.TargetMuscles
                                    : r.Category.ToString();
                            }
                        }
                        else if (routineRef is not null)
                        {
                            contentName = routineRef.Name;
                            detailText = routineDetailText;
                        }
                    }
                }
                else if (string.Equals(taskCode, "nut", StringComparison.OrdinalIgnoreCase))
                {
                    var activePlanId = task.NutritionPlanId ?? planRef?.Id;
                    if (activePlanId is { } pId)
                    {
                        contentRefId = pId;
                        if (task.NutritionPlanId is not null)
                        {
                            var p = await dbContext
                                .NutritionPlans.AsNoTracking()
                                .Where(x => x.Id == pId)
                                .Select(x => new { x.Name, x.Description })
                                .FirstOrDefaultAsync(ct);
                            if (p is not null)
                            {
                                contentName = p.Name;
                                detailText = p.Description;
                            }
                        }
                        else if (planRef is not null)
                        {
                            contentName = planRef.Name;
                            detailText = planDetailText;
                        }
                    }
                }

                dayTasks.Add(
                    new EnrollmentWeekTaskDto(
                        TaskCode: taskCode,
                        TaskLabel: taskLabel,
                        Points: completed && completion is not null ? completion.PointsAwarded : 0,
                        ScheduledPoints: task.Points,
                        Status: completed ? "completed" : "pending",
                        CompletedAt: completed && completion is not null
                            ? completion.CompletedAt
                            : null,
                        ContentRefId: contentRefId,
                        ContentName: contentName,
                        DetailText: detailText
                    )
                );
            }

            var hasCheckin = checkinsLookup.TryGetValue(date, out var checkin);
            var totalPoints = hasCheckin && checkin is not null ? checkin.TotalPoints : 0;
            var bonusAwarded = hasCheckin && checkin is not null ? checkin.BonusAwarded : 0;
            var isPerfectDay = hasCheckin && checkin is not null && checkin.IsPerfectDay;

            days.Add(
                new EnrollmentWeekDayDto(
                    LocalDate: date,
                    Weekday: weekday,
                    DayLabel: dayLabel,
                    IsPerfectDay: isPerfectDay,
                    TotalPoints: totalPoints,
                    MaxPoints: maxPoints,
                    BonusAwarded: bonusAwarded,
                    Tasks: dayTasks
                )
            );
        }

        return new EnrollmentWeekDetailDto(
            WeekNumber: week.WeekNumber,
            WeekStartDateLocal: week.WeekStartDateLocal,
            WeekEndDateLocal: week.WeekEndDateLocal,
            NutritionPlan: planRef,
            ExerciseRoutine: routineRef,
            Days: days
        );
    }

    /// <summary>Lunes = 1 … Domingo = 7 (mismo índice que NutritionPlanDay).</summary>
    private static short ToIsoWeekday(DateOnly date) => (short)(((int)date.DayOfWeek + 6) % 7 + 1);

    /// <summary>
    /// Fecha local de hoy del paciente desde su zona IANA. Fallback a UTC si la
    /// zona no está disponible en el SO (Windows mapea las IANA comunes).
    /// </summary>
    private static DateOnly PatientLocalToday(string timezone)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow.Date);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private sealed record SnapshotTask(
        short Weekday,
        string TaskCode,
        int Points,
        int SortOrder,
        Guid? RoutineId = null,
        Guid? NutritionPlanId = null
    );
}
