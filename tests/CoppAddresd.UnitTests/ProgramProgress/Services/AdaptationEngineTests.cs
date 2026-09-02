using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.UnitTests.ProgramProgress.Services;

/// <summary>
/// Tests del motor de reglas de adaptación (SPEC §6.8, T-23 / AC-16):
/// determinista, sin I/O; las reglas 1/2 (refresco de rutina) son mutuamente
/// excluyentes y la regla 3 (cruce de XP) es independiente.
/// </summary>
public class AdaptationEngineTests
{
    private readonly ProgramAdaptationEngine _engine = new();
    private readonly Guid _enrollmentId = Guid.NewGuid();

    private static AdaptationEvaluationInput Input(
        Guid enrollmentId,
        DateOnly today,
        int previousBalance,
        int currentBalance,
        params AdaptationDayWindow[] days
    ) => new(enrollmentId, today, previousBalance, currentBalance, days);

    private static AdaptationDayWindow PerfectDay(DateOnly date) => new(date, true, null);

    private static AdaptationDayWindow MissedDay(DateOnly date, int? mood = null) =>
        new(date, false, mood);

    private static DateOnly Today() => new(2026, 8, 28);

    private static List<AdaptationDayWindow> SevenDays(
        DateOnly today,
        Func<DateOnly, AdaptationDayWindow> dayFactory
    ) => Enumerable.Range(0, 7).Select(i => dayFactory(today.AddDays(i - 6))).ToList();

    // --- Regla 1: días imperfectos ---

    [Fact]
    public void Evaluate_DosDiasImperfectosEn7Dias_ProponeRoutineContentRefresh()
    {
        var today = Today();
        var days = SevenDays(
            today,
            d => d == today.AddDays(-1) || d == today.AddDays(-2) ? MissedDay(d) : PerfectDay(d)
        );

        var proposals = _engine.Evaluate(
            Input(_enrollmentId, today, previousBalance: 100, currentBalance: 180, days.ToArray())
        );

        var refresh = Assert.Single(proposals);
        Assert.Equal(AdaptationKind.RoutineContentRefresh, refresh.Kind);
        Assert.False(refresh.RequiresApproval);
        Assert.Equal(AdaptationTargetEntityType.ProgramEnrollments, refresh.TargetEntityType);
        Assert.Equal(_enrollmentId, refresh.TargetEntityId);
        Assert.Contains("2 o más días", refresh.Reason);
    }

    [Fact]
    public void Evaluate_UnSoloDiaImperfecto_NoProponeNada()
    {
        var today = Today();
        var days = SevenDays(today, d => d == today.AddDays(-1) ? MissedDay(d) : PerfectDay(d));

        var proposals = _engine.Evaluate(
            Input(_enrollmentId, today, previousBalance: 100, currentBalance: 180, days.ToArray())
        );

        Assert.Empty(proposals);
    }

    [Fact]
    public void Evaluate_DiaDeHoyImperfecto_NoCuentaComoPerdido()
    {
        // Hoy aún puede completarse: un día imperfecto HOY no dispara la regla.
        var today = Today();
        var days = SevenDays(today, d => MissedDay(d));
        days[^1] = MissedDay(today); // hoy imperfecto (aún en curso)

        var proposals = _engine.Evaluate(
            Input(_enrollmentId, today, previousBalance: 100, currentBalance: 180, days.ToArray())
        );

        // 6 días pasados imperfectos + hoy: solo cuentan los 6 → dispara igual.
        Assert.Single(proposals);
        Assert.Equal(AdaptationKind.RoutineContentRefresh, proposals[0].Kind);
    }

    // --- Regla 2: ánimo bajo sostenido ---

    [Fact]
    public void Evaluate_AnimoBajo7Dias_ProponeVarianteSuave()
    {
        var today = Today();
        var days = SevenDays(today, d => MissedDay(d, mood: 1));

        var proposals = _engine.Evaluate(
            Input(_enrollmentId, today, previousBalance: 100, currentBalance: 180, days.ToArray())
        );

        var refresh = Assert.Single(proposals);
        Assert.Equal(AdaptationKind.RoutineContentRefresh, refresh.Kind);
        Assert.Contains("Ánimo bajo sostenido", refresh.Reason);
        Assert.Contains("gentle", refresh.Payload.GetRawText());
    }

