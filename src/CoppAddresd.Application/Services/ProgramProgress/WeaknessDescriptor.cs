using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Descriptor de una debilidad detectada (SPEC §21, B): el resultado puro del
/// <see cref="WeaknessRulesEngine"/> — sin persistencia. El servicio de
/// detección lo traduce a una fila de <c>app.weaknesses</c> (dedupe AC-43:
/// se omite si ya existe una <c>open</c>/<c>acknowledged</c>/
/// <c>in_intervention</c> con el mismo <c>Code</c>).
/// </summary>
/// <param name="Code">Código canónico de la regla (ver <see cref="WeaknessCodes"/>).</param>
/// <param name="Category">Eje funcional (SPEC §21, A).</param>
/// <param name="Severity">Severidad ordinal para la cola clínica.</param>
/// <param name="Title">Título corto en español (varchar 120).</param>
/// <param name="Description">Hallazgo con indicador + acción sugerida (puede contener contexto clínico).</param>
/// <param name="MetricId">Métrica clínica del indicador (si la regla la tiene; p. ej. glucosa, % grasa).</param>
/// <param name="IndicatorValue">Valor del indicador que disparó la regla.</param>
/// <param name="Action">Acción sugerida (copy clínica: p. ej. <c>create_intervention</c>, <c>referral_psychologist</c>).</param>
public sealed record WeaknessDescriptor(
    string Code,
    WeaknessCategory Category,
    WeaknessSeverity Severity,
    string Title,
    string Description,
    Guid? MetricId,
    decimal? IndicatorValue,
    string Action);