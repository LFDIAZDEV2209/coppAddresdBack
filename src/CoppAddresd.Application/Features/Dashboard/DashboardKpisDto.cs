namespace CoppAddresd.Application.Features.Dashboard;

/// <summary>
/// KPIs del dashboard Home del ERP (datos agregados de plataforma).
/// Se leen exclusivamente desde las tablas de pre-agregación CQRS (rollups diarios
/// del <c>AppDbContext</c>), nunca desde las tablas transaccionales (OLTP).
/// </summary>
/// <param name="TotalPatients">
/// Total de pacientes registrados en la plataforma. El rollup de pacientes acumula +1
/// por alta (nunca decrementa), por lo que la suma histórica de
/// <c>total_patients</c>/<c>general</c> (fila global <see cref="Guid.Empty"/>) hasta hoy
/// es el total de la plataforma.
/// </param>
/// <param name="NewPatients30d">Pacientes nuevos registrados en la ventana consultada (últimos N días).</param>
/// <param name="HealthTests30d">Tests de salud completados en la ventana (<c>assignments_count</c> dimensión <c>completed</c>).</param>
/// <param name="InventoryEntries30d">Documentos de entrada de inventario creados en la ventana (<c>entries_count</c>/<c>total</c>).</param>
/// <param name="InventoryExits30d">Documentos de salida de inventario creados en la ventana (<c>exits_count</c>/<c>total</c>).</param>
/// <param name="ProgramTasks30d">Tareas del programa completadas en la ventana (<c>tasks_completed_today</c>/<c>general</c>).</param>
/// <param name="ActivitySeries30d">
/// Serie diaria de actividad de la ventana (una entrada por día calendario, en orden
/// ascendente; 0 cuando un módulo no registró actividad ese día).
/// </param>
public record DashboardKpisDto(
    int TotalPatients,
    int NewPatients30d,
    int HealthTests30d,
    int InventoryEntries30d,
    int InventoryExits30d,
    int ProgramTasks30d,
    IReadOnlyList<DashboardActivityPoint> ActivitySeries30d);

/// <summary>
/// Punto diario de la serie de actividad del dashboard Home del ERP.
/// Cada campo suma lo que su rollup correspondiente registró para ese día calendario.
/// </summary>
/// <param name="Date">Etiqueta del día en formato invariante <c>"MMM d"</c> (ej. "Sep 4").</param>
/// <param name="NewPatients">Pacientes nuevos registrados ese día (rollup de pacientes, fila global).</param>
/// <param name="Tests">Tests de salud completados ese día (rollup de tests de salud, fila global).</param>
/// <param name="Entries">Entradas de inventario creadas ese día (rollup de inventario).</param>
/// <param name="Exits">Salidas de inventario creadas ese día (rollup de inventario).</param>
/// <param name="ProgramTasks">Tareas del programa completadas ese día (rollup de programa ANTARES).</param>
public record DashboardActivityPoint(
    string Date,
    int NewPatients,
    int Tests,
    int Entries,
    int Exits,
    int ProgramTasks);