    [Fact]
    public void Evaluate_AnimoBajoGanaSobreDiasImperfectos_UnaSolaPropuesta()
    {
        var today = Today();
        var days = SevenDays(today, d => MissedDay(d, mood: 2));

        var proposals = _engine.Evaluate(
            Input(_enrollmentId, today, previousBalance: 100, currentBalance: 180, days.ToArray())
        );

        // Las reglas 1 y 2 son mutuamente excluyentes: una sola propuesta.
        var refresh = Assert.Single(proposals);
        Assert.Equal(AdaptationKind.RoutineContentRefresh, refresh.Kind);
        Assert.Contains("gentle", refresh.Payload.GetRawText());
    }

    [Fact]
    public void Evaluate_AnimoBajoNoConsistente_NoProponeVarianteSuave()
    {
        // Un solo día con ánimo bajo no es "consistente 7 días": la regla 2 no
        // aplica, y un solo día imperfecto no alcanza el umbral de la regla 1.
        var today = Today();
        var days = SevenDays(today, d => PerfectDay(d));
        days[2] = MissedDay(today.AddDays(-4), mood: 1);

        var proposals = _engine.Evaluate(
            Input(_enrollmentId, today, previousBalance: 100, currentBalance: 180, days.ToArray())
        );

        Assert.Empty(proposals);
    }

    // --- Regla 3: cruce de umbral de XP (AC-16) ---

    [Fact]
    public void Evaluate_CruceDe5000Xp_ProponeDifficultyChangeConAprobacion()
    {
        var today = Today();
        var days = SevenDays(today, d => PerfectDay(d));

        var proposals = _engine.Evaluate(
            Input(
                _enrollmentId,
                today,
                previousBalance: ProgramAdaptationEngine.DifficultyChangeXpThreshold,
                currentBalance: ProgramAdaptationEngine.DifficultyChangeXpThreshold + 1,
                days.ToArray()
            )
        );

        var change = Assert.Single(proposals);
        Assert.Equal(AdaptationKind.DifficultyChange, change.Kind);
        Assert.True(change.RequiresApproval);
        Assert.Equal(_enrollmentId, change.TargetEntityId);
        Assert.Contains("5000", change.Reason);
    }

    [Fact]
    public void Evaluate_YaSobre5000SinCruce_NoProponeDifficultyChange()
    {
        // Si ya estaba sobre el umbral, la regla no se dispara en cada tarea.
        var today = Today();
        var days = SevenDays(today, d => PerfectDay(d));

        var proposals = _engine.Evaluate(
            Input(
                _enrollmentId,
                today,
                previousBalance: ProgramAdaptationEngine.DifficultyChangeXpThreshold + 200,
                currentBalance: ProgramAdaptationEngine.DifficultyChangeXpThreshold + 280,
                days.ToArray()
            )
        );

        Assert.Empty(proposals);
    }

    [Fact]
    public void Evaluate_CruceMasAnimoBajo_ProponeDosCambiosIndependientes()
    {
        var today = Today();
        var days = SevenDays(today, d => MissedDay(d, mood: 1));

        var proposals = _engine.Evaluate(
            Input(
                _enrollmentId,
                today,
                previousBalance: ProgramAdaptationEngine.DifficultyChangeXpThreshold,
                currentBalance: ProgramAdaptationEngine.DifficultyChangeXpThreshold + 1,
                days.ToArray()
            )
        );

        Assert.Equal(2, proposals.Count);
        Assert.Contains(
            proposals,
            p => p.Kind == AdaptationKind.DifficultyChange && p.RequiresApproval
        );
        Assert.Contains(
            proposals,
            p => p.Kind == AdaptationKind.RoutineContentRefresh && !p.RequiresApproval
        );
    }

    // --- Determinismo ---

    [Fact]
    public void Evaluate_MismaEntrada_MismaSalida()
    {
        var today = Today();
        var days = SevenDays(
            today,
            d => d == today.AddDays(-1) || d == today.AddDays(-2) ? MissedDay(d) : PerfectDay(d)
        );

        var first = _engine.Evaluate(
            Input(_enrollmentId, today, previousBalance: 5000, currentBalance: 5001, days.ToArray())
        );
        var second = _engine.Evaluate(
            Input(_enrollmentId, today, previousBalance: 5000, currentBalance: 5001, days.ToArray())
        );

        Assert.Equal(first.Count, second.Count);
        Assert.Equal(first[0].Kind, second[0].Kind);
        Assert.Equal(first[0].Reason, second[0].Reason);
        Assert.Equal(first[0].Payload.GetRawText(), second[0].Payload.GetRawText());
    }
}
