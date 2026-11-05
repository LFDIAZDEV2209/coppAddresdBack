using System.Globalization;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Features.Nutrition.Engine;

public sealed record NutritionItem(string Id, double G);
public sealed record NutritionMenu(string Name, NutritionItem[] Items, string Note);
public sealed record NutritionMacros(double K, double C, double P, double F, double Fi);
public sealed record NutritionMealTarget(double C, double P, double F, double Fi);
public sealed record NutritionDay(int D, Dictionary<string, int> Tpl, Dictionary<string, NutritionItem[]> Meals);

public sealed partial class NutritionEngine(IReadOnlyDictionary<string, NutritionFoodItem> foods)
{
    public static readonly string[] MealOrder = ["des", "alm", "mer", "cen"];
    private readonly Dictionary<string, NutritionItem[]> cache = new(StringComparer.Ordinal);
    public NutritionMacros ItemMac(NutritionItem item)
    {
        var food = foods[item.Id]; var q = item.G / 100;
        var c = food.Carbs100 * q; var p = food.Protein100 * q; var f = food.Fat100 * q;
        return new(4 * c + 4 * p + 9 * f, c, p, f, food.Fiber100 * q);
    }
    public NutritionMacros SumItems(IEnumerable<NutritionItem> items)
    {
        double k = 0, c = 0, p = 0, f = 0, fi = 0;
        foreach (var item in items) { var m = ItemMac(item); k += m.K; c += m.C; p += m.P; f += m.F; fi += m.Fi; }
        return new(R0(k), R0(c), R0(p), R0(f), R0(fi));
    }
    public NutritionMacros DayTotals(NutritionDay day)
    {
        double k = 0, c = 0, p = 0, f = 0, fi = 0;
        foreach (var meal in MealOrder) { var m = SumItems(day.Meals[meal]); k += m.K; c += m.C; p += m.P; f += m.F; fi += m.Fi; }
        return new(k, c, p, f, fi);
    }
    public static Dictionary<string, NutritionMealTarget> MealTargets(NutritionTargets t)
    {
        double[] c = [.28, .38, .12, .22], p = [.25, .35, .10, .30], f = [.27, .33, .13, .27], fi = [.25, .35, .15, .25];
        return MealOrder.Select((m, i) => (m, target: new NutritionMealTarget(t.C * c[i], t.P * p[i], t.F * f[i], t.Fi * fi[i])))
            .ToDictionary(x => x.m, x => x.target);
    }
    public static Dictionary<string, double> MealKcalTargets(NutritionTargets t) => MealTargets(t)
        .ToDictionary(x => x.Key, x => R0(4 * x.Value.C + 4 * x.Value.P + 9 * x.Value.F));
    public string QtyText(NutritionItem item)
    {
        var f = foods[item.Id]; var grams = R0(item.G).ToString(CultureInfo.InvariantCulture);
        if (f.GramsPerUnit is not { } unit) return $"{grams} g";
        var n = R0(item.G / unit * 2) / 2;
        return $"{grams} g · ≈ {n.ToString(CultureInfo.InvariantCulture)} {(n == 1 ? f.UnitSingular : f.UnitPlural)}";
    }
    public double RoundGrams0(NutritionFoodItem food, double g)
    {
        if (food.GramsPerUnit is { } ug) { var step = Or(food.UnitStep, .5); return Math.Max(step, R0(g / ug / step) * step) * ug; }
        // El orden es literal: te es V y no llega a la rama posterior de 200 g.
        if (food.FoodGroup == "V") return Math.Max(20, R0(g / 10) * 10);
        if (food.Id == "te") return 200;
        return Math.Max(5, R0(g / 5) * 5);
    }
    public double RoundGrams(string id, double g)
    {
        var food = foods[id]; var r = RoundGrams0(food, g);
        if (!NutritionMenus.Caps.TryGetValue(id, out var cap)) return r;
        if (food.GramsPerUnit is not { } ug) return Clamp(r, cap.Min, cap.Max);
        var step = Or(food.UnitStep, .5);
        return Clamp(r, Math.Ceiling(cap.Min / ug / step) * step * ug, Math.Floor(cap.Max / ug / step) * step * ug);
    }
    public NutritionItem[] SolveMeal(NutritionMenu template, NutritionMealTarget target, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var key = FormattableString.Invariant($"{template.Name}|{R0(target.C)}|{R0(target.P)}|{R0(target.F)}|{R0(target.Fi)}");
        if (cache.TryGetValue(key, out var saved)) return saved.ToArray();
        NutritionItem[] Build(double[] scale) => template.Items.Select(item => new NutritionItem(item.Id,
            RoundGrams(item.Id, item.G * (foods[item.Id].FoodGroup switch { "C" => scale[0], "P" => scale[1], "F" => scale[2], _ => 1 })))).ToArray();
        double Cost(double[] scale)
        {
            double c = 0, p = 0, f = 0, fi = 0;
            foreach (var item in Build(scale)) { var m = ItemMac(item); c += m.C; p += m.P; f += m.F; fi += m.Fi; }
            var e = Math.Pow((c - target.C) / Math.Max(target.C, 1), 2)
                + Math.Pow((p - target.P) / Math.Max(target.P, 1), 2) * 1.4
                + Math.Pow((f - target.F) / Math.Max(target.F, 1), 2);
            var kt = 4 * target.C + 4 * target.P + 9 * target.F; var kg = 4 * c + 4 * p + 9 * f;
            e += 2 * Math.Pow((kg - kt) / kt, 2);
            e += .025 * ((scale[0] - 1) * (scale[0] - 1) + (scale[1] - 1) * (scale[1] - 1) + (scale[2] - 1) * (scale[2] - 1));
            if (fi > 2 * target.Fi) e += .15 * Math.Pow((fi - 2 * target.Fi) / target.Fi, 2);
            return e;
        }
        double[] best = [1, 1, 1]; var bestCost = Cost(best);
        // No sustituir por contadores enteros: la acumulación binaria afecta empates y porciones.
        for (double a = .6; a <= 2.41; a += .1)
        {
            ct.ThrowIfCancellationRequested();
            for (double b = .6; b <= 2.01; b += .1)
            for (double c = .6; c <= 2.01; c += .1) { double[] s = [a, b, c]; var v = Cost(s); if (v < bestCost) { bestCost = v; best = s; } }
        }
        for (double a = -.1; a <= .1001; a += .025)
        for (double b = -.1; b <= .1001; b += .025)
        for (double c = -.1; c <= .1001; c += .025)
        {
            double[] s = [Clamp(best[0] + a, .5, 2.5), Clamp(best[1] + b, .5, 2.1), Clamp(best[2] + c, .5, 2.1)];
            var v = Cost(s); if (v < bestCost) { bestCost = v; best = s; }
        }
        var result = Build(best); cache[key] = result; return result.ToArray();
    }
    public NutritionDay[] BuildDays(NutritionTargets targets, int variety = 4, CancellationToken ct = default)
    {
        if (variety is not (2 or 4)) throw new ArgumentOutOfRangeException(nameof(variety));
        var mt = MealTargets(targets); int[] offsets = [0, 2, 1, 3]; var days = new List<NutritionDay>();
        for (var d = 1; d <= 30; d++)
        {
            ct.ThrowIfCancellationRequested();
            var indices = MealOrder.Select((m, i) => (m, idx: ((d - 1) + offsets[i]) % variety)).ToDictionary(x => x.m, x => x.idx);
            var meals = MealOrder.ToDictionary(m => m, m => SolveMeal(NutritionMenus.Templates[m][indices[m]], mt[m], ct));
            days.Add(new(d, indices, meals));
        }
        return days.ToArray();
    }
    public NutritionDay[] BuildDays(NutritionPatient p, CancellationToken ct = default) => BuildDays(ComputeTargets(p), p.Adh is "A0" or "A1" ? 2 : 4, ct);
    public static IReadOnlyList<(string Moment, double Glasses)> WaterPlan(double n)
    {
        var names = new[] { "Al despertar", "Con el desayuno", "Media mañana", "Con el almuerzo", "Media tarde", "Con la merienda", "Con la cena", "Antes de dormir" };
        var glasses = Enumerable.Repeat(1d, 8).ToArray(); int[] order = [2, 4, 5, 0];
        var extra = Math.Max(0, n - 8); var i = 0;
        while (extra > 0) { glasses[order[i++ % 4]]++; extra--; }
        return names.Select((name, j) => (name, glasses[j])).ToArray();
    }
}
