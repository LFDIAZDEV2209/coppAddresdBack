using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seeder de desarrollo para el dashboard de Tests de Salud. Genera pacientes
/// evaluados (asignaciones completadas + evaluaciones + resultados de score +
/// alertas activas) y pacientes pendientes repartidos por estados de EE. UU.,
/// con severidades y fechas distribuidas en los últimos 12 meses para que el
/// mapa de calor, las gráficas de cobertura/riesgo y los KPIs tengan datos
/// realistas. Algunos estados quedan intencionalmente sin evaluaciones para
/// que el mapa los muestre en gris ("Sin datos").
///
/// Idempotencia: si ya existe cualquier asignación de test, no vuelve a
/// ejecutarse. Se registra solo en Development (ver Program.cs).
/// </summary>
public sealed class HealthTestsDemoSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<HealthTestsDemoSeeder> logger) : IHostedService
{
    private sealed record StateSeedDef(string StateCode, int Evaluated, int HighPercent, int Pending);

    private static readonly IReadOnlyList<StateSeedDef> StateSeeds =
    [
        // Estados con evaluaciones (código, evaluados, % alto/crítico objetivo, pendientes).
        new("FL", 20, 60, 4),
        new("NY", 18, 45, 3),
        new("TX", 16, 32, 3),
        new("CA", 15, 12, 4),
        new("MS", 10, 72, 2),
        new("AL", 9, 65, 2),
        new("LA", 9, 58, 2),
        new("WV", 8, 68, 2),
        new("AR", 7, 55, 2),
        new("NM", 6, 48, 2),
        new("KY", 8, 42, 2),
        new("MO", 8, 40, 2),
        new("KS", 6, 45, 2),
        new("OK", 6, 38, 2),
        new("AZ", 8, 35, 2),
        new("TN", 9, 32, 3),
        new("GA", 10, 28, 3),
        new("NC", 10, 30, 3),
        new("SC", 6, 34, 2),
        new("OH", 11, 22, 3),
        new("PA", 11, 20, 3),
        new("MI", 10, 25, 3),
        new("IL", 10, 42, 3),
        new("IN", 7, 24, 2),
        new("WI", 6, 28, 2),
        new("MN", 6, 12, 2),
        new("IA", 5, 20, 2),
        new("NE", 5, 34, 2),
        new("NV", 6, 30, 2),
        new("CO", 9, 10, 2),
        new("UT", 5, 11, 2),
        new("WA", 8, 16, 2),
        new("OR", 6, 14, 2),
        new("CT", 5, 16, 2),
        new("NJ", 8, 18, 2),
        new("MD", 6, 17, 2),
        new("VA", 7, 22, 2),
        new("MA", 7, 14, 2),
        new("HI", 4, 20, 1),
        new("ID", 4, 16, 1),
        // Estados sin evaluaciones: permanecen grises en el mapa ("Sin datos").
        new("AK", 0, 0, 3),
        new("ND", 0, 0, 2),
        new("SD", 0, 0, 2),
        new("MT", 0, 0, 2),
        new("WY", 0, 0, 2),
        new("VT", 0, 0, 2),
        new("NH", 0, 0, 2),
        new("ME", 0, 0, 2),
        new("DE", 0, 0, 2),
        new("RI", 0, 0, 2),
    ];

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Seed demo de Tests de Salud cancelado.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falló el seed demo de Tests de Salud.");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.HealthTestAssignments.AnyAsync(ct))
        {
            logger.LogInformation("Tests de Salud: ya existen asignaciones; se omite el seed demo.");
            return;
        }

        var versionIds = await db.HealthTestVersions
            .AsNoTracking()
            .Where(v => v.Status == HealthTestVersionStatus.active)
            .OrderBy(v => v.VersionNumber)
            .ThenBy(v => v.Id)
            .Select(v => v.Id)
            .ToListAsync(ct);

        if (versionIds.Count == 0)
        {
            logger.LogWarning("Tests de Salud: no hay versiones activas; se omite el seed demo.");
            return;
        }

        var now = DateTime.UtcNow;
        var assignments = new List<HealthTestAssignment>();
        var evaluations = new List<HealthTestEvaluation>();
        var results = new List<HealthTestResult>();
        var alerts = new List<HealthTestAlert>();

        var evaluatedCount = 0;
        var pendingCount = 0;
        var alertCount = 0;

