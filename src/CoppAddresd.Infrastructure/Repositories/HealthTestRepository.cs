using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

public sealed class HealthTestRepository(AppDbContext dbContext) : IHealthTestRepository
{
    // --- Instrumentos ---

    public async Task<HealthTestInstrument?> GetInstrumentByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestInstruments.AsNoTracking()
            .Include(x => x.Versions.OrderBy(v => v.VersionNumber))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<HealthTestInstrument?> GetInstrumentByCodeAsync(
        string code,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestInstruments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, ct);

    public async Task<(IReadOnlyList<HealthTestInstrument> Items, int Total)> ListInstrumentsAsync(
        string? search,
        string? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.HealthTestInstruments.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.Contains(search) || x.Code.Contains(search));

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Category == category);

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<HealthTestInstrument> AddInstrumentAsync(
        HealthTestInstrument instrument,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestInstruments.Add(instrument);
        await dbContext.SaveChangesAsync(ct);
        return instrument;
    }

    public async Task UpdateInstrumentAsync(
        HealthTestInstrument instrument,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestInstruments.Update(instrument);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Versiones ---

    public async Task<HealthTestVersion?> GetVersionByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) => await dbContext.HealthTestVersions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<HealthTestVersion?> GetVersionWithDetailsAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestVersions.AsNoTracking()
            .Include(x => x.Instrument)
            .Include(x => x.Questions.OrderBy(q => q.SortOrder))
                .ThenInclude(q => q.Options.OrderBy(o => o.SortOrder))
            .Include(x => x.ScoreRanges)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<HealthTestVersion>> ListVersionsByInstrumentAsync(
        Guid instrumentId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestVersions.AsNoTracking()
            .Where(x => x.InstrumentId == instrumentId)
            .OrderByDescending(x => x.VersionNumber)
            .ToListAsync(ct);

    public async Task<HealthTestVersion> AddVersionAsync(
        HealthTestVersion version,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestVersions.Add(version);
        await dbContext.SaveChangesAsync(ct);
        return version;
    }

    public async Task UpdateVersionAsync(HealthTestVersion version, CancellationToken ct = default)
    {
        dbContext.HealthTestVersions.Update(version);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<int> GetNextVersionNumberAsync(
        Guid instrumentId,
        CancellationToken ct = default
    ) =>
        (
            await dbContext
                .HealthTestVersions.AsNoTracking()
                .Where(x => x.InstrumentId == instrumentId)
                .MaxAsync(x => (int?)x.VersionNumber, ct)
            ?? 0
        ) + 1;

    // --- Preguntas y opciones ---

    public async Task<HealthTestQuestion?> GetQuestionByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestQuestions.AsNoTracking()
            .Include(x => x.Options.OrderBy(o => o.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<HealthTestQuestion>> ListQuestionsByVersionAsync(
        Guid versionId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestQuestions.AsNoTracking()
            .Include(x => x.Options.OrderBy(o => o.SortOrder))
            .Where(x => x.VersionId == versionId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HealthTestAnswerOption>> ListOptionsByQuestionAsync(
        Guid questionId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAnswerOptions.AsNoTracking()
            .Where(x => x.QuestionId == questionId && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(ct);

    public async Task AddQuestionsRangeAsync(
        IEnumerable<HealthTestQuestion> questions,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestQuestions.AddRange(questions);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Rangos ---

    public async Task<IReadOnlyList<HealthTestScoreRange>> ListRangesByVersionAsync(
        Guid versionId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestScoreRanges.AsNoTracking()
            .Where(x => x.VersionId == versionId && x.IsActive)
            .OrderBy(x => x.MinValue)
            .ToListAsync(ct);

    public async Task AddRangesRangeAsync(
        IEnumerable<HealthTestScoreRange> ranges,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestScoreRanges.AddRange(ranges);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Baterías ---

    public async Task<HealthTestBattery?> GetBatteryByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext.HealthTestBatteries.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<HealthTestBattery?> GetBatteryWithItemsAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestBatteries.AsNoTracking()
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<HealthTestBattery?> GetAutoAssignBatteryAsync(
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestBatteries.AsNoTracking()
            .Include(x => x.Items)
            .Where(x => x.IsActive && x.AutoAssignOnPatientCreate)
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<HealthTestBattery> Items, int Total)> ListBatteriesAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.HealthTestBatteries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.Contains(search) || x.Code.Contains(search));

        if (isActive.HasValue)
            query = query.Where(x => x.IsActive == isActive.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(x => x.Items.OrderBy(i => i.SortOrder))
            .OrderBy(x => x.Name)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<HealthTestBattery> AddBatteryAsync(
        HealthTestBattery battery,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestBatteries.Add(battery);
        await dbContext.SaveChangesAsync(ct);
        return battery;
    }

    public async Task UpdateBatteryAsync(HealthTestBattery battery, CancellationToken ct = default)
    {
        dbContext.HealthTestBatteries.Update(battery);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task AddBatteryItemsRangeAsync(
        IEnumerable<HealthTestBatteryItem> items,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestBatteryItems.AddRange(items);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteBatteryItemsByBatteryIdAsync(
        Guid batteryId,
        CancellationToken ct = default
    )
    {
        await dbContext
            .HealthTestBatteryItems.Where(x => x.BatteryId == batteryId)
            .ExecuteDeleteAsync(ct);
    }

    // --- Asignaciones ---

    public async Task<HealthTestAssignment?> GetAssignmentByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAssignments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<HealthTestAssignment?> GetAssignmentWithDetailsAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAssignments.AsNoTracking()
            .Include(x => x.Version)
                .ThenInclude(v => v!.Instrument)
            .Include(x => x.Patient)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<(IReadOnlyList<HealthTestAssignment> Items, int Total)> ListAssignmentsAsync(
        Guid? patientId,
        Guid? versionId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.HealthTestAssignments.AsNoTracking().AsQueryable();

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        if (versionId.HasValue)
            query = query.Where(x => x.VersionId == versionId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status.ToString() == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(x => x.Patient)
            .Include(x => x.Version)
                .ThenInclude(v => v!.Instrument)
            .OrderByDescending(x => x.AssignedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(
        IReadOnlyList<HealthTestAssignment> Items,
        int Total
    )> ListPendingAssignmentsForProfessionalAsync(
        IReadOnlyCollection<Guid> patientIds,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .HealthTestAssignments.AsNoTracking()
            .Where(x => patientIds.Contains(x.PatientId))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status.ToString() == status);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(x => x.Patient)
            .Include(x => x.Version)
                .ThenInclude(v => v!.Instrument)
            .OrderByDescending(x => x.AssignedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<HealthTestAssignment>> ListAssignmentsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAssignments.AsNoTracking()
            .Include(x => x.Version)
                .ThenInclude(v => v!.Instrument)
            .Where(x => x.PatientId == patientId)
            .OrderBy(x => x.AssignedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HealthTestAssignment>> ListActiveAssignmentsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAssignments.AsNoTracking()
            .Include(x => x.Version)
                .ThenInclude(v => v!.Instrument)
            .Where(x =>
                x.PatientId == patientId
                && (
                    x.Status == HealthTestAssignmentStatus.pending
                    || x.Status == HealthTestAssignmentStatus.in_progress
                )
            )
            .OrderBy(x => x.AssignedAt)
            .ToListAsync(ct);

    public async Task<HealthTestAssignment> AddAssignmentAsync(
        HealthTestAssignment assignment,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task AddAssignmentsRangeAsync(
        IEnumerable<HealthTestAssignment> assignments,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestAssignments.AddRange(assignments);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task UpdateAssignmentAsync(
        HealthTestAssignment assignment,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestAssignments.Update(assignment);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Asignaciones de batería ---

    public async Task<HealthTestBatteryAssignment?> GetBatteryAssignmentByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestBatteryAssignments.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<HealthTestBatteryAssignment> AddBatteryAssignmentAsync(
        HealthTestBatteryAssignment assignment,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestBatteryAssignments.Add(assignment);
        await dbContext.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task UpdateBatteryAssignmentAsync(
        HealthTestBatteryAssignment assignment,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestBatteryAssignments.Update(assignment);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<
        IReadOnlyList<HealthTestBatteryAssignment>
    > ListBatteryAssignmentsByPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await dbContext
            .HealthTestBatteryAssignments.AsNoTracking()
            .Include(x => x.Battery)
            .Where(x => x.PatientId == patientId)
            .OrderBy(x => x.AssignedAt)
            .ToListAsync(ct);

    // --- Evaluaciones ---

    public async Task<HealthTestEvaluation?> GetEvaluationByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestEvaluations.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<HealthTestEvaluation?> GetEvaluationWithDetailsAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestEvaluations.AsNoTracking()
            .Include(x => x.Version)
                .ThenInclude(v => v!.Instrument)
            .Include(x => x.Version)
                .ThenInclude(v => v!.Questions.OrderBy(q => q.SortOrder))
                    .ThenInclude(q => q.Options)
            .Include(x => x.Version)
                .ThenInclude(v => v!.ScoreRanges)
            .Include(x => x.Responses)
                .ThenInclude(r => r.Question)
            .Include(x => x.Responses)
                .ThenInclude(r => r.AnswerOption)
            .Include(x => x.Results)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<HealthTestEvaluation> AddEvaluationAsync(
        HealthTestEvaluation evaluation,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestEvaluations.Add(evaluation);
        await dbContext.SaveChangesAsync(ct);
        return evaluation;
    }

    public async Task UpdateEvaluationAsync(
        HealthTestEvaluation evaluation,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestEvaluations.Update(evaluation);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<Guid?> GetEvaluationByAssignmentAsync(
        Guid assignmentId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestEvaluations.AsNoTracking()
            .Where(x => x.AssignmentId == assignmentId)
            .OrderByDescending(x => x.StartedAt)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<HealthTestEvaluation>> ListEvaluationsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestEvaluations.AsNoTracking()
            .Include(x => x.Version)
                .ThenInclude(v => v!.Instrument)
            .Include(x => x.Results)
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(ct);

    // --- Respuestas y resultados ---

    public async Task AddResponsesRangeAsync(
        IEnumerable<HealthTestResponse> responses,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestResponses.AddRange(responses);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task DeleteResponsesByEvaluationAsync(
        Guid evaluationId,
        CancellationToken ct = default
    )
    {
        await dbContext
            .HealthTestResponses.Where(x => x.EvaluationId == evaluationId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<HealthTestResponse>> ListResponsesByEvaluationAsync(
        Guid evaluationId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestResponses.AsNoTracking()
            .Where(x => x.EvaluationId == evaluationId)
            .ToListAsync(ct);

    public async Task AddResultsRangeAsync(
        IEnumerable<HealthTestResult> results,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestResults.AddRange(results);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<HealthTestResult>> ListResultsByEvaluationAsync(
        Guid evaluationId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestResults.AsNoTracking()
            .Where(x => x.EvaluationId == evaluationId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HealthTestResult>> ListResultsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await (
            from r in dbContext.HealthTestResults.AsNoTracking()
            join e in dbContext.HealthTestEvaluations on r.EvaluationId equals e.Id
            where e.PatientId == patientId
            orderby e.CompletedAt descending, r.Code
            select r
        ).ToListAsync(ct);

    public async Task<(
        IReadOnlyList<HealthTestEvaluation> Items,
        int Total
    )> ListEvaluationsByPatientPageAsync(
        Guid patientId,
        string? status,
        DateTime? from,
        DateTime? to,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .HealthTestEvaluations.AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .AsQueryable();

        if (
            !string.IsNullOrWhiteSpace(status)
            && Enum.TryParse(status, ignoreCase: true, out HealthTestEvaluationStatus statusEnum)
        )
            query = query.Where(x => x.Status == statusEnum);

        if (from.HasValue)
            query = query.Where(x => x.CompletedAt >= from.Value);

        if (to.HasValue)
            query = query.Where(x => x.CompletedAt <= to.Value);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(x => x.Version!.Instrument!.Category == category);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(x => x.Version)
                .ThenInclude(v => v!.Instrument)
            .Include(x => x.Results)
            .OrderByDescending(x => x.CompletedAt ?? x.StartedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    /// <summary>
    /// Evaluaciones del paciente para un mismo instrumento (intentos del test),
    /// ordenadas por inicio (y id como desempate) para calcular el número de
    /// intento y la comparativa histórica.
    /// </summary>
    public async Task<IReadOnlyList<HealthTestEvaluation>> ListEvaluationsByInstrumentAsync(
        Guid patientId,
        Guid instrumentId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestEvaluations.AsNoTracking()
            .Where(x => x.PatientId == patientId && x.Version!.InstrumentId == instrumentId)
            .OrderBy(x => x.StartedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HealthTestComment>> ListCommentsByEvaluationAsync(
        Guid evaluationId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestComments.AsNoTracking()
            .Where(x => x.EvaluationId == evaluationId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HealthTestAssignment>> ListAssignmentsByBatteryAssignmentAsync(
        Guid batteryAssignmentId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAssignments.AsNoTracking()
            .Where(x => x.BatteryAssignmentId == batteryAssignmentId)
            .ToListAsync(ct);

    // --- Indicadores y alertas ---

    public async Task<HealthTestIndicatorDef?> GetIndicatorDefByCodeAsync(
        string code,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestIndicatorDefs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, ct);

    public async Task<IReadOnlyList<HealthTestIndicatorDef>> ListActiveIndicatorDefsAsync(
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestIndicatorDefs.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HealthTestAlertRule>> ListActiveAlertRulesAsync(
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAlertRules.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync(ct);

    public async Task<HealthTestAlertRule?> GetAlertRuleByCodeAsync(
        string code,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAlertRules.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code, ct);

    public async Task<HealthTestAlert> AddAlertAsync(
        HealthTestAlert alert,
        CancellationToken ct = default
    )
    {
        dbContext.HealthTestAlerts.Add(alert);
        await dbContext.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<(IReadOnlyList<HealthTestAlert> Items, int Total)> ListAlertsAsync(
        Guid? patientId,
        string? status,
        string? severity,
        int page,
        int pageSize,
        CancellationToken ct = default
    )
    {
        var query = dbContext.HealthTestAlerts.AsNoTracking().AsQueryable();

        if (patientId.HasValue)
            query = query.Where(x => x.PatientId == patientId.Value);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status.ToString() == status);

        if (!string.IsNullOrWhiteSpace(severity))
            query = query.Where(x => x.Severity.ToString() == severity);

        var total = await query.CountAsync(ct);
        var items = await query
            .Include(x => x.Patient)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(Math.Clamp(pageSize, 1, 100))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<HealthTestAlert>> ListAlertsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAlerts.AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);

    public async Task<HealthTestAlert?> GetAlertByIdAsync(
        Guid id,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAlerts.AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Rule)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task UpdateAlertAsync(HealthTestAlert alert, CancellationToken ct = default)
    {
        dbContext.HealthTestAlerts.Update(alert);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<bool> AlertExistsForResultRuleAsync(
        Guid resultId,
        Guid ruleId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAlerts.AsNoTracking()
            .AnyAsync(x => x.ResultId == resultId && x.RuleId == ruleId, ct);

    // --- Pacientes ---

    public async Task<IReadOnlyList<Guid>> GetPatientIdsForProfessionalAsync(
        Guid professionalId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .PatientProfessionalAssignments.AsNoTracking()
            .Where(x => x.ProfessionalId == professionalId && x.Status == "Active")
            .Select(x => x.PatientId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<bool> PatientBelongsToProfessionalAsync(
        Guid patientId,
        Guid professionalId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .PatientProfessionalAssignments.AsNoTracking()
            .AnyAsync(
                x =>
                    x.PatientId == patientId
                    && x.ProfessionalId == professionalId
                    && x.Status == "Active",
                ct
            );

    public async Task<bool> PatientExistsAsync(Guid patientId, CancellationToken ct = default) =>
        await dbContext
            .PatientProfiles.AsNoTracking()
            .AnyAsync(x => x.Id == patientId && x.DeletedAt == null, ct);

    public async Task<PatientProfile?> GetPatientByUserIdAsync(
        Guid userId,
        CancellationToken ct = default
    ) =>
        await dbContext
            .PatientProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.DeletedAt == null, ct);

    public async Task<int> CountPatientsAsync(CancellationToken ct = default) =>
        await dbContext.PatientProfiles.AsNoTracking().CountAsync(x => x.DeletedAt == null, ct);

    public async Task<int> CountAssignmentsByStatusAsync(
        HealthTestAssignmentStatus status,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAssignments.AsNoTracking()
            .CountAsync(x => x.Status == status, ct);

    public async Task<int> CountAssignmentsByStatusForPatientsAsync(
        HealthTestAssignmentStatus status,
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAssignments.AsNoTracking()
            .CountAsync(x => x.Status == status && patientIds.Contains(x.PatientId), ct);

    public async Task<int> CountEvaluationsBySeverityAsync(
        HealthTestSeverity severity,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestResults.AsNoTracking()
            .CountAsync(
                x => x.Severity == severity && x.ResultType == HealthTestResultType.score,
                ct
            );

    public async Task<int> CountEvaluationsBySeverityForPatientsAsync(
        HealthTestSeverity severity,
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestResults.AsNoTracking()
            .Where(x =>
                x.Severity == severity
                && x.ResultType == HealthTestResultType.score
                && dbContext.HealthTestEvaluations.Any(e =>
                    e.Id == x.EvaluationId && patientIds.Contains(e.PatientId)
                )
            )
            .CountAsync(ct);

    public async Task<int> CountAlertsByStatusAsync(
        HealthTestAlertStatus status,
        CancellationToken ct = default
    ) => await dbContext.HealthTestAlerts.AsNoTracking().CountAsync(x => x.Status == status, ct);

    public async Task<int> CountAlertsByStatusForPatientsAsync(
        HealthTestAlertStatus status,
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAlerts.AsNoTracking()
            .CountAsync(x => x.Status == status && patientIds.Contains(x.PatientId), ct);

    public async Task AddCommentAsync(HealthTestComment comment, CancellationToken ct = default)
    {
        dbContext.HealthTestComments.Add(comment);
        await dbContext.SaveChangesAsync(ct);
    }

    // --- Transacción multi-paso (submit) ---

    public async Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        await dbContext.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                await action();
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<Task<T>> action,
        CancellationToken ct = default
    )
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await action();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
}
