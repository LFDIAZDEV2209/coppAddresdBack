using System.Text.Json;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Día de la ventana de evaluación del motor de adaptaciones: la ventana se
/// arma SIEMPRE con los 7 días calendario que terminan en <c>TodayLocalDate</c>
/// (los días sin check-in llegan con <c>IsPerfectDay = false</c> y
/// <c>MoodScore = null</c>). El motor es una función pura: nunca consulta BD.
/// </summary>
public sealed record AdaptationDayWindow(DateOnly LocalDate, bool IsPerfectDay, int? MoodScore);

/// <summary>
/// Entrada de <see cref="IProgramAdaptationEngine.Evaluate"/>: balance XP
/// ANTES (<c>PreviousXpBalance</c>) y DESPUÉS (<c>CurrentXpBalance</c>) de la
/// escritura que dispara la evaluación (tarea + bonus + hitos), para que la
/// regla de cruce de umbral se dispare una sola vez.
/// </summary>
public sealed record AdaptationEvaluationInput(
    Guid EnrollmentId,
    DateOnly TodayLocalDate,
    int PreviousXpBalance,
    int CurrentXpBalance,
    IReadOnlyList<AdaptationDayWindow> Last7Days
);

/// <summary>
/// Cambio propuesto por el motor: <c>RequiresApproval</c> se deriva del kind
/// (SPEC §6.8) y el caller decide persistirlo como <c>Pending</c> (cola ERP)
/// o auto-aplicarlo en la misma transacción.
/// </summary>
public sealed record AdaptationProposal(
    AdaptationKind Kind,
    AdaptationTargetEntityType TargetEntityType,
    Guid TargetEntityId,
    JsonElement Payload,
    string Reason,
    bool RequiresApproval
);

/// <summary>
/// Motor de reglas de adaptación (SPEC §6.8, T-23): funciones puras,
/// deterministas y sin I/O. El repositorio reúne los datos de la ventana y
/// persiste las propuestas dentro de la transacción de la completación.
/// </summary>
public interface IProgramAdaptationEngine
{
    /// <summary>Evalúa las reglas y devuelve las propuestas aplicables (vacío si ninguna).</summary>
    IReadOnlyList<AdaptationProposal> Evaluate(AdaptationEvaluationInput input);
}
