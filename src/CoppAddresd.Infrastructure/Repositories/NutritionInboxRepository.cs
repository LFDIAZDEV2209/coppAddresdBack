using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>Bandeja por paciente: alcance antes de filtros, conteo y página, sin cargar agregados ni hacer N+1.</summary>
public sealed class NutritionInboxRepository(AppDbContext db) : INutritionInboxRepository
{
    private const string Source = """
        WITH visible AS (
            SELECT p.id AS "PatientId", (p.first_name || ' ' || p.last_name) AS "PatientName",
                n.id AS "PrescriptionId", n.version AS "Version", n.revision AS "Revision",
                COALESCE(n.provisional, false) AS "Provisional", n.calorie_target AS "K",
                n.start_date AS "StartDate", n.end_date AS "EndDate",
                COALESCE(n.is_published, false) AS "IsPublished",
                n.clinical_profile_json::text AS "ClinicalProfileJson", n.targets_json::text AS "TargetsJson",
                n.created_at AS "CreatedAt",
                CASE WHEN n.id IS NULL THEN 'sin_plan'
                     WHEN n.state = 'aprobado' AND n.is_published
                        AND n.end_date <= (@now AT TIME ZONE p.timezone)::date + 5 THEN 'por_renovar'
                     ELSE n.state END AS "State"
            FROM app.patient_profiles p
            LEFT JOIN LATERAL (
                SELECT r.id, r.version, r.revision, r.provisional, r.calorie_target, r.start_date, r.end_date,
                    r.is_published, r.clinical_profile_json, r.targets_json, r.created_at, r.state
                FROM app.nutrition_prescriptions r WHERE r.patient_id = p.id
                ORDER BY r.created_at DESC, r.id DESC LIMIT 1
            ) n ON true
            WHERE p.deleted_at IS NULL
                AND (@global OR EXISTS (SELECT 1 FROM app.patient_professionals pp
                    WHERE pp.patient_id = p.id AND pp.professional_id = @professional AND pp.status = 'Active'))
        ), filtered AS (
            SELECT * FROM visible WHERE (@state IS NULL OR "State" = @state)
                AND (@search IS NULL OR "PatientName" ILIKE @search ESCAPE '\')
        )
        """;

    public async Task<NutritionInboxData> ListAsync(NutritionActor actor, int page, int pageSize,
        string? state, string? search, DateTime utcNow, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Los comodines del usuario son texto literal, nunca fragmentos SQL ni patrones abiertos.
        var pattern = search is null ? null : "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        NpgsqlParameter[] Parameters() => [
            new("global", actor.ViewAll),
            new("professional", NpgsqlDbType.Uuid) { Value = (object?)actor.ProfessionalId ?? DBNull.Value },
            new("state", NpgsqlDbType.Text) { Value = (object?)state ?? DBNull.Value },
            new("search", NpgsqlDbType.Text) { Value = (object?)pattern ?? DBNull.Value },
            new("now", utcNow)];
        try
        {
            var total = await db.Database.SqlQueryRaw<int>(Source + " SELECT count(*)::int AS \"Value\" FROM filtered", Parameters()).SingleAsync(ct);
            var parameters = Parameters().Concat(new NpgsqlParameter[] {
                new("limit", pageSize), new("offset", ((long)page - 1) * pageSize) }).ToArray();
            var items = await db.Database.SqlQueryRaw<NutritionInboxRow>(Source + """
                SELECT "PatientId", "PatientName", "State", "PrescriptionId", "Version", "Revision", "Provisional",
                    "K", "StartDate", "EndDate", "IsPublished", "ClinicalProfileJson", "TargetsJson"
                FROM filtered
                ORDER BY ("PrescriptionId" IS NULL) DESC,
                    CASE WHEN "PrescriptionId" IS NULL THEN "PatientName" END ASC,
                    "CreatedAt" DESC NULLS LAST, "PatientId" ASC
                LIMIT @limit OFFSET @offset
                """, parameters).ToListAsync(ct);
            return new(items, total);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.InvalidParameterValue)
        {
            throw new UnprocessableEntityException("La zona horaria del paciente no es válida.");
        }
    }
}
