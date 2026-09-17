using CoppAddresd.Application.Features.HealthTests;
using CoppAddresd.Application.Features.HealthTests.Alerts;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

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

    /// <summary>
    /// Geo agregado del mapa: fast path desde el rollup snapshot
    /// (<c>app.health_test_geo_rollups</c>); si el rollup está vacío (backfill
    /// pendiente) cae a la agregación en memoria original (mismo resultado).
    /// </summary>
    public async Task<CoppAddresd.Application.Features.HealthTests.HealthTestsGeoDto> GetGeoAsync(
        CancellationToken ct = default
    )
    {
        if (await GeoRollupExistsAsync(ct))
        {
            return await GetGeoFromRollupAsync(ct);
        }

        return await ComputeGeoInMemoryAsync(ct);
    }

    /// <summary>
    /// Lectura del rollup snapshot: O(#ciudades) + 2 consultas acotadas para
    /// las alertas top-4. Contrato idéntico a la agregación en memoria.
    /// </summary>
    public async Task<CoppAddresd.Application.Features.HealthTests.HealthTestsGeoDto> GetGeoFromRollupAsync(
        CancellationToken ct = default
    )
    {
        var rows = await dbContext
            .HealthTestGeoRollups.AsNoTracking()
            .OrderByDescending(r => r.PatientsCount)
            .ToListAsync(ct);

        var geoCities = rows
            .Select(r => new CoppAddresd.Application.Features.HealthTests.HealthTestsGeoCityDto(
                r.CityId,
                r.CityName,
                r.StateCode,
                (int)r.PatientsCount,
                (int)r.EvaluatedCount,
                r.EvaluatedCount > 0
                    ? Math.Round((double)r.HighRiskCount / r.EvaluatedCount * 100, 1)
                    : null,
                r.AvgScoreCount > 0
                    ? Math.Round((double)(r.AvgScoreSum / r.AvgScoreCount), 1)
                    : null,
                null,
                null
            ))
            .ToList();

        int totalPatients = (int)rows.Sum(r => r.PatientsCount);
        int highRiskGlobal = (int)rows.Sum(r => r.HighRiskCount);

        long totalEvaluated = rows.Sum(r => r.AvgScoreCount);
        decimal totalScoreSum = rows.Sum(r => r.AvgScoreSum);
        double? avgScoreGlobal =
            totalEvaluated > 0 ? Math.Round((double)(totalScoreSum / totalEvaluated), 1) : null;

        var alerts =
            new List<CoppAddresd.Application.Features.HealthTests.HealthTestsGeoAlertDto>();

        // Top-4 pacientes de alto riesgo: consultas acotadas (índice
        // ix_health_test_results_severity_type), no el escaneo completo.
        var highRiskPatientIds = await (
            from r in dbContext.HealthTestResults.AsNoTracking()
            join e in dbContext.HealthTestEvaluations.AsNoTracking() on r.EvaluationId equals e.Id
            where
                r.ResultType == CoppAddresd.Domain.Enums.HealthTests.HealthTestResultType.score
                && (
                    r.Severity == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.high
                    || r.Severity == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.critical
                )
            select e.PatientId
        )
            .Distinct()
            .OrderBy(x => x)
            .Take(4)
            .ToListAsync(ct);

        if (highRiskPatientIds.Count > 0)
        {
            var scoreRows = await (
                from r in dbContext.HealthTestResults.AsNoTracking()
                join e in dbContext.HealthTestEvaluations.AsNoTracking() on r.EvaluationId equals e.Id
                where
                    r.ResultType == CoppAddresd.Domain.Enums.HealthTests.HealthTestResultType.score
                    && highRiskPatientIds.Contains(e.PatientId)
                select new { e.PatientId, r.Severity, r.Value }
            ).ToListAsync(ct);

            var names = await dbContext
                .PatientProfiles.AsNoTracking()
                .Where(p => highRiskPatientIds.Contains(p.Id))
                .Select(p => new { p.Id, Name = (p.FirstName + " " + p.LastName).Trim() })
                .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

            var severityOrder = new Dictionary<
                CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity,
                int
            >
            {
                [CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.low] = 0,
                [CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.moderate] = 1,
                [CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.high] = 2,
                [CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.critical] = 3,
            };

            foreach (var pid in highRiskPatientIds)
            {
                var grp = scoreRows.Where(x => x.PatientId == pid).ToList();
                var worst = grp
                    .OrderByDescending(x =>
                        x.Severity.HasValue && severityOrder.TryGetValue(x.Severity.Value, out var ov)
                            ? ov
                            : -1
                    )
                    .First();
                var sev = worst.Severity ?? CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.low;
                alerts.Add(
                    new CoppAddresd.Application.Features.HealthTests.HealthTestsGeoAlertDto(
                        pid,
                        names.GetValueOrDefault(pid, "Paciente"),
                        sev == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.critical
                            ? "Riesgo crítico"
                            : "Riesgo alto",
                        grp.Count > 0 ? (double)Math.Round(grp.Average(x => x.Value), 4) : null,
                        sev.ToString()
                    )
                );
            }
        }

        return new CoppAddresd.Application.Features.HealthTests.HealthTestsGeoDto(
            geoCities,
            alerts,
            totalPatients,
            avgScoreGlobal,
            highRiskGlobal
        );
    }

    /// <summary>
    /// Agregación geo en memoria (fallback original cuando el rollup está vacío).
    /// </summary>
    private async Task<CoppAddresd.Application.Features.HealthTests.HealthTestsGeoDto> ComputeGeoInMemoryAsync(
        CancellationToken ct
    )
    {
        // Pacientes con ciudad (solo activos no borrados)
        var patientsWithCity = await dbContext
            .PatientProfiles.AsNoTracking()
            .Where(p => p.DeletedAt == null && p.CityId != null)
            .Select(p => new { p.Id, CityId = p.CityId!.Value })
            .ToListAsync(ct);

        if (patientsWithCity.Count == 0)
        {
            return new CoppAddresd.Application.Features.HealthTests.HealthTestsGeoDto(
                [],
                [],
                0,
                null,
                0
            );
        }

        var patientIds = patientsWithCity.Select(p => p.Id).ToList();
        var cityIds = patientsWithCity.Select(p => p.CityId).Distinct().ToList();

        // Detalle de ciudades + estado
        var cityDetails = await dbContext
            .Cities.AsNoTracking()
            .Where(c => cityIds.Contains(c.Id))
            .Join(
                dbContext.States.AsNoTracking(),
                c => c.StateId,
                s => s.Id,
                (c, s) =>
                    new
                    {
                        c.Id,
                        c.Name,
                        StateCode = s.Code,
                    }
            )
            .ToListAsync(ct);
        var cityDetailMap = cityDetails.ToDictionary(c => c.Id);

        // Resultados tipo score con severidad para calcular riesgo por paciente
        var allScoreResults = await (
            from r in dbContext.HealthTestResults.AsNoTracking()
            join e in dbContext.HealthTestEvaluations.AsNoTracking() on r.EvaluationId equals e.Id
            where
                r.ResultType == CoppAddresd.Domain.Enums.HealthTests.HealthTestResultType.score
                && patientIds.Contains(e.PatientId)
            select new
            {
                e.PatientId,
                r.Severity,
                r.Value,
                e.CompletedAt,
            }
        ).ToListAsync(ct);

        // Worst severity por paciente + avg score por paciente (latest)
        var severityOrder = new Dictionary<
            CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity,
            int
        >
        {
            [CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.low] = 0,
            [CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.moderate] = 1,
            [CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.high] = 2,
            [CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.critical] = 3,
        };

        var patientRisk =
            new Dictionary<Guid, CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity>();
        var patientScore = new Dictionary<Guid, decimal>();
        foreach (var grp in allScoreResults.GroupBy(x => x.PatientId))
        {
            var worst = grp.OrderByDescending(x =>
                    x.Severity.HasValue && severityOrder.TryGetValue(x.Severity.Value, out var ov)
                        ? ov
                        : -1
                )
                .First();
            patientRisk[grp.Key] =
                worst.Severity ?? CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.low;
            // avg score across results (simple mean)
            patientScore[grp.Key] = grp.Average(x => x.Value);
        }

        // Alta riesgo = high o critical
        int highRiskGlobal = patientRisk.Count(kv =>
            kv.Value == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.high
            || kv.Value == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.critical
        );

        // Agrupar por ciudad
        var citiesRaw = patientsWithCity
            .GroupBy(p => p.CityId)
            .Select(g => new
            {
                CityId = g.Key,
                Count = g.Count(),
                PatientIds = g.Select(x => x.Id).ToList(),
            })
            .ToList();

        var geoCities =
            new List<CoppAddresd.Application.Features.HealthTests.HealthTestsGeoCityDto>();
        foreach (var c in citiesRaw)
        {
            var detail = cityDetailMap.TryGetValue(c.CityId, out var cd) ? cd : null;
            int evaluatedInCity = c.PatientIds.Count(pid => patientRisk.ContainsKey(pid));
            int highInCity = c.PatientIds.Count(pid =>
                patientRisk.TryGetValue(pid, out var sev)
                && (
                    sev == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.high
                    || sev == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.critical
                )
            );
            // % de alto riesgo sobre pacientes evaluados (con score); sin
            // evaluaciones no hay dato (null → "Sin datos" en el mapa).
            double? highPct =
                evaluatedInCity > 0
                    ? Math.Round((double)highInCity / evaluatedInCity * 100, 1)
                    : null;
            var scoresInCity = c
                .PatientIds.Where(pid => patientScore.ContainsKey(pid))
                .Select(pid => patientScore[pid])
                .ToList();
            double? avgScore =
                scoresInCity.Count > 0 ? (double)Math.Round(scoresInCity.Average(), 1) : null;

            geoCities.Add(
                new CoppAddresd.Application.Features.HealthTests.HealthTestsGeoCityDto(
                    c.CityId,
                    detail?.Name ?? "Desconocido",
                    detail?.StateCode,
                    c.Count,
                    evaluatedInCity,
                    highPct,
                    avgScore,
                    null,
                    null
                )
            );
        }

        geoCities = geoCities.OrderByDescending(c => c.Count).ToList();

        // Alertas top: pacientes con alto riesgo (max 4)
        var alerts =
            new List<CoppAddresd.Application.Features.HealthTests.HealthTestsGeoAlertDto>();
        var highRiskPatientIds = patientRisk
            .Where(kv =>
                kv.Value == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.high
                || kv.Value == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.critical
            )
            .Select(kv => kv.Key)
            .Take(4)
            .ToList();

        if (highRiskPatientIds.Count > 0)
        {
            var alertPatients = await dbContext
                .PatientProfiles.AsNoTracking()
                .Where(p => highRiskPatientIds.Contains(p.Id))
                .Select(p => new { p.Id, Name = (p.FirstName + " " + p.LastName).Trim() })
                .ToListAsync(ct);

            foreach (var pid in highRiskPatientIds)
            {
                var name = alertPatients.FirstOrDefault(x => x.Id == pid)?.Name ?? "Paciente";
                var sev = patientRisk[pid];
                alerts.Add(
                    new CoppAddresd.Application.Features.HealthTests.HealthTestsGeoAlertDto(
                        pid,
                        name,
                        sev == CoppAddresd.Domain.Enums.HealthTests.HealthTestSeverity.critical
                            ? "Riesgo crítico"
                            : "Riesgo alto",
                        patientScore.TryGetValue(pid, out var sc) ? (double)sc : null,
                        sev.ToString()
                    )
                );
            }
        }

        int totalPatients = patientsWithCity.Select(p => p.Id).Distinct().Count();
        double? avgScoreGlobal =
            patientScore.Count > 0 ? (double)Math.Round(patientScore.Values.Average(), 1) : null;

        return new CoppAddresd.Application.Features.HealthTests.HealthTestsGeoDto(
            geoCities,
            alerts,
            totalPatients,
            avgScoreGlobal,
            highRiskGlobal
        );
    }

    /// <summary>
    /// Asignaciones con paciente, versión/instrumento, evaluaciones y resultados
    /// (tabla maestra del ERP, una sola consulta). Con <c>professionalId</c> filtra
    /// por el alcance del profesional (patient_professionals); con
    /// <c>patientIds</c> acota a un conjunto (filtro geográfico del dashboard).
    /// </summary>
    public async Task<IReadOnlyList<HealthTestAssignment>> ListAssignmentsWithPatientDataAsync(
        Guid? professionalId,
        IReadOnlyCollection<Guid>? patientIds = null,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .HealthTestAssignments.AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Patient)
            .Include(a => a.Version!)
                .ThenInclude(v => v.Instrument)
            .Include(a => a.Evaluations)
                .ThenInclude(e => e.Results)
            .AsQueryable();

        if (professionalId.HasValue)
        {
            query = query.Where(a =>
                dbContext.PatientProfessionalAssignments.Any(pp =>
                    pp.PatientId == a.PatientId && pp.ProfessionalId == professionalId.Value
                )
            );
        }

        if (patientIds is not null)
        {
            query = query.Where(a => patientIds.Contains(a.PatientId));
        }

        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> ListActiveAlertCountsByPatientAsync(
        CancellationToken ct = default
    ) =>
        await dbContext
            .HealthTestAlerts.AsNoTracking()
            .Where(a =>
                a.Status == HealthTestAlertStatus.active
                || a.Status == HealthTestAlertStatus.reviewing
            )
            .GroupBy(a => a.PatientId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> ListProfessionalNamesByPatientAsync(
        CancellationToken ct = default
    ) =>
        await (
            from pp in dbContext.PatientProfessionalAssignments.AsNoTracking()
            join pr in dbContext.Professionals on pp.ProfessionalId equals pr.Id
            join e in dbContext.Employees on pr.EmployeeId equals e.Id
            where e.Status == "Active"
            select new { pp.PatientId, Name = (e.FirstName + " " + e.LastName).Trim() }
        )
            .Distinct()
            .ToDictionaryAsync(x => x.PatientId, x => x.Name, ct);

    public async Task<IReadOnlyDictionary<Guid, string>> ListClinicNamesByIdsAsync(
        IEnumerable<Guid> clinicIds,
        CancellationToken ct = default
    )
    {
        var ids = clinicIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext
            .Clinics.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ListInsurerNamesByIdsAsync(
        IEnumerable<Guid> insurerIds,
        CancellationToken ct = default
    )
    {
        var ids = insurerIds.ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext
            .Insurers.AsNoTracking()
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Name, ct);
    }

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

    /// <summary>
    /// Pacientes activos con ciudad dentro de uno o varios estados (códigos, ej.
    /// "CA", unión) o de una ciudad concreta (precedencia). Se apoya en la
    /// relación PatientProfile.City → City.State y proyecta solo el Id (sin
    /// tracking).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetPatientIdsByGeoAsync(
        IReadOnlyCollection<string>? stateCodes,
        Guid? cityId,
        CancellationToken ct = default
    )
    {
        var query = dbContext
            .PatientProfiles.AsNoTracking()
            .Where(p => p.DeletedAt == null && p.CityId != null);

        if (cityId.HasValue)
        {
            query = query.Where(p => p.CityId == cityId.Value);
        }
        else
        {
            var normalized = (stateCodes ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();
            if (normalized.Count == 0)
            {
                return Array.Empty<Guid>();
            }

            query = query.Where(p => normalized.Contains(p.City!.State!.Code));
        }

        return await query.Select(p => p.Id).Distinct().ToListAsync(ct);
    }

    // --- Filtro geográfico set-based (sin materializar GUIDs) ---

    /// <summary>¿Trae zona geográfica real (ciudad o algún estado)?</summary>
    private static bool ZoneHasFilter(IReadOnlyCollection<string>? stateCodes, Guid? cityId) =>
        cityId.HasValue
        || (stateCodes ?? []).Any(c => !string.IsNullOrWhiteSpace(c));

    /// <summary>
    /// Predicado reutilizable de zona: pacientes activos con ciudad dentro de
    /// los estados dados (unión) o de la ciudad dada (con precedencia).
    /// Proyecta sobre el JOIN cities/states para traducir a SQL sin IN lists.
    /// </summary>
    private IQueryable<PatientProfile> ZonePatientsQuery(
        IReadOnlyCollection<string>? stateCodes,
        Guid? cityId
    )
    {
        var query = dbContext
            .PatientProfiles.AsNoTracking()
            .Where(p => p.DeletedAt == null && p.CityId != null);

        if (cityId.HasValue)
        {
            query = query.Where(p => p.CityId == cityId.Value);
        }
        else
        {
            var normalized = (stateCodes ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();
            if (normalized.Count == 0)
            {
                return query.Where(p => false);
            }

            query = query.Where(p => normalized.Contains(p.City!.State!.Code));
        }

        return query;
    }

    /// <summary>Fila del KPI set-based por zona (mapeo directo de columnas SQL).</summary>
    public sealed record HealthTestStatsZoneRow(
        long Total,
        long Pending,
        long Completed,
        long HighRisk,
        long ActiveAlerts
    );

    /// <inheritdoc />
    public async Task<HealthTestStatsDto> GetHealthTestStatsForZoneAsync(
        Guid? professionalId,
        IReadOnlyCollection<string>? stateCodes,
        Guid? cityId,
        CancellationToken ct = default
    )
    {
        if (!cityId.HasValue)
        {
            var normalized = (stateCodes ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpperInvariant())
                .Where(c => c.Length > 0)
                .Distinct()
                .ToList();
            if (normalized.Count == 0)
            {
                return new HealthTestStatsDto(0, 0, 0, 0, 0);
            }
        }

        // Un solo round-trip: zona como CTE + 5 conteos con EXISTS (índices
        // ix_patient_profiles_city_id / ix_health_test_results_severity_type).
        const string sql = """
            WITH zone AS (
                SELECT p.id
                FROM app.patient_profiles p
                JOIN app.cities c ON c.id = p.city_id
                JOIN app.states s ON s.id = c.state_id
                WHERE p.deleted_at IS NULL
                  AND ((@cityId::uuid IS NOT NULL AND c.id = @cityId::uuid)
                       OR (@cityId::uuid IS NULL AND s.code = ANY(@states)))
                  AND (@profId::uuid IS NULL OR EXISTS (
                        SELECT 1 FROM app.patient_professionals pp
                        WHERE pp.patient_id = p.id
                          AND pp.professional_id = @profId::uuid
                          AND pp.status = 'Active'))
            )
            SELECT
                (SELECT COUNT(*) FROM zone) AS "Total",
                (SELECT COUNT(*) FROM app.health_test_assignments a
                 JOIN zone z ON z.id = a.patient_id
                 WHERE a.status = 'pending') AS "Pending",
                (SELECT COUNT(*) FROM app.health_test_assignments a
                 JOIN zone z ON z.id = a.patient_id
                 WHERE a.status = 'completed') AS "Completed",
                (SELECT COUNT(*) FROM app.health_test_results r
                 JOIN app.health_test_evaluations e ON e.id = r.evaluation_id
                 JOIN zone z ON z.id = e.patient_id
                 WHERE r.severity = 'high' AND r.result_type = 'score') AS "HighRisk",
                (SELECT COUNT(*) FROM app.health_test_alerts al
                 JOIN zone z ON z.id = al.patient_id
                 WHERE al.status = 'active') AS "ActiveAlerts"
            """;

        var cityParam = new NpgsqlParameter("@cityId", NpgsqlDbType.Uuid)
        {
            Value = (object?)cityId ?? DBNull.Value,
        };
        var profParam = new NpgsqlParameter("@profId", NpgsqlDbType.Uuid)
        {
            Value = (object?)professionalId ?? DBNull.Value,
        };
        var statesParam = new NpgsqlParameter("@states", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = (stateCodes ?? []).ToArray(),
        };

        var rows = await dbContext.Database
            .SqlQueryRaw<HealthTestStatsZoneRow>(sql, cityParam, statesParam, profParam)
            .ToListAsync(ct);

        var row = rows.FirstOrDefault() ?? new HealthTestStatsZoneRow(0, 0, 0, 0, 0);
        return new HealthTestStatsDto(
            (int)row.Total,
            (int)row.Pending,
            (int)row.Completed,
            (int)row.HighRisk,
            (int)row.ActiveAlerts
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HealthTestAssignment>> ListAssignmentsWithPatientDataForZoneAsync(
        Guid? professionalId,
        IReadOnlyCollection<string>? stateCodes,
        Guid? cityId,
        CancellationToken ct = default
    )
    {
        if (!ZoneHasFilter(stateCodes, cityId))
        {
            return await ListAssignmentsWithPatientDataAsync(professionalId, null, ct);
        }

        var zonePatientIds = ZonePatientsQuery(stateCodes, cityId).Select(p => p.Id);

        var query = dbContext
            .HealthTestAssignments.AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Patient)
            .Include(a => a.Version!)
                .ThenInclude(v => v.Instrument)
            .Include(a => a.Evaluations)
                .ThenInclude(e => e.Results)
            .AsQueryable()
            .Where(a => zonePatientIds.Contains(a.PatientId));

        if (professionalId.HasValue)
        {
            query = query.Where(a =>
                dbContext.PatientProfessionalAssignments.Any(pp =>
                    pp.PatientId == a.PatientId
                    && pp.ProfessionalId == professionalId.Value
                    && pp.Status == "Active"
                )
            );
        }

        return await query.ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> ListActiveAlertCountsForZoneAsync(
        IReadOnlyCollection<string>? stateCodes,
        Guid? cityId,
        CancellationToken ct = default
    )
    {
        if (!ZoneHasFilter(stateCodes, cityId))
        {
            return await ListActiveAlertCountsByPatientAsync(ct);
        }

        return await dbContext
            .HealthTestAlerts.AsNoTracking()
            .Where(a =>
                (a.Status == HealthTestAlertStatus.active
                 || a.Status == HealthTestAlertStatus.reviewing)
                && ZonePatientsQuery(stateCodes, cityId).Select(p => p.Id).Contains(a.PatientId)
            )
            .GroupBy(a => a.PatientId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> ListProfessionalNamesForZoneAsync(
        IReadOnlyCollection<string>? stateCodes,
        Guid? cityId,
        CancellationToken ct = default
    )
    {
        if (!ZoneHasFilter(stateCodes, cityId))
        {
            return await ListProfessionalNamesByPatientAsync(ct);
        }

        return await (
            from pp in dbContext.PatientProfessionalAssignments.AsNoTracking()
            join pr in dbContext.Professionals on pp.ProfessionalId equals pr.Id
            join e in dbContext.Employees on pr.EmployeeId equals e.Id
            where
                e.Status == "Active"
                && ZonePatientsQuery(stateCodes, cityId).Select(p => p.Id).Contains(pp.PatientId)
            select new { pp.PatientId, Name = (e.FirstName + " " + e.LastName).Trim() }
        )
            .Distinct()
            .ToDictionaryAsync(x => x.PatientId, x => x.Name, ct);
    }

    /// <inheritdoc />
    public async Task<bool> GeoRollupExistsAsync(CancellationToken ct = default) =>
        await dbContext.HealthTestGeoRollups.AsNoTracking().AnyAsync(ct);

    /// <summary>
    /// Etiquetas cortas es-CO de meses (misma salida que toLocaleDateString
    /// "es-CO" month short en el cliente).
    /// </summary>
    private static readonly string[] MesesEsCo =
        ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    /// <inheritdoc />
    public async Task<IReadOnlyList<CoppAddresd.Application.Features.HealthTests.Alerts.HealthTestCoverageTrendPointDto>> GetCoverageTrendAsync(
        CancellationToken ct = default
    )
    {
        var now = DateTime.UtcNow;
        var thisMonth = new DateOnly(now.Year, now.Month, 1);
        var firstMonth = thisMonth.AddMonths(-11);

        var rows = await dbContext
            .HealthTestDailyMetrics.AsNoTracking()
            .Where(m =>
                m.ClinicId == Guid.Empty
                && m.MetricKey == "assignments_count"
                && m.DimensionKey == "completed"
                && m.MetricDate >= firstMonth
            )
            .ToListAsync(ct);

        var totalPatients = await CountPatientsAsync(ct);
        var denominator = Math.Max(totalPatients, 1);

        var byMonth = rows
            .GroupBy(r => new DateOnly(r.MetricDate.Year, r.MetricDate.Month, 1))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalCount));

        var months = new List<CoppAddresd.Application.Features.HealthTests.Alerts.HealthTestCoverageTrendPointDto>(12);
        for (var i = 0; i < 12; i++)
        {
            var month = thisMonth.AddMonths(-11 + i);
            var completed = byMonth.GetValueOrDefault(month);
            var coverage = Math.Round(completed / (double)denominator * 100, 0, MidpointRounding.AwayFromZero);
            months.Add(new CoppAddresd.Application.Features.HealthTests.Alerts.HealthTestCoverageTrendPointDto(
                MesesEsCo[month.Month - 1],
                coverage,
                (int)completed
            ));
        }

        return months;
    }

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
    )
    {
        var statusKey = status.ToString().ToLowerInvariant();
        var preAggSum = await dbContext
            .HealthTestDailyMetrics.AsNoTracking()
            .Where(x => x.MetricKey == "assignments_count" && x.DimensionKey == statusKey && x.ClinicId == Guid.Empty)
            .SumAsync(x => (int?)x.TotalCount, ct);

        if (preAggSum.HasValue && preAggSum.Value > 0)
        {
            return preAggSum.Value;
        }

        return await dbContext
            .HealthTestAssignments.AsNoTracking()
            .CountAsync(x => x.Status == status, ct);
    }

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
    )
    {
        var severityKey = severity.ToString().ToLowerInvariant();
        var preAggSum = await dbContext
            .HealthTestDailyMetrics.AsNoTracking()
            .Where(x => x.MetricKey == "severity_count" && x.DimensionKey == severityKey && x.ClinicId == Guid.Empty)
            .SumAsync(x => (int?)x.TotalCount, ct);

        if (preAggSum.HasValue && preAggSum.Value > 0)
        {
            return preAggSum.Value;
        }

        return await dbContext
            .HealthTestResults.AsNoTracking()
            .CountAsync(
                x => x.Severity == severity && x.ResultType == HealthTestResultType.score,
                ct
            );
    }

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
    )
    {
        var statusKey = status.ToString().ToLowerInvariant();
        var preAggSum = await dbContext
            .HealthTestDailyMetrics.AsNoTracking()
            .Where(x => x.MetricKey == "alerts_count" && x.DimensionKey == statusKey && x.ClinicId == Guid.Empty)
            .SumAsync(x => (int?)x.TotalCount, ct);

        if (preAggSum.HasValue && preAggSum.Value > 0)
        {
            return preAggSum.Value;
        }

        return await dbContext.HealthTestAlerts.AsNoTracking().CountAsync(x => x.Status == status, ct);
    }

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
