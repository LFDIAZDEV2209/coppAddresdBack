using System.Text.Json;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Motor de reglas de adaptación del programa (SPEC §6.8, T-23). Determinista
/// (sin aleatoriedad temporal): dada la misma entrada, produce la misma salida.
///
/// Reglas (SPEC §6.8):
/// 1. 2+ días sin completar el programa (día no perfecto) en los últimos 7 días
///    → <see cref="AdaptationKind.RoutineContentRefresh"/> (variante estándar).
/// 2. Ánimo (mood) ≤ 2 en TODOS los días con check-in de la ventana de 7 días
///    → <see cref="AdaptationKind.RoutineContentRefresh"/> (variante suave).
/// 3. El paciente cruza el umbral de XP (SPEC/AC-16: &gt; 5000)
///    → <see cref="AdaptationKind.DifficultyChange"/> con aprobación clínica.
///
/// Las reglas 1 y 2 son mutuamente excluyentes en una evaluación (el ánimo
/// bajo tiene prioridad: es la variante "más suave" de la misma entidad); la
/// regla 3 es independiente y puede coexistir con un refresco.
/// </summary>
public sealed class ProgramAdaptationEngine : IProgramAdaptationEngine
{
    /// <summary>Umbral de XP para la propuesta de cambio de dificultad (SPEC §6.8 / AC-16).</summary>
    public const int DifficultyChangeXpThreshold = 5000;

    /// <summary>Días con ánimo ≤ 2 requeridos para la variante suave (SPEC §6.8: "7 days").</summary>
    private const int LowMoodWindowDays = 7;

    /// <summary>Días no perfectos requeridos para el refresco (SPEC §6.8: "2+ missed perfect days").</summary>
    private const int MissedPerfectDaysThreshold = 2;

    public IReadOnlyList<AdaptationProposal> Evaluate(AdaptationEvaluationInput input)
    {
        var proposals = new List<AdaptationProposal>(2);

        // Regla 3: cruce de umbral de XP (solo en el cruce, no en cada tarea).
        if (
            input.PreviousXpBalance <= DifficultyChangeXpThreshold
            && input.CurrentXpBalance > DifficultyChangeXpThreshold
        )
        {
            proposals.Add(CreateDifficultyChangeProposal(input.EnrollmentId));
        }

        // Reglas 1 y 2: refresco de rutina (mutuamente excluyentes; ánimo bajo gana).
        if (HasSustainedLowMood(input.Last7Days))
        {
            proposals.Add(CreateRoutineRefreshProposal(input.EnrollmentId, gentleVariant: true));
        }
        else if (
            CountMissedPerfectDays(input.Last7Days, input.TodayLocalDate)
            >= MissedPerfectDaysThreshold
        )
        {
            proposals.Add(CreateRoutineRefreshProposal(input.EnrollmentId, gentleVariant: false));
        }

        return proposals;
    }

    private static bool HasSustainedLowMood(IReadOnlyList<AdaptationDayWindow> days)
    {
        // La ventana trae exactamente LowMoodWindowDays días; la regla exige
        // check-in con mood ≤ 2 en todos (sin check-in el día no aporta).
        return days.Count >= LowMoodWindowDays
            && days.All(d => d.MoodScore is { } mood && mood <= 2);
    }

    private static int CountMissedPerfectDays(
        IReadOnlyList<AdaptationDayWindow> days,
        DateOnly todayLocalDate
    )
    {
        // Solo días YA vencidos: hoy aún puede completarse y no cuenta como
        // día perdido (SPEC §6.8: "missed perfect days in last 7 days").
        return days.Count(d => d.LocalDate < todayLocalDate && !d.IsPerfectDay);
    }

    private static AdaptationProposal CreateDifficultyChangeProposal(Guid enrollmentId) =>
        new(
            Kind: AdaptationKind.DifficultyChange,
            TargetEntityType: AdaptationTargetEntityType.ProgramEnrollments,
            TargetEntityId: enrollmentId,
            Payload: JsonSerializer.SerializeToElement(
                new { xpThreshold = DifficultyChangeXpThreshold, suggestedDirection = "increase" }
            ),
            Reason: $"El paciente superó los {DifficultyChangeXpThreshold} XP; se sugiere subir la dificultad del programa (AC-16).",
            RequiresApproval: true
        );

    private static AdaptationProposal CreateRoutineRefreshProposal(
        Guid enrollmentId,
        bool gentleVariant
    ) =>
        new(
            Kind: AdaptationKind.RoutineContentRefresh,
            TargetEntityType: AdaptationTargetEntityType.ProgramEnrollments,
            TargetEntityId: enrollmentId,
            Payload: JsonSerializer.SerializeToElement(
                new { variant = gentleVariant ? "gentle" : "standard" }
            ),
            Reason: gentleVariant
                ? "Ánimo bajo sostenido (≤ 2) durante 7 días; se sugiere una rutina más suave."
                : "2 o más días sin completar el programa en los últimos 7 días; se sugiere refrescar la rutina.",
            RequiresApproval: false
        );
}
