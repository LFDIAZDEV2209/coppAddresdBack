namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Resolvedor de contenido activo del programa para un paciente en una fecha
/// local dada (SPEC §4.2/§4.3/§6.10). Servicio de solo lectura que determina:
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
    /// local indicada. Devuelve <c>null</c> si no hay ninguna asignación activa
    /// (ni plan de alimentación ni rutina de ejercicio).
    /// </summary>
    /// <param name="patientId">ID del paciente.</param>
    /// <param name="localDate">Fecha local del paciente (no UTC).</param>
    /// <param name="ct">Token de cancelación.</param>
    Task<ProgramContentResolution?> ResolveAsync(
        Guid patientId,
        DateOnly localDate,
        CancellationToken ct = default);
}

/// <summary>
/// Resolución de contenido activo del programa para un paciente en una fecha
/// local (SPEC §4.2/§4.3/§6.10). Resultado inmutable del resolvedor.
/// Todos los campos son opcionales: <c>null</c> indica ausencia de contenido
/// activo para ese componente.
/// </summary>
public sealed record ProgramContentResolution
{
    /// <summary>ID del plan de alimentación activo, o <c>null</c> si no hay asignación vigente.</summary>
    public Guid? NutritionPlanId { get; init; }

    /// <summary>
    /// Número de día del plan que coincide con el weekday de la fecha consultada
    /// (1=Lunes..7=Domingo, convención ISO 8601). <c>null</c> si el plan activo
    /// no tiene un día para ese weekday.
    /// </summary>
    public int? NutritionPlanDayNumber { get; init; }

    /// <summary>
    /// <c>true</c> si el plan de alimentación activo tiene al menos un día para
    /// el weekday de la fecha consultada. Equivalente a
    /// <see cref="NutritionPlanDayNumber"/>.HasValue, pero explícito para
    /// consumidores que solo necesitan el indicador booleano.
    /// </summary>
    public bool HasDayForWeekday => NutritionPlanDayNumber.HasValue;

    /// <summary>ID de la rutina de ejercicio activa, o <c>null</c> si no hay asignación vigente.</summary>
    public Guid? ExerciseRoutineId { get; init; }
}
