using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.ActivityLog;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Interventions;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Weaknesses;
using CoppAddresd.Application.Features.ProgramProgress.Queries.ExportEnrollments;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReconcileStreaks;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Fake en memoria de <see cref="IProgramRepository"/> para los tests de
/// handlers (patrón fakes-in-memory de <c>tests/CoppAddresd.Telemedicine.UnitTests</c>).
/// Cada método tiene un comportamiento por defecto determinista y un hook
/// <c>On*</c> opcional para que cada test controle el escenario sin tocar BD.
/// </summary>
internal sealed class FakeProgramRepository : IProgramRepository
{
    // ------------------------------------------------------------ Almacenamiento

    public Dictionary<Guid, ProgramEnrollment> Enrollments { get; } = [];

    public Dictionary<Guid, ProgramTemplate> Templates { get; } = [];

    public Dictionary<Guid, AdaptationRecommendation> Adaptations { get; } = [];

    public List<CompleteTaskInput> CompletedTaskInputs { get; } = [];

    /// <summary>Resultados devueltos por <c>CompleteTaskAsync</c> (para aserciones no tautológicas).</summary>
    public List<CompleteTaskResult> CompletedTaskResults { get; } = [];

    public List<(string Action, string SchemaName, string TableName, Guid RecordId, Guid? ActorId)> AuditRows { get; } = [];

    /// <summary>"Hoy" local del paciente que devuelve <c>GetPatientLocalTodayAsync</c> (null = inscripción inexistente).</summary>
    public DateOnly? PatientToday { get; set; }

    // ------------------------------------------------------------ Hooks

    public Func<CompleteTaskInput, CancellationToken, Task<CompleteTaskResult>>? OnCompleteTask { get; set; }

    public Func<Guid, CancellationToken, Task<ProgramSnapshotDto?>>? OnGetSnapshot { get; set; }

    public Func<Guid, CancellationToken, Task<ProgramCalendarDto>>? OnGetCalendar { get; set; }

    public Func<Guid, CancellationToken, Task<ProgramPathDto>>? OnGetPath { get; set; }

    public Func<Guid, CancellationToken, Task<ProgramEnrollmentDto?>>? OnGetEnrollment { get; set; }

    // ------------------------------------------------------------ Inscripciones

