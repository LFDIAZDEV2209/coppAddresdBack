using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.ClinicalXp;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Nutrition;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
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
        Guid? patientId, ProgramEnrollmentStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = Enrollments.Values.AsEnumerable();
        if (patientId.HasValue)
        {
            query = query.Where(e => e.PatientId == patientId.Value);
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
}