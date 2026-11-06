namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Resolvedor de contenido activo por fecha local. Prioriza la prescripción
/// publicada de Nutrition y usa bienestar únicamente en su ausencia:
/// <list type="bullet">
///   <item>El plan de alimentación activo (status Active, ventana de fechas vigente).</item>
///   <item>Si el plan tiene un día para el weekday de la fecha consultada.</item>
///   <item>La rutina de ejercicio activa (misma regla de ventana).</item>
/// </list>
///
/// Algoritmo de selección (SPEC §6.10): de todas las asignaciones con status
/// Active y ventana que cubre <paramref name="localDate"/>, gana la de
/// <c>start_date</c> más reciente (overlap rule).
/// </summary>
public interface IProgramContentResolver
{
    /// <summary>
    /// Resuelve el contenido activo del programa para el paciente en la fecha
    /// local indicada. Devuelve <c>null</c> si no hay prescripción publicada
    /// ni asignación activa de alimentación o ejercicio.
    /// </summary>
    /// <param name="patientId">ID del paciente.</param>
    /// <param name="localDate">Fecha local del paciente (no UTC).</param>
    /// <param name="ct">Token de cancelación.</param>
    Task<ProgramContentResolution?> ResolveAsync(
        Guid patientId,
        DateOnly localDate,
        CancellationToken ct = default);

    /// <summary>
    /// Resuelve el contenido activo para TODAS las fechas locales de la ventana
    /// [<c>from</c>, <c>to</c>] en una sola pasada (T-83, B18): carga las
    /// asignaciones del paciente UNA vez y los días de cada plan distinto una
    /// vez (cache). Devuelve un diccionario solo con las fechas que tienen
    /// contenido activo (ausencia de clave = sin asignación para ese día).
    /// Consumido por el timeline del configurador de contenido (GET content),
    /// que necesita resolver N semanas sin recargar asignaciones por semana.
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, ProgramContentResolution>> ResolveRangeAsync(
        Guid patientId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default);
}

/// <summary>
/// Resolución de contenido activo del programa para un paciente en una fecha
/// local (SPEC §4.2/§4.3/§6.10). Resultado inmutable del resolvedor.
/// Los IDs de prescripción y de bienestar son fuentes distintas; nunca se
/// mezclan sus días ni sus referencias de persistencia.
/// </summary>
public sealed record ProgramContentResolution
{
    /// <summary>ID del plan de alimentación activo, o <c>null</c> si no hay asignación vigente.</summary>
    public Guid? NutritionPlanId { get; init; }

    /// <summary>Prescripción publicada de Nutrition; nunca se persiste como FK wellness.</summary>
    public Guid? SourcePrescriptionId { get; init; }

    /// <summary>Día inclusivo del ciclo de la prescripción (1–30), separado del weekday wellness.</summary>
    public int? PrescriptionDayNumber { get; init; }

    /// <summary>
    /// Número de día del plan que coincide con el weekday de la fecha consultada
    /// (1=Lunes..7=Domingo, convención ISO 8601). <c>null</c> si el plan activo
    /// no tiene un día para ese weekday.
    /// </summary>
    public int? NutritionPlanDayNumber { get; init; }

    /// <summary>
    /// <c>true</c> si hay día nutricional resuelto, del ciclo prescrito o del plan wellness.
    /// Se conserva el nombre existente para los consumidores del programa.
    /// </summary>
    public bool HasDayForWeekday => NutritionPlanDayNumber.HasValue || PrescriptionDayNumber.HasValue;

    /// <summary>ID de la rutina de ejercicio activa, o <c>null</c> si no hay asignación vigente.</summary>
    public Guid? ExerciseRoutineId { get; init; }
}
