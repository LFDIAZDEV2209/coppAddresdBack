using System.Globalization;

namespace CoppAddresd.Application.Features.Nutrition.Engine;

public sealed record NutritionAlert(string S, string M, bool Tec = false);

public sealed partial class NutritionEngine
{
    private static string N(double n) => n.ToString(CultureInfo.InvariantCulture);
    public static IReadOnlyList<NutritionAlert> ClinicalFlags(NutritionPatient p, NutritionTargets tg)
    {
        var f = new List<NutritionAlert>();
        if (p.NutRiesgo == "alto") f.Add(new("crit", "Conducta alimentaria de riesgo (alimentación emocional): evitar dieta restrictiva; coordinar con psicología."));
        if (p.SleepRF >= 2) f.Add(new("crit", $"{N(p.SleepRF.Value)} señales de posible apnea del sueño: valoración médica antes de intensificar el plan."));
        if (p.RiesgoNum >= 4) f.Add(new("crit", "Riesgo cardiometabólico MUY ALTO: revisión individual obligatoria."));
        if (p.Hba1c >= 6.5) f.Add(new("crit", "HbA1c en rango diabético: validar plan con médico tratante."));
        if (p.Imc >= 35) f.Add(new("crit", "IMC ≥ 35: plan debe ser supervisado individualmente."));
        var wp = WeightPlan(p);
        if (wp.Fase == "subir") f.Add(new("crit", $"IMC {N(p.Imc)} por debajo del rango saludable: no se aplica déficit; valoración médica y nutricional individual."));
        else if (wp.Fase == "mantener" && tg.Deficit > 0) f.Add(new("warn", $"Ya está en un peso saludable (meta {N(wp.MetaKg)} kg): el plan no debería tener déficit calórico. Pasa a mantenimiento."));
        else if (wp.Fase == "perder" && tg.Deficit == 0) f.Add(new("warn", $"IMC {N(p.Imc)} sobre el rango saludable y el plan está en mantenimiento (meta {N(wp.MetaKg)} kg)."));
        if (wp.Fase == "perder" && tg.Deficit > 0 && wp.Old) f.Add(new("info", "Adulto mayor: déficit moderado (≤10 %) y proteína alta para conservar masa muscular."));
        if (tg.K == tg.Floor) f.Add(new("warn", "Meta calórica en el piso mínimo; revisar si es realmente necesaria."));
        if (p.Estres == "muy_elevado") f.Add(new("warn", "Estrés muy elevado: puede afectar la adherencia; reforzar acompañamiento."));
        if (p.Adh is "A0" or "A1") f.Add(new("info", "Adherencia baja: plan simplificado con 2 menús rotativos por comida."));
        if (p.Hba1c >= 5.7 && p.Hba1c < 6.5) f.Add(new("info", $"HbA1c {N(p.Hba1c.Value)}% (prediabetes): carbohidratos complejos, fibra y proteína en cada comida."));
        if (p.SleepNivel is "prioritario" or "fragil") f.Add(new("info", "Sueño frágil: cena liviana ≥ 2–3 h antes de dormir; sin cafeína después de las 2 PM."));
        if (p.RiesgoNum >= 3 && p.RiesgoNum < 4) f.Add(new("info", "Riesgo cardiometabólico alto: sodio < 2.000 mg/día y grasas saturadas < 7% de las kcal."));
        return f;
    }
    public IReadOnlyList<NutritionAlert> ValidatePlan(NutritionTargets tg, IEnumerable<NutritionDay> days)
    {
        double maxK = 0, minP = 1e9, minFi = 1e9, maxC = 0, maxF = 0;
        static double Dev(double a, double b) => Math.Abs(a - b) / b;
        foreach (var day in days)
        {
            var t = DayTotals(day); maxK = Math.Max(maxK, Dev(t.K, tg.K)); minP = Math.Min(minP, t.P);
            minFi = Math.Min(minFi, t.Fi); maxC = Math.Max(maxC, Dev(t.C, tg.C)); maxF = Math.Max(maxF, Dev(t.F, tg.F));
        }
        var f = new List<NutritionAlert>();
        if (tg.K < tg.Floor) f.Add(new("crit", $"Meta de {N(tg.K)} kcal por debajo del piso seguro ({N(tg.Floor)} kcal).", true));
        if (maxK > .08) f.Add(new("warn", $"Algún día se desvía {N(R0(maxK * 100))}% de la meta calórica (tolerancia 8%).", true));
        if (minP < tg.P * .9) f.Add(new("warn", $"Proteína mínima de {N(minP)} g (meta {N(tg.P)} g).", true));
        if (minFi < tg.Fi * .9) f.Add(new("warn", $"Fibra mínima de {N(minFi)} g (meta {N(tg.Fi)} g).", true));
        if (maxC > .20) f.Add(new("warn", $"Carbohidratos se desvían {N(R0(maxC * 100))}% de la meta.", true));
        if (maxF > .25) f.Add(new("warn", $"Grasas se desvían {N(R0(maxF * 100))}% de la meta.", true));
        return f;
    }
    public IReadOnlyList<NutritionAlert> AllFlags(NutritionPatient p, NutritionTargets t, IEnumerable<NutritionDay> days)
        => ClinicalFlags(p, t).Concat(ValidatePlan(t, days)).ToArray();
    public static bool EligibleBulk(IEnumerable<NutritionAlert> flags) => !flags.Any(x => x.S == "crit" || x.Tec);
    public static bool CanApprove(IEnumerable<NutritionAlert> flags, bool criticalConfirmed) => criticalConfirmed || !flags.Any(x => x.S == "crit");
    public static IReadOnlyList<string> DefaultNotes(NutritionPatient p)
    {
        var notes = new List<string> { "Bebe agua distribuida durante el día; evita bebidas azucaradas y jugos.",
            "Cena antes de las 7:30 PM, sin carbohidratos simples (pan blanco, azúcar, gaseosas)." };
        if (p.Estres is "elevado" or "muy_elevado") notes.Add("Incluye almendras, espinaca y legumbres (magnesio) para apoyar el manejo del estrés.");
        if (p.SleepNivel is "prioritario" or "fragil") notes.Add("Evita café y té después de las 2:00 PM.");
        notes.Add("Este plan es de apoyo y fue revisado por tu nutricionista; no reemplaza la consulta médica.");
        return notes;
    }
}
