using System.Globalization;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Acceso set-based a la batería inicial y persistencia transaccional por paciente.</summary>
public sealed class ProgramAssignmentRepository(AppDbContext db) : IProgramAssignmentRepository
{
    /// <summary>División relacional: existe al menos un requerido y ninguno carece de evaluación completada.</summary>
    public const string CompletedBatterySql = """
        SELECT ba.id AS "Value"
        FROM app.health_test_battery_assignments ba
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        WHERE ba.patient_id = @patientId AND ba.id = @batteryAssignmentId
          AND ba.status <> 'cancelled'
          AND b.code = 'bateria-inicial' AND b.is_active AND b.auto_assign_on_patient_create
          AND EXISTS (SELECT 1 FROM app.health_test_battery_items i
                      WHERE i.battery_id = b.id AND i.is_required)
          AND NOT EXISTS (
              SELECT 1 FROM app.health_test_battery_items i
              WHERE i.battery_id = b.id AND i.is_required
                AND NOT EXISTS (
                    SELECT 1 FROM app.health_test_assignments a
                    JOIN app.health_test_versions v ON v.id = a.version_id
                    JOIN app.health_test_evaluations e ON e.assignment_id = a.id
                    WHERE a.battery_assignment_id = ba.id AND a.patient_id = ba.patient_id
                      AND v.instrument_id = i.instrument_id
                      AND (i.version_id IS NULL OR i.version_id = a.version_id)
                      AND a.status = 'completed' AND e.status = 'completed'
                      AND e.patient_id = ba.patient_id AND e.version_id = a.version_id))
        """;

    public async Task<Guid?> FindCompletedInitialBatteryAsync(Guid patientId, Guid batteryAssignmentId, CancellationToken ct) =>
        await db.Database.SqlQueryRaw<Guid>(CompletedBatterySql,
            new NpgsqlParameter("patientId", patientId), new NpgsqlParameter("batteryAssignmentId", batteryAssignmentId))
            .Select(x => (Guid?)x).SingleOrDefaultAsync(ct);

    public Task<ProgramEnrollment?> FindPendingWithoutRecommendationAsync(Guid patientId, CancellationToken ct) =>
        db.ProgramEnrollments.AsNoTracking().Where(e=>e.PatientId==patientId && e.Status==ProgramEnrollmentStatus.PendingAssignment
            && !db.ProgramAssignmentRecommendations.Any(r=>r.EnrollmentId==e.Id)).OrderBy(e=>e.CreatedAt).FirstOrDefaultAsync(ct);

    public Task<bool> HasCurrentEnrollmentAsync(Guid patientId, CancellationToken ct) =>
        db.ProgramEnrollments.AsNoTracking().AnyAsync(e => e.PatientId == patientId
            && (e.Status == ProgramEnrollmentStatus.Active || e.Status == ProgramEnrollmentStatus.PendingAssignment), ct);

