using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.UnitTests.ProgramProgress.Scores;

/// <summary>
/// Pruebas unitarias del calculador del Índice de Salud (SPEC §13.4):
/// bandas de cada dimensión y del ponderado. El calculador es una función
/// pura: se alimenta con los datos de ventana que el repositorio reúne
/// (T-39, AC-19 y AC-21).
/// </summary>
public sealed class HealthScoreCalculatorTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly ScoreCalculationContext Context = new(
        PatientId, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20), ScoreTrigger.OnRead);

    private static readonly IReadOnlyList<ScoreWeight> DefaultWeights =
    [
        new(ScoreDimension.adherence, 0.30m),
        new(ScoreDimension.clinical, 0.30m),
        new(ScoreDimension.nutrition, 0.20m),
        new(ScoreDimension.psychology, 0.10m),
        new(ScoreDimension.exercise, 0.10m),
    ];

    private readonly HealthScoreCalculator _calculator = new(NullLogger<HealthScoreCalculator>.Instance);

    private HealthScoreResult Run(
        IReadOnlyList<AdherenceDayStatus>? days = null,
        IReadOnlyList<ClinicalIndicator>? clinical = null,
        NutritionLog? nutrition = null,
        IReadOnlyList<short>? mood = null,
        int exerciseDays = 0,
        int dayCount = 7,
        IReadOnlyList<ScoreWeight>? weights = null)
        => _calculator.Calculate(new HealthScoreInput(
            weights ?? DefaultWeights,
            days ?? [],
            clinical ?? [],
            nutrition,
            mood ?? [],
            exerciseDays,
            dayCount), Context);

    // ------------------------------------------------------------ 13.4.1 adherence

    /// <summary>AC-19: 3 perfectos, 2 parciales, 1 rescatado por congelamiento y 1 perdido → 66.</summary>
    [Fact]
    public void Adherence_MezclaAC19_Devuelve66()
    {
        var days = new List<AdherenceDayStatus>
        {
            AdherenceDayStatus.Perfect, AdherenceDayStatus.Perfect, AdherenceDayStatus.Perfect,
            AdherenceDayStatus.Partial, AdherenceDayStatus.Partial,
            AdherenceDayStatus.Rescued,
            AdherenceDayStatus.Missed,
        };

        var result = Run(days: days);

        // round((3*1.0 + 2*0.6 + 1*0.4 + 1*0.0) / 7 * 100) = round(65.714) = 66
        Assert.Equal(66, result.Adherence);
    }

    [Fact]
    public void Adherence_7Perfectos_Devuelve100()
        => Assert.Equal(100, Run(days: Enumerable.Repeat(AdherenceDayStatus.Perfect, 7).ToList()).Adherence);

    [Fact]
    public void Adherence_SinCheckins_Devuelve0()
        => Assert.Equal(0, Run().Adherence);

    // ------------------------------------------------------------ 13.4.2 clinical

    private static ClinicalIndicator Indicator(decimal baseline, decimal current, FavorableDirection direction = FavorableDirection.LowerIsBetter)
        => new("weight", baseline, current, direction);

    [Fact]
    public void Clinical_CambioFavorableMayor5Porciento_Devuelve100()
        // 100 → 80 con LowerIsBetter (-20%): favorable (signo coincide) → 100.
        => Assert.Equal(100, Run(clinical: [Indicator(100m, 80m)]).Clinical);

    [Fact]
    public void Clinical_CambioFavorableEntre1Y5_Devuelve75()
        // 100 → 95 = -5% con LowerIsBetter (favorable) → 75 (magnitud 5 no > 5).
        => Assert.Equal(75, Run(clinical: [Indicator(100m, 95m)]).Clinical);

    [Fact]
    public void Clinical_CambioEstableMenorIgual1_Devuelve50()
        // 100 → 99 = -1% (magnitud 1, dentro del umbral estable) → 50.
        => Assert.Equal(50, Run(clinical: [Indicator(100m, 99m)]).Clinical);

    [Fact]
    public void Clinical_CambioDesfavorableEntre1Y5_Devuelve25()
        // 100 → 95 con HigherIsBetter (+5% desfavorable) → 25.
        => Assert.Equal(25, Run(clinical:
            [Indicator(100m, 95m, FavorableDirection.HigherIsBetter)]).Clinical);

    [Fact]
    public void Clinical_CambioDesfavorableMayor5_Devuelve10()
        // 100 → 80 con HigherIsBetter (-20% desfavorable) → 10.
        => Assert.Equal(10, Run(clinical:
            [Indicator(100m, 80m, FavorableDirection.HigherIsBetter)]).Clinical);

    [Fact]
    public void Clinical_PromediaVariasMetricas()
        // 100 (favorable 20% con HigherIsBetter) + 75 (favorable 5% con
        // LowerIsBetter) → (100+75)/2 = 87.5 → 88.
        => Assert.Equal(88, Run(clinical:
        [
            Indicator(100m, 120m, FavorableDirection.HigherIsBetter),
            Indicator(100m, 95m),
        ]).Clinical);

    /// <summary>AC-21: sin líneas base ni mediciones → 50 (neutral del sistema de referencia).</summary>
    [Fact]
    public void Clinical_SinDatos_Devuelve50()
        => Assert.Equal(50, Run().Clinical);

    // ------------------------------------------------------------ 13.4.3 nutrition

    [Fact]
    public void Nutrition_LogrosSobreTotales_DevuelvePorcentaje()
        // 8 de 10 hábitos alimentarios → 80.
        => Assert.Equal(80, Run(nutrition: new NutritionLog(8, 10)).Nutrition);

    /// <summary>AC-21: sin logs de hábitos → 0 (SPEC §13.4.3).</summary>
    [Fact]
    public void Nutrition_SinLogs_Devuelve0()
        => Assert.Equal(0, Run().Nutrition);

    [Fact]
    public void Nutrition_ExcesoDeLogrosSeRecortaA100()
        => Assert.Equal(100, Run(nutrition: new NutritionLog(12, 10)).Nutrition);

    // ------------------------------------------------------------ 13.4.4 psychology

    [Fact]
    public void Psychology_RescaleDeAnimo_DevuelvePorcentaje()
        // (5 - 1)/4*100 = 100 y (1 - 1)/4*100 = 0 → promedio 50.
        => Assert.Equal(50, Run(mood: [(short)5, (short)1]).Psychology);

    /// <summary>AC-21: sin registros emocionales → 60 (default moderado, SPEC §13.4.4).</summary>
    [Fact]
    public void Psychology_SinRegistros_Devuelve60()
        => Assert.Equal(60, Run().Psychology);

    // ------------------------------------------------------------ 13.4.5 exercise

    [Fact]
    public void Exercise_DiasConEjercicioSobreDiasDelPeriodo_DevuelvePorcentaje()
        // 5 de 7 días con ejercicio → 71 (500/7 = 71.43 → 71).
        => Assert.Equal(71, Run(exerciseDays: 5, dayCount: 7).Exercise);

    /// <summary>AC-21: sin completaciones de ejercicio → 0 (SPEC §13.4.5).</summary>
    [Fact]
    public void Exercise_SinCompletaciones_Devuelve0()
        => Assert.Equal(0, Run().Exercise);

    // ------------------------------------------------------------ 13.4 ponderado

    /// <summary>
    /// AC-19: el ponderado aplica los pesos por defecto
    /// (0.30/0.30/0.20/0.10/0.10) sobre las 5 dimensiones y redondea.
    /// </summary>
    [Fact]
    public void Ponderado_AplicaPesosPorDefecto_Redondea()
    {
        var result = _calculator.Calculate(new HealthScoreInput(
            DefaultWeights,
            Enumerable.Repeat(AdherenceDayStatus.Perfect, 7).ToList(),   // adherence 100
            [Indicator(100m, 120m, FavorableDirection.HigherIsBetter)],  // clinical 100
            new NutritionLog(8, 10),                                     // nutrition 80
            [(short)5],                                                  // psychology 100
            7,                                                           // exercise 100
            7), Context);

        // 100*0.30 + 100*0.30 + 80*0.20 + 100*0.10 + 100*0.10 = 30+30+16+10+10 = 96
        Assert.Equal(96, result.Total);
    }

    /// <summary>
    /// AC-21: con los neutros sin datos (clinical 50, nutrition 0, psychology
    /// 60, exercise 0, adherence 0) el ponderado = 50*0.30 + 0 + 0 + 60*0.10 + 0
    /// = 15 + 6 = 21 (las dimensiones sin peso no aportan).
    /// </summary>
    [Fact]
    public void Ponderado_ConNeutrosSinDatos_Devuelve21()
    {
        var result = Run(weights: DefaultWeights);
        Assert.Equal(21, result.Total);
    }

    /// <summary>
    /// AC-19 defensivo: si la suma de pesos no es 1.0000 (p. ej. un peso 0 en
    /// la BD), el ponderado se normaliza para seguir viviendo en 0..100.
    /// </summary>
    [Fact]
    public void Ponderado_PesosQueNoSuman1_SeNormaliza()
    {
        var weights = new List<ScoreWeight>
        {
            new(ScoreDimension.adherence, 0.30m),
            new(ScoreDimension.clinical, 0.30m),
            new(ScoreDimension.nutrition, 0.20m),
            new(ScoreDimension.psychology, 0.10m),
            new(ScoreDimension.exercise, 0.00m), // mal configurado → suma 0.90
        };

        var result = _calculator.Calculate(new HealthScoreInput(
            weights,
            Enumerable.Repeat(AdherenceDayStatus.Perfect, 7).ToList(), // 100
            [Indicator(100m, 120m, FavorableDirection.HigherIsBetter)], // 100
            new NutritionLog(8, 10),                                    // 80
            [(short)5],                                                 // 100
            7,                                                          // 100
            7), Context);

        // Normalizado: 100*0.30/0.90 + 100*0.30/0.90 + 80*0.20/0.90 + 100*0.10/0.90
        // = 33.33 + 33.33 + 17.78 + 11.11 = 95.56 → 96.
        Assert.Equal(96, result.Total);
    }
}