    public Task<ProgramEnrollment> EnrollAsync(
        Guid patientId, Guid templateId, string timezone, DateOnly startLocalDate,
        Guid? createdBy = null, CancellationToken ct = default)
    {
        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            TemplateId = templateId,
            Timezone = timezone,
            StartLocalDate = startLocalDate,
            Status = ProgramEnrollmentStatus.Active,
            CurrentWeekNumber = 1,
            CreatedBy = createdBy,
            CreatedAt = DateTime.UtcNow,
        };
        Enrollments[enrollment.Id] = enrollment;
        return Task.FromResult(enrollment);
    }

    public Task<ProgramEnrollment> PauseAsync(Guid enrollmentId, Guid? actorId = null, CancellationToken ct = default)
        => TransitionAsync(enrollmentId, ProgramEnrollmentStatus.Paused, e => e.PausedAt = DateTime.UtcNow);

    public Task<ProgramEnrollment> ResumeAsync(Guid enrollmentId, Guid? actorId = null, CancellationToken ct = default)
        => TransitionAsync(enrollmentId, ProgramEnrollmentStatus.Active, e => e.PausedAt = null);

    public Task<ProgramEnrollment> WithdrawAsync(Guid enrollmentId, Guid? actorId = null, CancellationToken ct = default)
        => TransitionAsync(enrollmentId, ProgramEnrollmentStatus.Withdrawn, e => e.WithdrawnAt = DateTime.UtcNow);

    private Task<ProgramEnrollment> TransitionAsync(
        Guid enrollmentId, ProgramEnrollmentStatus status, Action<ProgramEnrollment> apply)
    {
        if (!Enrollments.TryGetValue(enrollmentId, out var enrollment))
        {
            throw new CoppAddresd.Domain.Exceptions.NotFoundException($"Inscripción {enrollmentId} no encontrada.");
        }

        enrollment.Status = status;
        apply(enrollment);
        return Task.FromResult(enrollment);
    }

    public Task<ProgramEnrollmentDto?> GetEnrollmentAsync(Guid enrollmentId, CancellationToken ct = default)
    {
        if (OnGetEnrollment is not null)
        {
            return OnGetEnrollment(enrollmentId, ct);
        }

        if (!Enrollments.TryGetValue(enrollmentId, out var e))
        {
            return Task.FromResult<ProgramEnrollmentDto?>(null);
        }

        return Task.FromResult<ProgramEnrollmentDto?>(new ProgramEnrollmentDto(
            e.Id, e.PatientId, e.TemplateId, e.Timezone, e.Status,
            e.StartedAt, e.StartLocalDate, e.CurrentWeekNumber, 83, 0, 0, 0, 0,
            e.CompletedAt, e.PausedAt, e.WithdrawnAt, e.CreatedAt));
    }

    public Task<(IReadOnlyList<ProgramEnrollmentDto> Items, int Total)> ListEnrollmentsAsync(
        Guid? patientId, ProgramEnrollmentStatus? status, int page, int pageSize,
        IReadOnlyList<Guid>? scopedPatientIds = null, CancellationToken ct = default)
    {
        var query = Enrollments.Values.AsEnumerable();
        if (patientId.HasValue)
        {
            query = query.Where(e => e.PatientId == patientId.Value);
        }

        if (scopedPatientIds is { Count: > 0 })
        {
            query = query.Where(e => scopedPatientIds.Contains(e.PatientId));
        }

        if (status.HasValue)
        {
            query = query.Where(e => e.Status == status.Value);
        }

        var items = query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .Select(e => new ProgramEnrollmentDto(
                e.Id, e.PatientId, e.TemplateId, e.Timezone, e.Status,
                e.StartedAt, e.StartLocalDate, e.CurrentWeekNumber, 83, 0, 0, 0, 0,
                e.CompletedAt, e.PausedAt, e.WithdrawnAt, e.CreatedAt))
            .ToList();

        return Task.FromResult<(IReadOnlyList<ProgramEnrollmentDto> Items, int Total)>((items, query.Count()));
    }

    // ------------------------------------------------------------ Completación

    public async Task<CompleteTaskResult> CompleteTaskAsync(CompleteTaskInput input, CancellationToken ct = default)
    {
        CompletedTaskInputs.Add(input);
        var result = OnCompleteTask is not null
            ? await OnCompleteTask(input, ct)
            : new CompleteTaskResult(
                CompleteTaskOutcome.Created, Guid.NewGuid(), 80, 80, false, 0, 0, 0, 80, 750);
        CompletedTaskResults.Add(result);
        return result;
    }

    // ------------------------------------------------------------ Lecturas del paciente

    public Task<DateOnly?> GetPatientLocalTodayAsync(Guid enrollmentId, CancellationToken ct = default)
        => Task.FromResult(PatientToday);

    public Task<ProgramSnapshotDto?> GetSnapshotAsync(Guid enrollmentId, DateOnly todayLocalDate, CancellationToken ct = default)
        => OnGetSnapshot is not null
            ? OnGetSnapshot(enrollmentId, ct)
            : Task.FromResult<ProgramSnapshotDto?>(null);

    public Task<ProgramCalendarDto> GetCalendarAsync(Guid enrollmentId, DateOnly from, DateOnly to, CancellationToken ct = default)
        => OnGetCalendar is not null
            ? OnGetCalendar(enrollmentId, ct)
            : Task.FromResult(new ProgramCalendarDto(from, to, [], new CalendarSummaryDto(0, 0, 0)));

    public Task<ProgramPathDto> GetPathAsync(Guid enrollmentId, CancellationToken ct = default)
        => OnGetPath is not null
            ? OnGetPath(enrollmentId, ct)
            : Task.FromResult(new ProgramPathDto([]));

    // ------------------------------------------------------------ Plantillas

    public Task<(IReadOnlyList<ProgramTemplate> Items, int Total)> ListTemplatesAsync(
        string? search, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Templates.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(t => t.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || t.Code.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<TemplateStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(t => t.Status == parsed);
        }

        var items = query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToList();

        return Task.FromResult<(IReadOnlyList<ProgramTemplate>, int)>((items, query.Count()));
    }

    public Task<ProgramTemplate?> GetTemplateAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Templates.TryGetValue(id, out var template) ? template : null);

    public Task<ProgramTemplate?> GetTemplateByCodeAsync(string code, CancellationToken ct = default)
        => Task.FromResult(Templates.Values.FirstOrDefault(t => t.Code == code));

    public Task<ProgramTemplate> UpsertTemplateAsync(
        ProgramTemplate template, IReadOnlyList<WeeklyDayTemplate> dayTemplates,
        Guid? actorId = null, CancellationToken ct = default)
    {
        template.DayTemplates = dayTemplates.ToList();
        Templates[template.Id] = template;
        return Task.FromResult(template);
    }

    public Task<IReadOnlyList<WeeklyDayTemplate>> ReplaceWeekdayTasksAsync(
        Guid templateId, IReadOnlyList<WeeklyDayTemplate> tasks,
        Guid? actorId = null, CancellationToken ct = default)
    {
        if (!Templates.TryGetValue(templateId, out var template))
        {
            throw new CoppAddresd.Domain.Exceptions.NotFoundException($"Plantilla {templateId} no encontrada.");
        }

        template.DayTemplates = tasks.ToList();
        return Task.FromResult(tasks);
    }

    // ------------------------------------------------------------ Adaptaciones

    public Task<(IReadOnlyList<AdaptationRecommendation> Items, int Total)> ListAdaptationsAsync(
        Guid? enrollmentId, AdaptationStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Adaptations.Values.AsEnumerable();
        if (enrollmentId.HasValue)
        {
            query = query.Where(a => a.EnrollmentId == enrollmentId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(a => a.Status == status.Value);
        }

        var items = query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToList();

        return Task.FromResult<(IReadOnlyList<AdaptationRecommendation>, int)>((items, query.Count()));
    }

    public Task<AdaptationRecommendation?> GetAdaptationAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Adaptations.TryGetValue(id, out var adaptation) ? adaptation : null);

    public Task<AdaptationRecommendation> DecideAdaptationAsync(
        Guid adaptationId, AdaptationDecisionAction action, Guid? actorId = null,
        string? auditActionOnApply = null, CancellationToken ct = default)
    {
        if (!Adaptations.TryGetValue(adaptationId, out var adaptation))
        {
            throw new CoppAddresd.Domain.Exceptions.NotFoundException(
                $"Recomendación de adaptación {adaptationId} no encontrada.");
        }

        var now = DateTime.UtcNow;
        switch (action)
        {
            case AdaptationDecisionAction.Approve when adaptation.Status == AdaptationStatus.Pending:
                adaptation.Status = AdaptationStatus.Approved;
                adaptation.DecidedBy = actorId;
                adaptation.DecidedAt = now;
                break;
            case AdaptationDecisionAction.Reject when adaptation.Status == AdaptationStatus.Pending:
                adaptation.Status = AdaptationStatus.Rejected;
                adaptation.DecidedBy = actorId;
                adaptation.DecidedAt = now;
                break;
            case AdaptationDecisionAction.Apply when adaptation.Status == AdaptationStatus.Approved:
                adaptation.Status = AdaptationStatus.Applied;
                adaptation.AppliedAt = now;
                break;
            default:
                throw new CoppAddresd.Domain.Exceptions.BusinessRuleViolationException(
                    "ADAPTATION_STATE: transición no permitida.");
        }

        adaptation.UpdatedAt = now;

        // Espejo del repositorio real: la fila semántica se registra junto con
        // la transición a Applied (AC-17), no por el handler post-commit.
        if (action == AdaptationDecisionAction.Apply
            && adaptation.Status == AdaptationStatus.Applied
            && !string.IsNullOrWhiteSpace(auditActionOnApply))
        {
            AuditRows.Add((auditActionOnApply, "app", "adaptation_recommendations", adaptation.Id, actorId));
        }

        return Task.FromResult(adaptation);
    }

    public Task<int> SupersedePendingAsync(
        Guid enrollmentId, AdaptationKind kind, Guid targetEntityId,
        Guid newRecommendationId, CancellationToken ct = default)
    {
        var superseded = Adaptations.Values
            .Where(a => a.EnrollmentId == enrollmentId
                && a.Kind == kind
                && a.TargetEntityId == targetEntityId
                && a.Status == AdaptationStatus.Pending
                && a.Id != newRecommendationId)
            .ToList();

        foreach (var adaptation in superseded)
        {
            adaptation.Status = AdaptationStatus.Superseded;
        }

        return Task.FromResult(superseded.Count);
    }

    public Task WriteAuditRowAsync(
        string action, string schemaName, string tableName, Guid recordId,
        Guid? actorId = null, CancellationToken ct = default)
    {
        AuditRows.Add((action, schemaName, tableName, recordId, actorId));
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------ Motor de puntajes (SPEC §13)

    /// <summary>Resultados configurados para <c>GetOrComputeHealthScoreAsync</c> (null = sin inscripción).</summary>
    public HealthScoreDto? HealthScore { get; set; }

    /// <summary>Resultados configurados para <c>GetOrComputeTransformationScoreAsync</c> (null = sin inscripción).</summary>
    public TransformationScoreDto? TransformationScore { get; set; }

    public Task<HealthScoreDto?> GetOrComputeHealthScoreAsync(
        Guid patientId, ScoreTrigger trigger, bool force = false,
        DateOnly? periodEndLocalDate = null, CancellationToken ct = default)
        => Task.FromResult(HealthScore);

    public Task<TransformationScoreDto?> GetOrComputeTransformationScoreAsync(
        Guid patientId, ScoreTrigger trigger, bool force = false, CancellationToken ct = default)
        => Task.FromResult(TransformationScore);

    public Task<IReadOnlyList<ClinicalBaselineDto>> ListClinicalBaselinesAsync(
        Guid patientId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ClinicalBaselineDto>>([]);

    public Task<ClinicalBaselineDto> UpsertClinicalBaselineAsync(
        ClinicalBaselineWrite input, IReadOnlyList<string> callerRoles, CancellationToken ct = default)
        => throw new NotSupportedException(
            "FakeProgramRepository no persiste líneas base: usa los tests de integración.");

    public Task<decimal?> GetLatestMeasurementAsync(
        Guid patientId, Guid metricId, DateOnly fromDate, DateOnly toDate, CancellationToken ct = default)
        => Task.FromResult<decimal?>(null);

    // ------------------------------------------------------------ XP clínica (SPEC §15)

    /// <summary>Resultado configurado para <c>EvaluateClinicalXpAwardsAsync</c>.</summary>
    public ClinicalXpEvaluationResult ClinicalXpEvaluation { get; set; } = ClinicalXpEvaluationResult.Empty;

    /// <summary>Revisiones clínicas configuradas para <c>ListPendingClinicalReviewsAsync</c>.</summary>
    public List<ClinicalReviewDto> PendingClinicalReviews { get; set; } = [];

    public Func<Guid, bool, Guid?, IReadOnlyList<string>, CancellationToken, Task<ClinicalReviewDto>>? OnDecideClinicalReview { get; set; }

    public Task<ClinicalXpEvaluationResult> EvaluateClinicalXpAwardsAsync(
        Guid patientId, DateOnly? periodEndLocalDate = null, CancellationToken ct = default)
        => Task.FromResult(ClinicalXpEvaluation);

    public Task<(IReadOnlyList<ClinicalReviewDto> Items, int Total)> ListPendingClinicalReviewsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var items = PendingClinicalReviews
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToList();
        return Task.FromResult<(IReadOnlyList<ClinicalReviewDto>, int)>((items, PendingClinicalReviews.Count));
    }

    public Task<ClinicalReviewDto> DecideClinicalXpReviewAsync(
        Guid reviewId, bool approve, Guid? actorId,
        IReadOnlyList<string> callerRoles, CancellationToken ct = default)
        => OnDecideClinicalReview is not null
            ? OnDecideClinicalReview(reviewId, approve, actorId, callerRoles, ct)
            : throw new NotSupportedException(
                "FakeProgramRepository no decide revisiones: configura OnDecideClinicalReview.");

    // ------------------------------------------------------------ Nutrición granular (SPEC §18)

    /// <summary>Resultado configurado para <c>LogNutritionAsync</c>.</summary>
    public NutritionLogResultDto? NutritionLogResult { get; set; }

    /// <summary>Resultado configurado para <c>EvaluateNutritionAwardsAsync</c>.</summary>
    public NutritionWeeklyAwardsResult NutritionWeeklyAwards { get; set; } = NutritionWeeklyAwardsResult.Empty;

    public Task<NutritionLogResultDto> LogNutritionAsync(
        Guid patientId, MealCode mealCode, DateOnly? localDate = null, CancellationToken ct = default)
        => NutritionLogResult is not null
            ? Task.FromResult(NutritionLogResult)
            : Task.FromResult(new NutritionLogResultDto(
                Guid.NewGuid(), mealCode.ToString(), localDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                true, 0, 0));

    public Task<NutritionWeeklyAwardsResult> EvaluateNutritionAwardsAsync(
        Guid patientId, DateOnly? periodEndLocalDate = null, CancellationToken ct = default)
        => Task.FromResult(NutritionWeeklyAwards);

    // ------------------------------------------------------------ Debilidades (SPEC §21, "Paso 7c")

    /// <summary>Paquete semanal configurado para <c>BuildPatientWeeklyDataAsync</c>.</summary>
    public PatientWeeklyData? PatientWeeklyData { get; set; }

    /// <summary>Debilidades configuradas para <c>ListWeaknessesAsync</c> / <c>ListOpenWeaknessesAsync</c>.</summary>
    public List<WeaknessDto> Weaknesses { get; set; } = [];

    /// <summary>Debilidades devueltas por <c>PersistDetectedWeaknessesAsync</c>.</summary>
    public List<Weakness> PersistedWeaknesses { get; set; } = [];

    /// <summary>Inputs registrados por <c>PersistDetectedWeaknessesAsync</c>.</summary>
    public List<(Guid PatientId, IReadOnlyList<WeaknessDescriptor> Descriptors)> PersistedWeaknessInputs { get; } = [];

    /// <summary>Hook opcional para <c>UpdateWeaknessStatusAsync</c>.</summary>
    public Func<Guid, WeaknessStatus, Guid?, IReadOnlyList<string>, CancellationToken, Task<WeaknessDto>>? OnUpdateWeaknessStatus { get; set; }

    public Task<PatientWeeklyData?> BuildPatientWeeklyDataAsync(
        Guid patientId, DateOnly? periodEndLocalDate = null, CancellationToken ct = default)
        => Task.FromResult(PatientWeeklyData);

    public Task<IReadOnlyList<Weakness>> PersistDetectedWeaknessesAsync(
        Guid patientId, IReadOnlyList<WeaknessDescriptor> descriptors, CancellationToken ct = default)
    {
        PersistedWeaknessInputs.Add((patientId, descriptors));
        if (PersistedWeaknesses.Count > 0)
        {
            return Task.FromResult<IReadOnlyList<Weakness>>(PersistedWeaknesses);
        }

        var list = descriptors.Select(d => new Weakness
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Code = d.Code,
            Category = d.Category,
            Severity = d.Severity,
            Title = d.Title,
            Description = d.Description,
            DetectedAt = DateTime.UtcNow,
            MetricId = d.MetricId,
            IndicatorValue = d.IndicatorValue,
            Source = WeaknessSource.ai,
            Status = WeaknessStatus.open,
            CreatedAt = DateTime.UtcNow,
        }).ToList();

        return Task.FromResult<IReadOnlyList<Weakness>>(list);
    }

    public Task<(IReadOnlyList<WeaknessDto> Items, int Total)> ListWeaknessesAsync(
        Guid patientId, int page, int pageSize, CancellationToken ct = default)
    {
        var items = Weaknesses
            .Where(w => w.PatientId == patientId || patientId == Guid.Empty)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToList();
        return Task.FromResult<(IReadOnlyList<WeaknessDto>, int)>((items, Weaknesses.Count));
    }

    public Task<(IReadOnlyList<WeaknessDto> Items, int Total)> ListOpenWeaknessesAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var items = Weaknesses
            .Where(w => string.Equals(w.Status, "open", StringComparison.OrdinalIgnoreCase))
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToList();
        return Task.FromResult<(IReadOnlyList<WeaknessDto>, int)>((items, Weaknesses.Count(w => string.Equals(w.Status, "open", StringComparison.OrdinalIgnoreCase))));
    }

    public Task<WeaknessDto> UpdateWeaknessStatusAsync(
        Guid weaknessId, WeaknessStatus status, Guid? actorId,
        IReadOnlyList<string> callerRoles, CancellationToken ct = default)
    {
        if (OnUpdateWeaknessStatus is not null)
        {
            return OnUpdateWeaknessStatus(weaknessId, status, actorId, callerRoles, ct);
        }

        var existing = Weaknesses.FirstOrDefault(w => w.Id == weaknessId);
        if (existing is not null)
        {
            var updated = existing with { Status = status.ToString() };
            return Task.FromResult(updated);
        }

        return Task.FromResult(new WeaknessDto(
            weaknessId, Guid.NewGuid(), "WK_SAMPLE", "nutricion", "low",
            "Muestra", null, DateTime.UtcNow, null, null, "ai",
            status.ToString(), null, null, DateTime.UtcNow, null));
    }

    // ------------------------------------------------------------ Intervenciones (SPEC §22, "Paso 7d")

    /// <summary>Intervenciones en memoria para el repositorio fake.</summary>
    public Dictionary<Guid, Intervention> Interventions { get; } = [];

    /// <summary>Intervenciones configuradas para <c>ListInterventionsAsync</c> / <c>ListOpenInterventionsAsync</c>.</summary>
    public List<InterventionDto> InterventionDtos { get; set; } = [];

    /// <summary>Intervención devuelta por defecto en mutaciones si no hay hook.</summary>
    public InterventionDto? SingleInterventionResult { get; set; }

    /// <summary>Inputs registrados por <c>EnsureInterventionFromWeaknessAsync</c>.</summary>
    public List<(Guid PatientId, Guid WeaknessId, InterventionType Type, string Title, string? Description, Guid? ActorId)> EnsureInterventionInputs { get; } = [];

    /// <summary>Inputs registrados por <c>AcceptInterventionAsync</c>.</summary>
    public List<(Guid InterventionId, Guid PatientId)> AcceptInterventionInputs { get; } = [];

    /// <summary>Inputs registrados por <c>UpdateInterventionStatusAsync</c>.</summary>
    public List<(Guid InterventionId, InterventionStatus Status, Guid? ActorId, IReadOnlyList<string> CallerRoles, string? Result, Guid? AssignedTo)> UpdateInterventionStatusInputs { get; } = [];

    /// <summary>Inputs registrados por <c>MarkTeleScheduledAsync</c>.</summary>
    public List<Guid> MarkTeleScheduledInputs { get; } = [];

    /// <summary>Inputs registrados por <c>MarkTeleAttendedAsync</c>.</summary>
    public List<(Guid InterventionId, Guid ClinicianId)> MarkTeleAttendedInputs { get; } = [];

    /// <summary>Inputs registrados por <c>MarkTeleComplyAsync</c>.</summary>
    public List<Guid> MarkTeleComplyInputs { get; } = [];

    /// <summary>Hook opcional para <c>EnsureInterventionFromWeaknessAsync</c>.</summary>
    public Func<Guid, Guid, InterventionType, string, string?, Guid?, CancellationToken, Task<Intervention>>? OnEnsureInterventionFromWeakness { get; set; }

    /// <summary>Hook opcional para <c>AcceptInterventionAsync</c>.</summary>
    public Func<Guid, Guid, CancellationToken, Task<InterventionDto>>? OnAcceptIntervention { get; set; }

    /// <summary>Hook opcional para <c>UpdateInterventionStatusAsync</c>.</summary>
    public Func<Guid, InterventionStatus, Guid?, IReadOnlyList<string>, string?, Guid?, CancellationToken, Task<InterventionDto>>? OnUpdateInterventionStatus { get; set; }

    /// <summary>Hook opcional para <c>MarkTeleScheduledAsync</c>.</summary>
    public Func<Guid, CancellationToken, Task<InterventionDto>>? OnMarkTeleScheduled { get; set; }

    /// <summary>Hook opcional para <c>MarkTeleAttendedAsync</c>.</summary>
    public Func<Guid, Guid, CancellationToken, Task<InterventionDto>>? OnMarkTeleAttended { get; set; }

    /// <summary>Hook opcional para <c>MarkTeleComplyAsync</c>.</summary>
    public Func<Guid, CancellationToken, Task<InterventionDto>>? OnMarkTeleComply { get; set; }

    public Task<Intervention> EnsureInterventionFromWeaknessAsync(
        Guid patientId, Guid weaknessId, InterventionType type,
        string title, string? description, Guid? actorId, CancellationToken ct = default)
    {
        EnsureInterventionInputs.Add((patientId, weaknessId, type, title, description, actorId));
        if (OnEnsureInterventionFromWeakness is not null)
        {
            return OnEnsureInterventionFromWeakness(patientId, weaknessId, type, title, description, actorId, ct);
        }

        var existing = Interventions.Values.FirstOrDefault(i => i.WeaknessId == weaknessId);
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        var intervention = new Intervention
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            WeaknessId = weaknessId,
            Type = type,
            Title = title,
            Description = description,
            Status = InterventionStatus.detected,
            Severity = "medium",
            CreatedAt = DateTime.UtcNow,
        };
        Interventions[intervention.Id] = intervention;
        return Task.FromResult(intervention);
    }

    public Task<(IReadOnlyList<InterventionDto> Items, int Total)> ListInterventionsAsync(
        Guid patientId, int page, int pageSize, CancellationToken ct = default)
    {
        var source = InterventionDtos.Count > 0
            ? InterventionDtos.Where(i => i.PatientId == patientId || patientId == Guid.Empty)
            : Interventions.Values.Where(i => i.PatientId == patientId || patientId == Guid.Empty).Select(ToDto);

        var items = source
            .OrderByDescending(i => i.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToList();

        return Task.FromResult<(IReadOnlyList<InterventionDto>, int)>((items, source.Count()));
    }

    public Task<(IReadOnlyList<InterventionDto> Items, int Total)> ListOpenInterventionsAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var source = InterventionDtos.Count > 0
            ? InterventionDtos.Where(i => !string.Equals(i.Status, "completed", StringComparison.OrdinalIgnoreCase))
            : Interventions.Values.Where(i => i.Status != InterventionStatus.completed).Select(ToDto);

        var items = source
            .OrderBy(i => i.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToList();

        return Task.FromResult<(IReadOnlyList<InterventionDto>, int)>((items, source.Count()));
    }

    public Task<InterventionDto> AcceptInterventionAsync(
        Guid interventionId, Guid patientId, CancellationToken ct = default)
    {
        AcceptInterventionInputs.Add((interventionId, patientId));
        if (OnAcceptIntervention is not null)
        {
            return OnAcceptIntervention(interventionId, patientId, ct);
        }

        if (SingleInterventionResult is not null)
        {
            return Task.FromResult(SingleInterventionResult);
        }

        if (Interventions.TryGetValue(interventionId, out var intervention))
        {
            intervention.Status = InterventionStatus.accepted;
            intervention.AcceptedAt = DateTime.UtcNow;
            intervention.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(ToDto(intervention));
        }

        return Task.FromResult(new InterventionDto(
            interventionId, patientId, null, "telehealth_referral",
            "Intervención aceptada", null, "accepted", "medium",
            null, null, DateTime.UtcNow, null, null, null, 15, DateTime.UtcNow, DateTime.UtcNow));
    }

    public Task<InterventionDto> UpdateInterventionStatusAsync(
        Guid interventionId, InterventionStatus status, Guid? actorId,
        IReadOnlyList<string> callerRoles, string? result = null,
        Guid? assignedTo = null, CancellationToken ct = default)
    {
        UpdateInterventionStatusInputs.Add((interventionId, status, actorId, callerRoles, result, assignedTo));
        if (OnUpdateInterventionStatus is not null)
        {
            return OnUpdateInterventionStatus(interventionId, status, actorId, callerRoles, result, assignedTo, ct);
        }

        if (SingleInterventionResult is not null)
        {
            return Task.FromResult(SingleInterventionResult);
        }

        if (Interventions.TryGetValue(interventionId, out var intervention))
        {
            intervention.Status = status;
            intervention.UpdatedAt = DateTime.UtcNow;
            if (status == InterventionStatus.completed)
            {
                intervention.CompletedAt = DateTime.UtcNow;
                intervention.Result = result;
            }
            if (assignedTo.HasValue)
            {
                intervention.AssignedTo = assignedTo;
            }
            return Task.FromResult(ToDto(intervention));
        }

        return Task.FromResult(new InterventionDto(
            interventionId, Guid.NewGuid(), null, "clinical_consult",
            "Intervención actualizada", null, status.ToString(), "medium",
            assignedTo, null, null, status == InterventionStatus.completed ? DateTime.UtcNow : null,
            null, result, 0, DateTime.UtcNow, DateTime.UtcNow));
    }

    public Task<InterventionDto> MarkTeleScheduledAsync(
        Guid interventionId, CancellationToken ct = default)
    {
        MarkTeleScheduledInputs.Add(interventionId);
        if (OnMarkTeleScheduled is not null)
        {
            return OnMarkTeleScheduled(interventionId, ct);
        }

        if (SingleInterventionResult is not null)
        {
            return Task.FromResult(SingleInterventionResult);
        }

        if (Interventions.TryGetValue(interventionId, out var intervention))
        {
            intervention.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(ToDto(intervention));
        }

        return Task.FromResult(new InterventionDto(
            interventionId, Guid.NewGuid(), null, "telehealth_referral",
            "Teleconsulta agendada", null, "detected", "medium",
            null, null, null, null, null, null, 50, DateTime.UtcNow, DateTime.UtcNow));
    }

    public Task<InterventionDto> MarkTeleAttendedAsync(
        Guid interventionId, Guid clinicianId, CancellationToken ct = default)
    {
        MarkTeleAttendedInputs.Add((interventionId, clinicianId));
        if (OnMarkTeleAttended is not null)
        {
            return OnMarkTeleAttended(interventionId, clinicianId, ct);
        }

        if (SingleInterventionResult is not null)
        {
            return Task.FromResult(SingleInterventionResult);
        }

        if (Interventions.TryGetValue(interventionId, out var intervention))
        {
            intervention.Status = InterventionStatus.in_progress;
            intervention.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(ToDto(intervention));
        }

        return Task.FromResult(new InterventionDto(
            interventionId, Guid.NewGuid(), null, "telehealth_referral",
            "Teleconsulta asistida", null, "in_progress", "medium",
            clinicianId, null, null, null, null, null, 100, DateTime.UtcNow, DateTime.UtcNow));
    }

    public Task<InterventionDto> MarkTeleComplyAsync(
        Guid interventionId, CancellationToken ct = default)
    {
        MarkTeleComplyInputs.Add(interventionId);
        if (OnMarkTeleComply is not null)
        {
            return OnMarkTeleComply(interventionId, ct);
        }

        if (SingleInterventionResult is not null)
        {
            return Task.FromResult(SingleInterventionResult);
        }

        if (Interventions.TryGetValue(interventionId, out var intervention))
        {
            intervention.UpdatedAt = DateTime.UtcNow;
            return Task.FromResult(ToDto(intervention));
        }

        return Task.FromResult(new InterventionDto(
            interventionId, Guid.NewGuid(), null, "telehealth_referral",
            "Cumplimiento evaluado", null, "in_progress", "medium",
            null, null, null, null, null, null, 50, DateTime.UtcNow, DateTime.UtcNow));
    }

    private static InterventionDto ToDto(Intervention i) => new(
        i.Id, i.PatientId, i.WeaknessId, i.Type.ToString(), i.Title,
        i.Description, i.Status.ToString(), i.Severity, i.AssignedTo,
        i.RecommendedAt, i.AcceptedAt, i.CompletedAt, i.PatientAction,
        i.Result, i.XpAwardedTotal, i.CreatedAt, i.UpdatedAt);

    // --- T-77: Helpers para el configurador de contenido ---

    public Task<(string Code, string Name)?> GetPlanNameAsync(Guid planId, CancellationToken ct = default)
        => Task.FromResult<(string Code, string Name)?>(("plan-code", "Plan Name"));

    public Task<(string Code, string Name)?> GetRoutineNameAsync(Guid routineId, CancellationToken ct = default)
        => Task.FromResult<(string Code, string Name)?>(("routine-code", "Routine Name"));

    // --- Detalle de semana ---

    public Task<EnrollmentWeekDetailDto?> GetEnrollmentWeekDetailAsync(
        Guid enrollmentId,
        int weekNumber,
        Guid clinicianUserId,
        CancellationToken ct = default)
        => Task.FromResult<EnrollmentWeekDetailDto?>(null);

    public Task<EnrollmentWeekDetailDto> ReplaceEnrollmentWeekTasksAsync(
        Guid enrollmentId, int weekNumber, IReadOnlyList<WeeklyDayTemplate> tasks, Guid? actorId = null, CancellationToken ct = default)
        => Task.FromResult(new EnrollmentWeekDetailDto(weekNumber, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow), null, null, []));

    // --- ERP gamificación (SPEC §23) ---

    public Task<ProgramErpDashboardDto> GetErpDashboardAsync(CancellationToken ct = default)
        => Task.FromResult(new ProgramErpDashboardDto(
            new ErpDashboardKpis(0, 0, 0, 0, 0, null, null, null, 0, 0, 0),
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            []));

    public Task<ProgramErpTodayDto> GetErpTodayAsync(CancellationToken ct = default)
        => Task.FromResult(new ProgramErpTodayDto([], [], [], []));

    public Task<ProgramErpAdherenciaDto> GetErpAdherenciaAsync(
        int page, int pageSize,
        string? search, string? sortBy, string? sortDir,
        CancellationToken ct = default)
        => Task.FromResult(new ProgramErpAdherenciaDto(
            [],
            [],
            new PaginatedErpAdherenciaTabla([], 0, page, pageSize, 0)));

    public Task<ProgramErpCofresDto> GetErpCofresAsync(CancellationToken ct = default)
        => Task.FromResult(new ProgramErpCofresDto(
            [],
            new ErpMilestoneCounts(0, 0, 0, 0, 0, 0, 0, 0),
            [],
            0));

    public Task<PatientOverviewDto?> GetPatientOverviewAsync(Guid patientId, CancellationToken ct = default)
        => Task.FromResult<PatientOverviewDto?>(null);

    // --- Bitácora de actividad (ERP) ---

    public Task<PaginatedActivityLogResult> ListActivityLogAsync(
        int page,
        int pageSize,
        string? tableName,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? actor,
        CancellationToken ct = default)
        => Task.FromResult(new PaginatedActivityLogResult([], 0, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), 0));

    // --- Exporte CSV (B14) ---

    public async IAsyncEnumerable<EnrollmentExportRow> StreamEnrollmentsForExportAsync(
        Guid? clinicId,
        DateTime? from,
        DateTime? to,
        IReadOnlyList<Guid>? scopedPatientIds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    // --- Reconciliación de rachas (B12) ---

    public Task<StreakReconciliationSummary> ReconcileStreaksAsync(CancellationToken ct = default)
        => Task.FromResult(new StreakReconciliationSummary(0, 0, []));
}