        for (var stateIndex = 0; stateIndex < StateSeeds.Count; stateIndex++)
        {
            var state = StateSeeds[stateIndex];

            var patientIds = await db.PatientProfiles
                .AsNoTracking()
                .Where(p => p.DeletedAt == null && p.CityId != null && p.City!.State!.Code == state.StateCode)
                .OrderBy(p => p.Id)
                .Select(p => p.Id)
                .Take(state.Evaluated + state.Pending)
                .ToListAsync(ct);

            for (var i = 0; i < patientIds.Count; i++)
            {
                var patientId = patientIds[i];

                if (i >= state.Evaluated)
                {
                    // Paciente mapeado con test asignado pero sin evaluar (KPI pendientes).
                    assignments.Add(new HealthTestAssignment
                    {
                        Id = Guid.NewGuid(),
                        PatientId = patientId,
                        VersionId = versionIds[(stateIndex + i) % versionIds.Count],
                        Status = HealthTestAssignmentStatus.pending,
                        Priority = 2 + i % 3,
                        AssignedAt = now.AddDays(-(3 + (i * 3 + stateIndex) % 21)),
                        DueDate = now.AddDays(4 + (i * 2 + stateIndex) % 14),
                    });
                    pendingCount++;
                    continue;
                }

                var severity = PickSeverity(state.HighPercent, stateIndex, i);
                var (value, qualifier, score) = ScoreFor(severity);

                // Distribución en ~12 meses (patrón determinista, sin azar).
                var daysAgo = 7 + (i * 11 + stateIndex * 5) % 350;
                var completedAt = now.AddDays(-daysAgo);
                var startedAt = completedAt.AddMinutes(-25);

                var versionId = versionIds[(stateIndex + i) % versionIds.Count];
                AddCompletedTest(
                    patientId, versionId, severity, value, qualifier, score,
                    startedAt, completedAt, sendAlert: severity >= HealthTestSeverity.high && (severity == HealthTestSeverity.critical || i % 3 == 0));

                // ~1 de cada 3 pacientes evaluados completó un segundo instrumento.
                if (i % 3 == 0)
                {
                    var secondVersionId = versionIds[(stateIndex + i + 4) % versionIds.Count];
                    var secondCompletedAt = completedAt.AddDays(-(5 + i % 9));
                    AddCompletedTest(
                        patientId, secondVersionId, severity, value, qualifier, score,
                        secondCompletedAt.AddMinutes(-20), secondCompletedAt, sendAlert: false,
                        secondTest: true);
                }

                evaluatedCount++;
            }
        }

        // Inserciones por niveles para respetar las FKs sin depender del orden del batch.
        db.HealthTestAssignments.AddRange(assignments);
        await db.SaveChangesAsync(ct);

        db.HealthTestEvaluations.AddRange(evaluations);
        await db.SaveChangesAsync(ct);

        db.HealthTestResults.AddRange(results);
        await db.SaveChangesAsync(ct);

        db.HealthTestAlerts.AddRange(alerts);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Seed demo de Tests de Salud completado: {Evaluated} pacientes evaluados, {Pending} pendientes, {Alerts} alertas activas.",
            evaluatedCount, pendingCount, alertCount);

        void AddCompletedTest(
            Guid patientId,
            Guid versionId,
            HealthTestSeverity severity,
            decimal value,
            string qualifier,
            decimal score,
            DateTime startedAt,
            DateTime completedAt,
            bool sendAlert,
            bool secondTest = false)
        {
            var assignment = new HealthTestAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                VersionId = versionId,
                Status = HealthTestAssignmentStatus.completed,
                Priority = severity >= HealthTestSeverity.high ? 1 : 3,
                AssignedAt = startedAt.AddDays(-2),
                StartedAt = startedAt,
                CompletedAt = completedAt,
            };
            assignments.Add(assignment);

            var evaluation = new HealthTestEvaluation
            {
                Id = Guid.NewGuid(),
                AssignmentId = assignment.Id,
                PatientId = patientId,
                VersionId = versionId,
                Status = HealthTestEvaluationStatus.completed,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Score = score,
                ScorePercentage = score,
            };
            evaluations.Add(evaluation);

            var result = new HealthTestResult
            {
                Id = Guid.NewGuid(),
                EvaluationId = evaluation.Id,
                ResultType = HealthTestResultType.score,
                Code = secondTest ? "score_total_b" : "score_total",
                Label = "Score total",
                Value = value,
                Qualifier = qualifier,
                Severity = severity,
                CreatedAt = completedAt,
            };
            results.Add(result);

            if (!sendAlert)
            {
                return;
            }

            alerts.Add(new HealthTestAlert
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                ResultId = result.Id,
                Severity = severity,
                Title = severity == HealthTestSeverity.critical
                    ? "Riesgo crítico en evaluación de salud"
                    : "Riesgo alto en evaluación de salud",
                Body = "El resultado del test supera el umbral configurado; requiere revisión clínica.",
                Status = HealthTestAlertStatus.active,
                CreatedAt = completedAt.AddHours(1),
            });
            alertCount++;
        }
    }

    private static HealthTestSeverity PickSeverity(int highPercent, int stateIndex, int index)
    {
        var isHigh = (index * 37 + stateIndex * 13) % 100 < highPercent;
        if (!isHigh)
        {
            return index % 4 == 0 ? HealthTestSeverity.moderate : HealthTestSeverity.low;
        }

        return index % 10 < 3 ? HealthTestSeverity.critical : HealthTestSeverity.high;
    }

    private static (decimal Value, string Qualifier, decimal Score) ScoreFor(HealthTestSeverity severity) =>
        severity switch
        {
            HealthTestSeverity.critical => (9.6m, "crítico", 96m),
            HealthTestSeverity.high => (7.8m, "alto", 78m),
            HealthTestSeverity.moderate => (5.5m, "moderado", 55m),
            _ => (3.1m, "bajo", 31m),
        };
}
