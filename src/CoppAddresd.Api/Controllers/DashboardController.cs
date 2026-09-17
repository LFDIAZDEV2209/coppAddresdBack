using System.Globalization;
using CoppAddresd.Api.Seeders;
using CoppAddresd.Application.Features.Dashboard;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Dashboard Home del ERP (datos agregados de plataforma).
/// Lee los rollups diarios de pre-agregación CQRS del <see cref="AppDbContext"/>
/// (pacientes, tests de salud, inventario y programa ANTARES) en O(1). Cuando el
/// rollup de un bloque no tiene filas (backfill pendiente / BD recién creada),
/// cae al conteo OLTP de la ventana con la misma semántica que el processor y el
/// backfill; nunca mezcla rollup + OLTP en la misma suma. Claves de rollup
/// verificadas contra los processors:
/// <list type="bullet">
/// <item><c>app.patient_daily_metrics</c>: "total_patients"/"new_patients" dimensión "general", fila global <see cref="Guid.Empty"/>.</item>
/// <item><c>app.health_test_daily_metrics</c>: "assignments_count" dimensión "completed", fila global <see cref="Guid.Empty"/>.</item>
/// <item><c>erp.inventory_daily_metrics</c>: "entries_count"/"exits_count" dimensión "total".</item>
/// <item><c>app.program_daily_metrics</c>: "tasks_completed_today" dimensión "general".</item>
/// </list>
/// </summary>
[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController(AppDbContext dbContext, MetricsBackfillSeeder backfillSeeder) : ControllerBase
{
    /// <summary>ClinicId de las filas globales consolidadas (vista Admin) en los rollups por clínica.</summary>
    private static readonly Guid GlobalId = Guid.Empty;

    /// <summary>
    /// KPIs + serie temporal del Home del ERP: total de pacientes de la plataforma y
    /// actividad de los últimos <paramref name="days"/> días (default 30, clamp 1..90)
    /// por módulo, leídos desde las tablas de pre-agregación.
    /// </summary>
    [HttpGet("kpis")]
    public async Task<ActionResult<DashboardKpisDto>> GetKpis(
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        // Ventana [from..to]: últimos N días calendario inclusive.
        days = Math.Clamp(days, 1, 90);
        var to = DateOnly.FromDateTime(DateTime.Today);
        var from = to.AddDays(-(days - 1));

        // ── Total de pacientes de la plataforma ────────────────────────────────
        // El rollup acumula +1 por alta en la fecha del evento y nunca decrementa.
        // Regla 8 CQRS: rollup-first con fallback OLTP si el rollup está vacío.
        var totalPatients = await dbContext.PatientDailyMetrics.AsNoTracking()
            .Where(m => m.ClinicId == GlobalId
                        && m.MetricKey == "total_patients"
                        && m.DimensionKey == "general"
                        && m.MetricDate <= to)
            .SumAsync(m => m.TotalCount, ct);

        if (totalPatients == 0)
        {
            var hasRollup = await dbContext.PatientDailyMetrics.AsNoTracking()
                .AnyAsync(m => m.MetricKey == "total_patients", ct);
            if (!hasRollup)
            {
                totalPatients = await dbContext.PatientProfiles.AsNoTracking().CountAsync(ct);
            }
        }

        // ── Ventanas diarias del período por rollup ────────────────────────────
        // Pacientes: altas del período (new_patients fila global).
        var patientDaily = await dbContext.PatientDailyMetrics.AsNoTracking()
            .Where(m => m.ClinicId == GlobalId
                        && m.MetricKey == "new_patients"
                        && m.DimensionKey == "general"
                        && m.MetricDate >= from && m.MetricDate <= to)
            .ToListAsync(ct);

        if (patientDaily.Count == 0)
        {
            var hasRollup = await dbContext.PatientDailyMetrics.AsNoTracking()
                .AnyAsync(m => m.MetricKey == "new_patients", ct);
            if (!hasRollup)
            {
                var fromDateTime = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var toDateTime = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

                var dbDaily = await dbContext.PatientProfiles.AsNoTracking()
                    .Where(p => p.CreatedAt >= fromDateTime && p.CreatedAt <= toDateTime)
                    .GroupBy(p => DateOnly.FromDateTime(p.CreatedAt))
                    .Select(g => new
                    {
                        MetricDate = g.Key,
                        TotalCount = (long)g.Count()
                    })
                    .ToListAsync(ct);

                patientDaily = dbDaily.Select(d => new PatientDailyMetric
                {
                    ClinicId = GlobalId,
                    MetricKey = "new_patients",
                    DimensionKey = "general",
                    MetricDate = d.MetricDate,
                    TotalCount = d.TotalCount,
                    LastUpdatedAt = DateTime.UtcNow
                }).ToList();
            }
        }

        // Tests de salud: asignaciones completadas en el período (fila global).
        var healthTestsDaily = await dbContext.HealthTestDailyMetrics.AsNoTracking()
            .Where(m => m.ClinicId == GlobalId
                        && m.MetricKey == "assignments_count"
                        && m.DimensionKey == "completed"
                        && m.MetricDate >= from && m.MetricDate <= to)
            .ToListAsync(ct);

        // Regla 8 CQRS: fallback OLTP si el rollup de tests de salud está vacío.
        if (healthTestsDaily.Count == 0)
        {
            var hasRollup = await dbContext.HealthTestDailyMetrics.AsNoTracking()
                .AnyAsync(m => m.MetricKey == "assignments_count", ct);
            if (!hasRollup)
            {
                // Misma semántica que el backfill: asignaciones en estado completed
                // fechadas por CompletedAt y, si no lo tienen, por AssignedAt.
                var fromDateTime = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var toDateTime = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

                // Se materializan solo los timestamps de la ventana y se agrupa en
                // memoria: GroupBy(DateOnly.FromDateTime(coalesce)) no traduce a SQL.
                var completionDates = await dbContext.HealthTestAssignments.AsNoTracking()
                    .Where(a => a.Status == HealthTestAssignmentStatus.completed
                                && (a.CompletedAt ?? a.AssignedAt) >= fromDateTime
                                && (a.CompletedAt ?? a.AssignedAt) <= toDateTime)
                    .Select(a => a.CompletedAt ?? a.AssignedAt)
                    .ToListAsync(ct);

                var dbDaily = completionDates
                    .GroupBy(d => DateOnly.FromDateTime(d))
                    .Select(g => new
                    {
                        MetricDate = g.Key,
                        TotalCount = (long)g.Count()
                    });

                healthTestsDaily = dbDaily.Select(d => new HealthTestDailyMetric
                {
                    ClinicId = GlobalId,
                    MetricKey = "assignments_count",
                    DimensionKey = "completed",
                    MetricDate = d.MetricDate,
                    TotalCount = d.TotalCount,
                    LastUpdatedAt = DateTime.UtcNow
                }).ToList();
            }
        }

        // Inventario: documentos de entrada/salida del período (dimensión "total").
        var inventoryDaily = await dbContext.InventoryDailyMetrics.AsNoTracking()
            .Where(m => (m.MetricKey == "entries_count" || m.MetricKey == "exits_count")
                        && m.DimensionKey == "total"
                        && m.MetricDate >= from && m.MetricDate <= to)
            .ToListAsync(ct);

        // Regla 8 CQRS: fallback OLTP si el rollup de inventario está vacío.
        if (inventoryDaily.Count == 0)
        {
            var hasRollup = await dbContext.InventoryDailyMetrics.AsNoTracking()
                .AnyAsync(m => m.MetricKey == "entries_count" || m.MetricKey == "exits_count", ct);
            if (!hasRollup)
            {
                // Misma semántica que el backfill: una fila por documento fechado
                // por erp.inventory_entries.date / erp.inventory_exits.date
                // (columna date: límites sin zona horaria, como el fallback del
                // repositorio de inventario).
                var fromDateTime = from.ToDateTime(TimeOnly.MinValue);
                var toExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

                var entryDates = await dbContext.InventoryEntries.AsNoTracking()
                    .Where(e => e.Date >= fromDateTime && e.Date < toExclusive)
                    .Select(e => e.Date)
                    .ToListAsync(ct);

                var exitDates = await dbContext.InventoryExits.AsNoTracking()
                    .Where(e => e.Date >= fromDateTime && e.Date < toExclusive)
                    .Select(e => e.Date)
                    .ToListAsync(ct);

                inventoryDaily = entryDates
                    .GroupBy(d => DateOnly.FromDateTime(d))
                    .Select(g => new InventoryDailyMetric
                    {
                        MetricDate = g.Key,
                        MetricKey = "entries_count",
                        DimensionKey = "total",
                        TotalCount = g.Count(),
                        LastUpdatedAt = DateTime.UtcNow
                    })
                    .Concat(exitDates
                        .GroupBy(d => DateOnly.FromDateTime(d))
                        .Select(g => new InventoryDailyMetric
                        {
                            MetricDate = g.Key,
                            MetricKey = "exits_count",
                            DimensionKey = "total",
                            TotalCount = g.Count(),
                            LastUpdatedAt = DateTime.UtcNow
                        }))
                    .ToList();
            }
        }

        // Programa ANTARES: tareas completadas del período (dimensión "general").
        var programDaily = await dbContext.ProgramDailyMetrics.AsNoTracking()
            .Where(m => m.MetricKey == "tasks_completed_today"
                        && m.DimensionKey == "general"
                        && m.MetricDate >= from && m.MetricDate <= to)
            .ToListAsync(ct);

        // Regla 8 CQRS: fallback OLTP si el rollup del programa está vacío.
        if (programDaily.Count == 0)
        {
            var hasRollup = await dbContext.ProgramDailyMetrics.AsNoTracking()
                .AnyAsync(m => m.MetricKey == "tasks_completed_today", ct);
            if (!hasRollup)
            {
                // Misma semántica que el backfill: una fila por task_completions
                // fechada por completed_at (reloj del servidor).
                var fromDateTime = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var toDateTime = to.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

                var completionDates = await dbContext.TaskCompletions.AsNoTracking()
                    .Where(t => t.CompletedAt >= fromDateTime && t.CompletedAt <= toDateTime)
                    .Select(t => t.CompletedAt)
                    .ToListAsync(ct);

                programDaily = completionDates
                    .GroupBy(d => DateOnly.FromDateTime(d))
                    .Select(g => new ProgramDailyMetric
                    {
                        MetricDate = g.Key,
                        MetricKey = "tasks_completed_today",
                        DimensionKey = "general",
                        TotalCount = g.Count(),
                        LastUpdatedAt = DateTime.UtcNow
                    })
                    .ToList();
            }
        }

        // ── Agregados del período (una fila por día y clave → suma directa) ────
        var newPatients30d = patientDaily.Sum(m => m.TotalCount);
        var healthTests30d = healthTestsDaily.Sum(m => m.TotalCount);
        var inventoryEntries30d = inventoryDaily
            .Where(m => m.MetricKey == "entries_count").Sum(m => m.TotalCount);
        var inventoryExits30d = inventoryDaily
            .Where(m => m.MetricKey == "exits_count").Sum(m => m.TotalCount);
        var programTasks30d = programDaily.Sum(m => m.TotalCount);

        // ── Serie temporal diaria [from..to] ────────────────────────────────────
        // Mapas día → conteo por módulo (PK del rollup garantiza una fila por día).
        var newPatientsByDay = patientDaily.ToDictionary(m => m.MetricDate, m => m.TotalCount);
        var testsByDay = healthTestsDaily.ToDictionary(m => m.MetricDate, m => m.TotalCount);
        var entriesByDay = inventoryDaily
            .Where(m => m.MetricKey == "entries_count")
            .ToDictionary(m => m.MetricDate, m => m.TotalCount);
        var exitsByDay = inventoryDaily
            .Where(m => m.MetricKey == "exits_count")
            .ToDictionary(m => m.MetricDate, m => m.TotalCount);
        var tasksByDay = programDaily.ToDictionary(m => m.MetricDate, m => m.TotalCount);

        // Etiqueta de fecha invariante "MMM d" (mismo formato que el fast path del
        // dashboard de inventario, pero con cultura fija para el contrato JSON).
        var rangeDays = to.DayNumber - from.DayNumber + 1;
        var series = Enumerable.Range(0, rangeDays).Select(i =>
        {
            var day = from.AddDays(i);
            return new DashboardActivityPoint(
                day.ToString("MMM d", CultureInfo.InvariantCulture),
                (int)newPatientsByDay.GetValueOrDefault(day),
                (int)testsByDay.GetValueOrDefault(day),
                (int)entriesByDay.GetValueOrDefault(day),
                (int)exitsByDay.GetValueOrDefault(day),
                (int)tasksByDay.GetValueOrDefault(day));
        }).ToList();

        return Ok(new DashboardKpisDto(
            (int)totalPatients,
            (int)newPatients30d,
            (int)healthTests30d,
            (int)inventoryEntries30d,
            (int)inventoryExits30d,
            (int)programTasks30d,
            series));
    }

    /// <summary>
    /// Reconciliación bajo demanda de todas las tablas de métricas CQRS (Backfill).
    /// Recalcula de forma idempotente los agregados diarios históricos desde las tablas OLTP.
    /// </summary>
    [HttpPost("maintenance/reconcile-metrics")]
    public async Task<IActionResult> ReconcileMetrics(CancellationToken ct = default)
    {
        var count = await backfillSeeder.RunBackfillAsync(ct);
        return Ok(new
        {
            message = "Reconciliación de tablas de métricas CQRS completada con éxito.",
            operations = count
        });
    }
}
