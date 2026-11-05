namespace CoppAddresd.Application.Features.Nutrition.Engine;

/// <summary>Entradas explícitas del prototipo; un dato clínico ausente no se inventa.</summary>
public sealed record NutritionPatient(double Peso, double Talla, double Edad, string Sexo, double Imc,
    double? Hba1c = null, string? Mov = null, string? Objetivo = null, bool ObjFijo = false,
    string? NutRiesgo = null, double? SleepRF = null, string? SleepNivel = null,
    string? Adh = null, double? RiesgoNum = null, string? Estres = null,
    double? Deficit = null, double? Kcal = null, double? CarbPct = null,
    double? ProtG = null, double? Fiber = null, double? Vasos = null, DateOnly? BirthDate = null);

public sealed record NutritionWeight(string Fase, double MetaKg, double MetaImc, double Thr,
    double Low, double LowKg, double KgLeft, bool Old, bool Auto);
public sealed record NutritionTargets(double K, double C, double P, double F, double Fi,
    double Vasos, double Bmr, double Tdee, double Deficit, double Floor, string Fase,
    double MetaKg, double KgLeft, double WkKg, double Semanas);

/// <summary>Port numérico de engine-rules-spec.md; conserva orden de operaciones y redondeos JS.</summary>
public sealed partial class NutritionEngine
{
    public static double R0(double x)
    {
        if (!double.IsFinite(x) || x == 0 || Math.Abs(x) >= 4503599627370496d) return x;
        var lo = Math.Floor(x);
        var result = x - lo < .5 ? lo : lo + 1;
        return result == 0 && x < 0 ? Math.CopySign(0, -1) : result;
    }
    public static double R1(double x) => R0(x * 10) / 10;
    private static double Clamp(double x, double a, double b) => Math.Max(a, Math.Min(b, x));
    private static double Or(double? x, double fallback) => x.HasValue && x != 0 && !double.IsNaN(x.Value) ? x.Value : fallback;

    public static NutritionWeight WeightPlan(NutritionPatient p)
    {
        var h = p.Talla / 100; var old = p.Edad >= 65;
        var thr = old ? 27.5 : 25; var metaImc = old ? 26 : 24; var low = old ? 23 : 18.5;
        var metaKg = R1(metaImc * h * h); var lowKg = R1(low * h * h);
        var phase = p.ObjFijo && p.Objetivo is "perder" or "mantener" ? p.Objetivo
            : p.Imc < low ? "subir" : p.Imc >= thr ? "perder"
            : p.Objetivo == "perder" && p.Imc > metaImc ? "perder" : "mantener";
        return new(phase!, metaKg, metaImc, thr, low, lowKg,
            phase == "perder" ? Math.Max(0, R1(p.Peso - metaKg)) : 0, old, !p.ObjFijo);
    }

    public static double DefaultDeficit(NutritionPatient p)
    {
        var w = WeightPlan(p);
        if (w.Fase == "subir") return -.08;
        if (w.Fase == "mantener") return 0;
        var d = p.Imc >= 40 ? .25 : p.Imc >= 35 ? .22 : p.Imc >= 30 ? .20 : p.Imc >= 27 ? .15 : .10;
        if (w.Old) d = Math.Min(d, .10);
        if (p.Hba1c >= 6.5 || p.RiesgoNum >= 4) d = Math.Min(d, .15);
        if (p.NutRiesgo == "alto") d = Math.Min(d, .10);
        if (w.KgLeft < 1.5) d = Math.Min(d, .08); else if (w.KgLeft < 3) d = Math.Min(d, .10);
        return d;
    }

    public static NutritionTargets ComputeTargets(NutritionPatient p)
    {
        var bmr = 10 * p.Peso + 6.25 * p.Talla - 5 * p.Edad + (p.Sexo == "M" ? 5 : -161);
        var factor = p.Mov switch { "DESPERTAR" => 1.2, "RAÍZ" => 1.3, "FLUJO" => 1.375,
            "IMPULSO" => 1.465, "MARCHA" => 1.55, "VIBRACIÓN" => 1.725, _ => 1.375 };
        var tdee = bmr * factor; var wp = WeightPlan(p);
        var def = p.Deficit ?? DefaultDeficit(p);
        if (p.Deficit is null && def > 0)
        {
            var cap = p.Peso * .01 * 7700 / (7 * tdee);
            if (def > cap) def = Math.Max(.05, R0(cap * 100) / 100);
        }
        var floor = p.Sexo == "M" ? 1500 : 1200;
        var k = Or(p.Kcal, R0(Math.Max(floor, tdee * (1 - def)) / 50) * 50);
        var carbPct = Or(p.CarbPct, p.Hba1c >= 5.7 ? .45 : .48);
        var protein = Or(p.ProtG, Clamp(Math.Max(R0(1.2 * p.Peso), R0(.2 * k / 4)), R0(.8 * p.Peso), R0(2 * p.Peso)));
        var carbs = R0(carbPct * k / 4); var fat = R0((k - 4 * carbs - 4 * protein) / 9);
        if (fat * 9 / k > .33) { fat = R0(.33 * k / 9); carbs = R0((k - 4 * protein - 9 * fat) / 4); }
        var fiber = Or(p.Fiber, Math.Max(25, R0(14 * k / 1000)));
        var glasses = Or(p.Vasos, Clamp(R0(35 * p.Peso / 250), 8, 12));
        var week = def > 0 ? R1((tdee - k) * 7 / 7700) : 0;
        return new(k, carbs, protein, fat, fiber, glasses, R0(bmr), R0(tdee), def, floor,
            wp.Fase, wp.MetaKg, wp.KgLeft, week, week > 0 ? Math.Ceiling(wp.KgLeft / week) : 0);
    }
}