    public async Task ExecuteForPatientAsync(Guid patientId, Func<Task> action, CancellationToken ct)
    {
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                // El perfil siempre existe, incluso cuando todavía no hay enrollment que bloquear.
                // Este lock serializa dos cierres concurrentes sin un nuevo índice de enrollments.
                _ = await db.PatientProfiles.FromSqlInterpolated(
                    $"SELECT * FROM app.patient_profiles WHERE id = {patientId} FOR UPDATE")
                    .SingleAsync(ct);
                await action();
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                db.ChangeTracker.Clear();
                throw;
            }
        });
    }

    public async Task<AssignmentProfileData> ReadProfileAsync(Guid patientId, Guid batteryAssignmentId, CancellationToken ct)
    {
        var patient = await db.PatientProfiles.AsNoTracking().Where(p => p.Id == patientId)
            .Select(p => new { p.Gender, Ethnicity = p.Ethnicity == null ? null : p.Ethnicity.Code }).SingleAsync(ct);
        var evaluations = await db.HealthTestEvaluations.AsNoTracking()
            .Where(e => e.PatientId == patientId && e.Status == HealthTestEvaluationStatus.completed
                && e.Assignment!.BatteryAssignmentId == batteryAssignmentId)
            .OrderByDescending(e => e.CompletedAt).ThenBy(e => e.Id)
            .Select(e => new { e.Id, e.VersionId, e.CompletedAt, TestCode = e.Version!.Instrument!.Code }).ToListAsync(ct);
        var latest = evaluations.GroupBy(e => e.TestCode).Select(g => g.First()).ToArray();
        var ids = latest.Select(e => e.Id).ToArray();
        var versions = latest.Select(e => e.VersionId).ToArray();
        var maxima = await db.HealthTestQuestions.AsNoTracking()
            .Where(q => versions.Contains(q.VersionId))
            .Select(q => new { q.VersionId, q.Section, Max = q.Options.Select(o => (decimal?)o.ScoreValue).Max() ?? 0 })
            .ToListAsync(ct);
        var rows = await db.HealthTestResults.AsNoTracking().Where(r => ids.Contains(r.EvaluationId))
            .OrderBy(r => r.EvaluationId).ThenBy(r => r.ResultType).ThenBy(r => r.Code)
            .Select(r => new { r.EvaluationId, r.ResultType, r.Code, r.Value, r.Severity, r.Qualifier }).ToListAsync(ct);
        var results = rows.Select(r =>
        {
            var evaluation = latest.Single(e => e.Id == r.EvaluationId);
            var maximum = maxima.Where(m => m.VersionId == evaluation.VersionId
                && (r.ResultType != HealthTestResultType.subscale || m.Section == r.Code)).Sum(m => m.Max);
            return new AssignmentResult(r.EvaluationId, evaluation.TestCode, r.ResultType.ToString(), r.Code,
                r.Value, r.Severity?.ToString(), r.Qualifier, maximum > 0 ? maximum : null, evaluation.CompletedAt);
        }).ToArray();
        var numeric = await db.HealthTestResponses.AsNoTracking()
            .Where(r => ids.Contains(r.EvaluationId) && r.Question!.Type == HealthTestQuestionType.num
                && r.Evaluation!.Version!.Instrument!.Code == "historia-clinica")
            .OrderBy(r => r.Question!.Code)
            .Select(r => new { r.EvaluationId, r.ValueText, r.Question!.Code, r.Question.Unit, r.Question.MinValue, r.Question.MaxValue })
            .ToListAsync(ct);
        var biometrics = numeric.Where(r => decimal.TryParse(r.ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            .Select(r => new AssignmentBiometric(r.EvaluationId, r.Code,
                decimal.Parse(r.ValueText!, NumberStyles.Float, CultureInfo.InvariantCulture), r.Unit, r.MinValue, r.MaxValue,
                latest.Single(e => e.Id == r.EvaluationId).CompletedAt)).ToArray();
        return new(patientId, batteryAssignmentId, patient.Gender, patient.Ethnicity, results, biometrics);
    }

    public async Task<AssignmentCatalog> ReadCatalogAsync(CancellationToken ct) => new(
        await db.ProgramConditions.AsNoTracking().OrderBy(c => c.Code).ToListAsync(ct),
        await db.ProgramAssignmentRules.AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.Code).ToListAsync(ct),
        await db.NutritionPlans.AsNoTracking().Where(p => p.IsTemplate && p.Status == CoppAddresd.Domain.Enums.NutritionPlanStatus.Active).OrderBy(p => p.Name).ThenBy(p => p.Id)
            .Select(p => new AssignmentContent(p.Id, p.Name, null)).ToListAsync(ct),
        await db.ExerciseRoutines.AsNoTracking().Where(r => r.Status == CoppAddresd.Domain.Enums.NutritionPlanStatus.Active).OrderBy(r => r.Name).ThenBy(r => r.Id)
            .Select(r => new AssignmentContent(r.Id, r.Name, (int?)r.Difficulty)).ToListAsync(ct));

    public async Task AddRecommendationAsync(ProgramAssignmentRecommendation recommendation, CancellationToken ct)
    {
        db.ProgramAssignmentRecommendations.Add(recommendation);
        await db.SaveChangesAsync(ct);
    }

    public Task<ProgramAssignmentRecommendation?> GetRecommendationAsync(Guid id, CancellationToken ct) =>
        db.ProgramAssignmentRecommendations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Guid>> ListMissingNarrativesAsync(DateTime cutoff, int limit, CancellationToken ct) =>
        await ProgramNarrativeEligibility.SelectPending(db.ProgramAssignmentRecommendations.AsNoTracking(), cutoff, limit)
            .ToListAsync(ct);

    public Task<bool> TryStartNarrativeAsync(Guid id, CancellationToken ct) => StartNarrativeAsync(id, false, ct);
    public Task<bool> TryStartNarrativeRegenerationAsync(Guid id, CancellationToken ct) => StartNarrativeAsync(id, true, ct);

    private async Task<bool> StartNarrativeAsync(Guid id, bool regenerate, CancellationToken ct)
    {
        var query = db.ProgramAssignmentRecommendations.Where(r => r.Id == id && r.State != "dismissed");
        if (!regenerate) query = query.Where(ProgramNarrativeEligibility.MissingNarrative);
        return await query.ExecuteUpdateAsync(s => s.SetProperty(r => r.State,
                r => r.State == "proposed" ? "pending-narrative" : r.State)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow), ct) > 0;
    }

    public async Task SaveNarrativeAsync(Guid id, string narrativeEs, string narrativeEn, string? modelName, CancellationToken ct)
    {
        // Una decisión clínica concurrente no se reabre al terminar la IA.
        await db.ProgramAssignmentRecommendations.Where(r => r.Id == id && r.State != "dismissed")
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.NarrativeEs, narrativeEs)
                .SetProperty(r => r.NarrativeEn, narrativeEn).SetProperty(r => r.ModelName, modelName)
                .SetProperty(r => r.State, r => r.State == "pending-narrative" ? "proposed" : r.State)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow), ct);
    }
}
