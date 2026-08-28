namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Servicio de detección de debilidades del paciente (SPEC §21, C — "Paso 7c"):
/// orquesta la construcción del paquete semanal (vía <c>IProgramRepository</c>),
/// la evaluación del <see cref="WeaknessRulesEngine"/> y la persistencia de las
/// debilidades NUEVAS (dedupe AC-43: se omite una regla si ya existe una fila
/// <c>open</c>/<c>acknowledged</c>/<c>in_intervention</c> con el mismo código).
///
/// Se dispara SOLO desde <c>POST /scores/calculate</c> (tras puntajes + XP
/// clínica + premios semanales de nutrición), una vez por recálculo; el dedupe
/// por estado abierto lo hace idempotente (AC-45). El <c>GET /scores</c> nunca
/// detecta debilidades.
/// </summary>
public interface IWeaknessDetectionService
{
    /// <summary>
    /// Detecta y persiste las debilidades nuevas del período del paciente.
    /// Devuelve el resumen (reglas disparadas y filas nuevas persistidas).
    /// Paciente sin inscripción activa → resultado vacío (sin error).
    /// </summary>
    Task<WeaknessDetectionResult> DetectAndPersistAsync(Guid patientId, CancellationToken ct = default);
}

/// <summary>
/// Resumen de una ejecución de detección (SPEC §21, C): reglas que dispararon
/// y cuántas filas NUEVAS se persistieron (tras el dedupe AC-43).
/// </summary>
public sealed record WeaknessDetectionResult(
    int RulesFired,
    int NewWeaknessesPersisted)
{
    public static readonly WeaknessDetectionResult Empty = new(0, 0);
}