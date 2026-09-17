using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Consultas agregadas del dashboard general de pacientes y del tablero
/// clínico. Todo se resuelve con agregados SQL y consultas bulk para la página
/// (nunca una consulta por fila); el alcance (clínica activa + propio vs
/// global) replica exactamente las fronteras de <see cref="PatientRepository"/>.
/// </summary>
public sealed class PatientDashboardRepository(AppDbContext dbContext, IPatientRepository patients)
    : IPatientDashboardRepository
{
    public async Task<PatientDashboardDto> GetDashboardAsync(
        Guid? clinicId,
        Guid? professionalId,
        string? stateCode,
        int months,
        DateTime nowUtc,
        CancellationToken ct = default
    )
    {
        // KPIs: agregado live sobre el mismo alcance que el resto del dashboard
        // (las gráficas también son live). No se usa la pre-agregación
        // patient_daily_metrics para evitar derivas visibles (p. ej. Activos >
        // Total) entre tarjetas y gráficas; el endpoint /stats conserva su
        // fuente histórica.
        var monthStartUtc = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Base scoped completa y base acotada al estado seleccionado (si lo hay).
        var scoped = ScopedPatients(clinicId, professionalId);
        var filtered = string.IsNullOrWhiteSpace(stateCode)
            ? scoped
            : scoped.Where(x => x.State != null && x.State.Code == stateCode);

        var kpis =
            await scoped
                .GroupBy(x => 1)
                .Select(g => new PatientStatsDto(
                    Total: g.Count(),
                    Active: g.Count(x => x.Status == "Activo"),
                    NewThisMonth: g.Count(x => x.CreatedAt >= monthStartUtc),
                    WithoutProfessional: g.Count(x => !x.Assignments.Any(a => a.Status == "Active"))
                ))
                .FirstOrDefaultAsync(ct)
            ?? new PatientStatsDto(0, 0, 0, 0);

        var genderRaw = await filtered
            .GroupBy(x => x.Gender)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);
        var gender = genderRaw
            .Select(x => new PatientDashboardSlice(x.Value ?? "Sin dato", x.Count))
            .ToList();

        var ageRaw = await filtered
            .Where(x => x.DateOfBirth != null)
            .GroupBy(x => nowUtc.Year - x.DateOfBirth!.Value.Year)
            .Select(g => new { Age = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var age = ageRaw
            .GroupBy(x => AgeBucket(x.Age))
            .Select(g => new PatientDashboardSlice(g.Key, g.Sum(x => x.Count)))
            .OrderBy(x => AgeBucketOrder(x.Value))
            .ToList();

        var maritalRaw = await filtered
            .GroupBy(x => x.MaritalStatus)
            .Select(g => new { Value = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);
        var marital = maritalRaw
            .Select(x => new PatientDashboardSlice(x.Value ?? "Sin dato", x.Count))
            .ToList();

        var insurerRaw = await filtered
            .GroupBy(x => x.InsurerId)
            .Select(g => new { InsurerId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(6)
            .ToListAsync(ct);
        var insurerIds = insurerRaw
            .Where(x => x.InsurerId != null)
            .Select(x => x.InsurerId!.Value)
            .ToList();
        var insurerNames =
            insurerIds.Count > 0
                ? await dbContext
                    .Insurers.AsNoTracking()
                    .Where(i => insurerIds.Contains(i.Id))
                    .ToDictionaryAsync(i => i.Id, i => i.Name, ct)
                : new Dictionary<Guid, string>();
        var insurers = insurerRaw
            .Select(x => new PatientDashboardNamedSlice(
                x.InsurerId,
                x.InsurerId is { } id
                    ? insurerNames.GetValueOrDefault(id, "Sin aseguradora")
                    : "Sin aseguradora",
                x.Count
            ))
            .ToList();

        var topDiagnosesRaw = await (
            from d in dbContext.PatientDiagnoses.AsNoTracking()
            join p in filtered on d.PatientId equals p.Id
            join c in dbContext.Icd10Codes.AsNoTracking() on d.Icd10CodeId equals c.Id
            group d by new { c.Code, c.Description } into g
            orderby g.Count() descending
            select new
            {
                g.Key.Code,
                g.Key.Description,
                Count = g.Count(),
            }
        )
            .Take(5)
            .ToListAsync(ct);
        var topDiagnoses = topDiagnosesRaw
            .Select(x => new PatientDashboardDiagnosisSlice(x.Code, x.Description, x.Count))
            .ToList();

        var startMonth = new DateTime(
            nowUtc.Year,
            nowUtc.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc
        ).AddMonths(-(months - 1));
        var growthRaw = await filtered
            .Where(x => x.CreatedAt >= startMonth)
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                Count = g.Count(),
            })
            .ToListAsync(ct);
        var growthLookup = growthRaw.ToDictionary(x => (x.Year, x.Month), x => x.Count);
        var growth = Enumerable
            .Range(0, months)
            .Select(offset =>
            {
                var month = startMonth.AddMonths(offset);
                return new PatientDashboardMonthSlice(
                    month.Year,
                    month.Month,
                    growthLookup.GetValueOrDefault((month.Year, month.Month))
                );
            })
            .ToList();

        // Geografía: SIEMPRE el alcance completo (sin filtro de estado) para
        // permitir cambiar la selección sin perder el contexto del mapa.
        var geoRaw = await (
            from p in scoped
            join s in dbContext.States.AsNoTracking() on p.StateId equals (Guid?)s.Id
            group p by new { s.Code, s.Name } into g
            orderby g.Count() descending
            select new
            {
                g.Key.Code,
                g.Key.Name,
                Count = g.Count(),
            }
        ).ToListAsync(ct);
        var totalWithState = geoRaw.Sum(x => x.Count);
        var states = geoRaw
            .Select(x => new PatientDashboardStateSlice(
                x.Code,
                x.Name,
                x.Count,
                totalWithState > 0 ? Math.Round(x.Count * 100m / totalWithState, 1) : 0m
            ))
            .ToList();

        // Top de profesionales: solo alcance global (con alcance propio todos
        // los pacientes son del mismo profesional — sería redundante).
        var topProfessionals = new List<PatientDashboardProfessionalSlice>();
        if (professionalId is null)
        {
            var prosRaw = await (
                from a in dbContext.PatientProfessionalAssignments.AsNoTracking()
                join p in filtered on a.PatientId equals p.Id
                where a.Status == "Active"
                group a by a.ProfessionalId into g
                orderby g.Count() descending
                select new { ProfessionalId = g.Key, Count = g.Count() }
            )
                .Take(6)
                .ToListAsync(ct);

            if (prosRaw.Count > 0)
            {
                var names = await patients.GetProfessionalNamesAsync(
                    prosRaw.Select(x => x.ProfessionalId).ToList(),
                    ct
                );
                topProfessionals = prosRaw
                    .Select(x => new PatientDashboardProfessionalSlice(
                        x.ProfessionalId,
                        names.GetValueOrDefault(x.ProfessionalId, "Profesional"),
                        x.Count
                    ))
                    .ToList();
            }
        }

        var contactRaw = await filtered
            .GroupBy(x => 1)
            .Select(g => new
            {
                Total = g.Count(),
                WithContact = g.Count(x => x.EmergencyContact != null && x.EmergencyContact != ""),
            })
            .FirstOrDefaultAsync(ct);
        var emergencyPct = contactRaw is { Total: > 0 }
            ? Math.Round(contactRaw.WithContact * 100m / contactRaw.Total, 1)
            : 0m;

        return new PatientDashboardDto(
            kpis,
            gender,
            age,
            marital,
            insurers,
            topDiagnoses,
            growth,
            states,
            topProfessionals,
            emergencyPct
        );
    }

    public async Task<(IReadOnlyList<ClinicalBoardItemDto> Items, int Total)> GetClinicalBoardAsync(
        int page,
        int pageSize,
        string? search,
        string? risk,
        bool? hasAlerts,
        string? followUp,
        Guid? clinicId,
        Guid? professionalId,
        DateTime nowUtc,
        CancellationToken ct = default
    )
    {
        var query = ScopedPatients(clinicId, professionalId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.FirstName, pattern)
                || EF.Functions.ILike(x.LastName, pattern)
                || EF.Functions.ILike(x.MedicalRecordNumber ?? string.Empty, pattern)
                || EF.Functions.ILike(x.DocumentNumber ?? string.Empty, pattern)
            );
        }

        // Proyección con señales por paciente (subconsultas correlacionadas:
        // "última evaluación", "mínima fecha pendiente" y alertas activas), sin
        // una consulta por fila. Los filtros se componen sobre la proyección
        // para que la paginación siga siendo server-side y estable.
        var projected = query.Select(x => new
        {
            PatientId = x.Id,
            x.MedicalRecordNumber,
            x.FirstName,
            x.LastName,
            x.DocumentNumber,
            LastSeverity = dbContext
                .HealthTestResults.AsNoTracking()
                .Where(r =>
                    r.Evaluation!.PatientId == x.Id
                    && r.Evaluation.Status == HealthTestEvaluationStatus.completed
                    && r.ResultType == HealthTestResultType.score
                )
                .OrderByDescending(r => r.Evaluation!.CompletedAt)
                .Select(r => r.Severity)
                .FirstOrDefault(),
            LastPct = dbContext
                .HealthTestEvaluations.AsNoTracking()
                .Where(e => e.PatientId == x.Id && e.Status == HealthTestEvaluationStatus.completed)
                .OrderByDescending(e => e.CompletedAt)
                .Select(e => e.ScorePercentage)
                .FirstOrDefault(),
            LastEvalAt = dbContext
                .HealthTestEvaluations.AsNoTracking()
                .Where(e => e.PatientId == x.Id && e.Status == HealthTestEvaluationStatus.completed)
                .Max(e => (DateTime?)e.CompletedAt),
            LastVersionId = dbContext
                .HealthTestEvaluations.AsNoTracking()
                .Where(e => e.PatientId == x.Id && e.Status == HealthTestEvaluationStatus.completed)
                .OrderByDescending(e => e.CompletedAt)
                .Select(e => (Guid?)e.VersionId)
                .FirstOrDefault(),
            PendingCount = dbContext
                .HealthTestAssignments.AsNoTracking()
                .Count(a =>
                    a.PatientId == x.Id
                    && (
                        a.Status == HealthTestAssignmentStatus.pending
                        || a.Status == HealthTestAssignmentStatus.in_progress
                    )
                ),
            NextDue = dbContext
                .HealthTestAssignments.AsNoTracking()
                .Where(a =>
                    a.PatientId == x.Id
                    && (
                        a.Status == HealthTestAssignmentStatus.pending
                        || a.Status == HealthTestAssignmentStatus.in_progress
                    )
                )
                .Min(a => (DateTime?)a.DueDate),
            ActiveAlerts = dbContext
                .HealthTestAlerts.AsNoTracking()
                .Count(a => a.PatientId == x.Id && a.Status == HealthTestAlertStatus.active),
        });

        if (string.Equals(risk, ClinicalBoardFilters.RiskHigh, StringComparison.OrdinalIgnoreCase))
        {
            projected = projected.Where(p =>
                p.LastSeverity == HealthTestSeverity.high
                || p.LastSeverity == HealthTestSeverity.critical
                || (p.LastSeverity == null && p.LastPct != null && p.LastPct >= 70)
            );
        }
        else if (
            string.Equals(
                risk,
                ClinicalBoardFilters.RiskModerate,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            projected = projected.Where(p =>
                p.LastSeverity == HealthTestSeverity.moderate
                || (
                    p.LastSeverity == null && p.LastPct != null && p.LastPct >= 40 && p.LastPct < 70
                )
            );
        }
        else if (
            string.Equals(risk, ClinicalBoardFilters.RiskLow, StringComparison.OrdinalIgnoreCase)
        )
        {
            projected = projected.Where(p =>
                p.LastSeverity == HealthTestSeverity.low
                || (p.LastSeverity == null && p.LastPct != null && p.LastPct < 40)
            );
        }

        if (hasAlerts is true)
        {
            projected = projected.Where(p => p.ActiveAlerts > 0);
        }

        if (
            string.Equals(
                followUp,
                ClinicalBoardFilters.FollowUpOverdue,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            projected = projected.Where(p =>
                p.PendingCount > 0 && p.NextDue != null && p.NextDue < nowUtc
            );
        }
        else if (
            string.Equals(
                followUp,
                ClinicalBoardFilters.FollowUpOnTrack,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            projected = projected.Where(p =>
                p.PendingCount > 0 && (p.NextDue == null || p.NextDue >= nowUtc)
            );
        }
        else if (
            string.Equals(
                followUp,
                ClinicalBoardFilters.FollowUpUnassigned,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            projected = projected.Where(p => p.PendingCount == 0);
        }

        var total = await projected.CountAsync(ct);

        var rows = await projected
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .ThenByDescending(p => p.PatientId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var patientIds = rows.Select(r => r.PatientId).ToList();
        var versionIds = rows.Where(r => r.LastVersionId != null)
            .Select(r => r.LastVersionId!.Value)
            .Distinct()
            .ToList();

        var instrumentNames =
            versionIds.Count > 0
                ? await dbContext
                    .HealthTestVersions.AsNoTracking()
                    .Where(v => versionIds.Contains(v.Id))
                    .Select(v => new
                    {
                        v.Id,
                        Name = v.Name != null
                            ? v.Name
                            : (v.Instrument != null ? v.Instrument.Name : null),
                    })
                    .ToDictionaryAsync(v => v.Id, v => v.Name, ct)
                : new Dictionary<Guid, string?>();

        // Severidad máxima por paciente de la página: MAX sobre el enum con
        // conversión a texto ordena alfabéticamente (incorrecto), así que el
        // bucket se resuelve con conteos por severidad en una sola consulta.
        var alertAgg =
            patientIds.Count > 0
                ? await dbContext
                    .HealthTestAlerts.AsNoTracking()
                    .Where(a =>
                        patientIds.Contains(a.PatientId) && a.Status == HealthTestAlertStatus.active
                    )
                    .GroupBy(a => a.PatientId)
                    .Select(g => new
                    {
                        PatientId = g.Key,
                        Critical = g.Count(a => a.Severity == HealthTestSeverity.critical),
                        High = g.Count(a => a.Severity == HealthTestSeverity.high),
                        Moderate = g.Count(a => a.Severity == HealthTestSeverity.moderate),
                        Low = g.Count(a => a.Severity == HealthTestSeverity.low),
                    })
                    .ToListAsync(ct)
                : [];
        var alertLookup = alertAgg.ToDictionary(a => a.PatientId);

        var items = rows.Select(r =>
            {
                alertLookup.TryGetValue(r.PatientId, out var alerts);
                var maxSeverity = alerts switch
                {
                    { Critical: > 0 } => "critical",
                    { High: > 0 } => "high",
                    { Moderate: > 0 } => "moderate",
                    { Low: > 0 } => "low",
                    _ => null,
                };

                return new ClinicalBoardItemDto(
                    r.PatientId,
                    r.MedicalRecordNumber,
                    r.FirstName,
                    r.LastName,
                    r.DocumentNumber,
                    ResolveRisk(r.LastSeverity, r.LastPct),
                    r.LastEvalAt,
                    r.LastVersionId is { } versionId
                        ? instrumentNames.GetValueOrDefault(versionId)
                        : null,
                    alerts is { } a ? a.Critical + a.High + a.Moderate + a.Low : 0,
                    maxSeverity,
                    r.NextDue,
                    ResolveFollowUp(r.PendingCount, r.NextDue, nowUtc)
                );
            })
            .ToList();

        return (items, total);
    }

    /// <summary>Pacientes no eliminados con la frontera de datos del módulo.</summary>
    private IQueryable<PatientProfile> ScopedPatients(Guid? clinicId, Guid? professionalId)
    {
        var query = dbContext.PatientProfiles.AsNoTracking().Where(x => x.DeletedAt == null);

        if (clinicId is not null)
            query = query.Where(x => x.ClinicId == clinicId);

        if (professionalId is not null)
            query = query.Where(x =>
                x.Assignments.Any(a => a.ProfessionalId == professionalId && a.Status == "Active")
            );

        return query;
    }

    private static string? ResolveRisk(HealthTestSeverity? severity, decimal? scorePercentage)
    {
        if (severity is not null)
        {
            return severity.Value switch
            {
                HealthTestSeverity.critical => "critical",
                HealthTestSeverity.high => "high",
                HealthTestSeverity.moderate => "moderate",
                HealthTestSeverity.low => "low",
                _ => null,
            };
        }

        if (scorePercentage is null)
            return null;

        if (scorePercentage >= 70)
            return "high";
        if (scorePercentage >= 40)
            return "moderate";
        return "low";
    }

    private static string ResolveFollowUp(int pendingCount, DateTime? nextDue, DateTime nowUtc)
    {
        if (pendingCount == 0)
            return ClinicalBoardFollowUp.Unassigned;

        return nextDue != null && nextDue < nowUtc
            ? ClinicalBoardFollowUp.Overdue
            : ClinicalBoardFollowUp.OnTrack;
    }

    private static string AgeBucket(int age) =>
        age switch
        {
            < 18 => "0-17",
            <= 30 => "18-30",
            <= 45 => "31-45",
            <= 55 => "46-55",
            <= 65 => "56-65",
            _ => "65+",
        };

    private static int AgeBucketOrder(string? bucket) =>
        bucket switch
        {
            "0-17" => 0,
            "18-30" => 1,
            "31-45" => 2,
            "46-55" => 3,
            "56-65" => 4,
            _ => 5,
        };
}
