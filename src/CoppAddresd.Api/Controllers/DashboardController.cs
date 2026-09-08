using System.Globalization;
using CoppAddresd.Application.Features.Dashboard;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Dashboard Home del ERP (datos agregados de plataforma).
/// Lee exclusivamente los rollups diarios de pre-agregación CQRS del <see cref="AppDbContext"/>
/// (pacientes, tests de salud, inventario y programa ANTARES) en O(1), sin escanear las
/// tablas transaccionales. Claves de rollup verificadas contra los processors:
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
public class DashboardController(AppDbContext dbContext) : ControllerBase
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
        // El rollup acumula +1 por alta en la fecha del evento y nunca decrementa
        // (los soft deletes no tocan total_patients): la suma histórica hasta hoy
        // equivale al total de pacientes registrados (misma semántica que el stats
        // del módulo de pacientes, que suma sin límite inferior de fecha).
        var totalPatients = await dbContext.PatientDailyMetrics.AsNoTracking()
            .Where(m => m.ClinicId == GlobalId
                        && m.MetricKey == "total_patients"
                        && m.DimensionKey == "general"
                        && m.MetricDate <= to)
            .SumAsync(m => m.TotalCount, ct);

        // ── Ventanas diarias del período por rollup ────────────────────────────
        // Pacientes: altas del período (nueva_patients fila global).
        var patientDaily = await dbContext.PatientDailyMetrics.AsNoTracking()
            .Where(m => m.ClinicId == GlobalId
                        && m.MetricKey == "new_patients"
                        && m.DimensionKey == "general"
                        && m.MetricDate >= from && m.MetricDate <= to)
            .ToListAsync(ct);

        // Tests de salud: asignaciones completadas en el período (fila global).
        var healthTestsDaily = await dbContext.HealthTestDailyMetrics.AsNoTracking()
            .Where(m => m.ClinicId == GlobalId
                        && m.MetricKey == "assignments_count"
                        && m.DimensionKey == "completed"
                        && m.MetricDate >= from && m.MetricDate <= to)
            .ToListAsync(ct);

        // Inventario: documentos de entrada/salida del período (dimensión "total").
        var inventoryDaily = await dbContext.InventoryDailyMetrics.AsNoTracking()
            .Where(m => (m.MetricKey == "entries_count" || m.MetricKey == "exits_count")
                        && m.DimensionKey == "total"
                        && m.MetricDate >= from && m.MetricDate <= to)
            .ToListAsync(ct);

        // Programa ANTARES: tareas completadas del período (dimensión "general").
        var programDaily = await dbContext.ProgramDailyMetrics.AsNoTracking()
            .Where(m => m.MetricKey == "tasks_completed_today"
                        && m.DimensionKey == "general"
                        && m.MetricDate >= from && m.MetricDate <= to)
            .ToListAsync(ct);

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
}
