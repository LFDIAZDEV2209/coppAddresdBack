using System.Globalization;
using System.Text;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Application.Features.ProgramProgress.Assignment;

namespace CoppAddresd.Application.Features.Nutrition.Cases;

public sealed record NutritionMeasuredField(object? Value, string? Unit, string Source, string? SourceCode = null,
    Guid? EvaluationId = null, DateTime? ObservedAt = null);
public sealed record NutritionMeasuredProfile(bool HasMeasuredProfile, bool CanGenerateWithoutManual,
    IReadOnlyDictionary<string, NutritionMeasuredField> Fields, IReadOnlyList<string> MissingData,
    IReadOnlyList<AssignmentResult> Results, NutritionPatient? Patient);

/// <summary>Adapta clasificaciones persistidas; no aplica umbrales clínicos nuevos ni inventa categorías ausentes.</summary>
public static class NutritionMeasuredProfiles
{
    private static string Key(string? value) => new string((value ?? "").Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray())
        .ToLowerInvariant().Replace(' ', '_');

    public static NutritionMeasuredProfile Resolve(NutritionCaseContext context, ClinicalContextDto clinical,
        AssignmentProfileData? assessment, IReadOnlyList<ClinicalMeasurementDto> realMeasurements,
        DateOnly today, NutritionGenerateInput? input = null, NutritionPatient? fallback = null)
    {
        var fields = new Dictionary<string, NutritionMeasuredField>();
        var results = assessment?.Results ?? [];
        var manual = input?.Manual;
        double? Metric(string name, string[] aliases, string unit, double? manualValue = null)
        {
            var biometric = assessment?.Biometrics.FirstOrDefault(x => aliases.Contains(Key(x.QuestionCode))
                && x.Unit == unit && (x.Minimum == null || x.Value >= x.Minimum) && (x.Maximum == null || x.Value <= x.Maximum));
            if (biometric is not null)
            {
                fields[name] = new(biometric.Value, unit, "health_test_response", biometric.QuestionCode, biometric.EvaluationId, biometric.ObservedAt);
                return (double)biometric.Value;
            }
            var row = realMeasurements.OrderByDescending(x => x.ObservedAt).FirstOrDefault(x => aliases.Contains(Key(x.Metric)) && x.Unit == unit);
            if (row is not null) { fields[name] = new(row.Value, unit, "clinical_measurements", row.Metric, ObservedAt: row.ObservedAt); return (double)row.Value; }
            row = clinical.Measurements.OrderByDescending(x => x.ObservedAt).FirstOrDefault(x => aliases.Contains(Key(x.Metric)) && x.Unit == unit);
            if (row is not null) { fields[name] = new(row.Value, unit, "clinical_context", row.Metric, ObservedAt: row.ObservedAt); return (double)row.Value; }
            fields[name] = new(manualValue, unit, manualValue is null ? "missing" : "manual_or_preserved");
            return manualValue;
        }
        string? Category(string name, string[] codes, Func<AssignmentResult, bool> predicate,
            Func<string, string?> map, string? supplied)
        {
            var row = results.Where(x => codes.Contains(Key(x.TestCode)) && predicate(x))
                .OrderByDescending(x => x.ObservedAt).ThenBy(x => x.Code, StringComparer.Ordinal).FirstOrDefault();
            var value = row is null ? null : map(Key(row.Qualifier)) ?? map(Key(row.Severity));
            fields[name] = value is not null ? new(value, null, "health_test_result", row!.Code, row.EvaluationId, row.ObservedAt)
                : new(supplied, null, supplied is null ? "missing" : "manual_or_preserved");
            return value ?? supplied;
        }
        var birth = context.BirthDate ?? input?.BirthDate ?? fallback?.BirthDate;
        var age = birth is { } date ? today.Year - date.Year - (date > today.AddYears(-(today.Year - date.Year)) ? 1 : 0) : (double?)null;
        var sex = Key(context.Sex) switch { "f" or "female" or "femenino" => "F", "m" or "male" or "masculino" => "M", _ => manual?.Sexo ?? fallback?.Sexo };
        fields["birthDate"] = new(birth?.ToString("yyyy-MM-dd"), null, context.BirthDate is null ? birth is null ? "missing" : "manual_or_preserved" : "patient_profile");
        fields["age"] = new(age, "years", age is null ? "missing" : "derived_from_birth_date");
        fields["sex"] = new(sex, null, context.Sex is null ? sex is null ? "missing" : "manual_or_preserved" : "patient_profile");
        var weight = Metric("weightKg", ["peso", "weight", "weight_kg"], "kg", manual?.Peso ?? fallback?.Peso);
        var height = Metric("heightCm", ["talla", "height", "height_cm"], "cm", manual?.Talla ?? fallback?.Talla);
        var bmi = Metric("bmi", ["bmi", "imc"], "kg/m2", manual?.Imc > 0 ? manual.Imc : null);
        if (bmi is null && weight > 0 && height > 0)
        {
            bmi = NutritionEngine.R1(weight.Value / Math.Pow(height.Value / 100, 2));
            fields["bmi"] = new(bmi, "kg/m2", "derived_from_weight_height");
        }
        var hba = Metric("hba1c", ["a1c", "hba1c"], "%", manual?.Hba1c ?? fallback?.Hba1c);
        // Glicemia y grasa solo desde ClinicalMeasurements: nunca desde fórmulas ni valores demo.
        foreach (var (name, aliases, unit) in new[] { ("glucose", new[] { "glucose", "fasting_glucose", "glicemia", "glucose_fasting" }, "mg/dl"),
            ("bodyFatPercent", new[] { "body_fat", "body_fat_pct", "body_fat_percent", "grasa_corporal" }, "%") })
        {
            var row = realMeasurements.OrderByDescending(x => x.ObservedAt).FirstOrDefault(x => aliases.Contains(Key(x.Metric))
                && string.Equals(x.Unit, unit, StringComparison.OrdinalIgnoreCase));
            fields[name] = row is null ? new(null, unit, "missing") : new(row.Value, row.Unit, "clinical_measurements", row.Metric, ObservedAt: row.ObservedAt);
        }
        static bool Total(AssignmentResult x) => x.Type == "score";
        var movement = Category("movement", ["movimiento"], Total, x => x switch
        {
            "despertar" or "inicial" or "bajo" or "low" => "DESPERTAR", "raiz" => "RAÍZ",
            "flujo" or "moderado" or "moderate" => "FLUJO", "impulso" => "IMPULSO",
            "marcha" or "avanzado" or "alto" or "high" => "MARCHA", "vibracion" => "VIBRACIÓN", _ => null
        }, manual?.Mov ?? fallback?.Mov);
        var nutritionRisk = Category("nutritionRisk", ["nutricional", "nutricion"],
            x => x.Type == "subscale" && (Key(x.Code).StartsWith("conducta") || Key(x.Code) == "nutriesgo"),
            x => x switch { "alto" or "high" or "critical" or "critico" => "alto", "moderado" or "moderate" => "moderado", "bajo" or "low" => "bajo", _ => null },
            manual?.NutRiesgo ?? fallback?.NutRiesgo);
        var sleep = Category("sleepLevel", ["sueno"], Total, x => x switch
        {
            "prioritario" or "bajo" or "low" => "prioritario", "fragil" or "moderado" or "moderate" => "fragil",
            "construccion" => "construccion", "consolidado" or "alto" or "high" => "consolidado", _ => null
        }, manual?.SleepNivel ?? fallback?.SleepNivel);
        var apnea = results.Where(x => Key(x.TestCode) == "sueno" && Key(x.Code) == "iapnea" && x.Type == "indicator")
            .OrderByDescending(x => x.ObservedAt).FirstOrDefault();
        var sleepFlags = apnea is null ? manual?.SleepRF ?? fallback?.SleepRF : (double?)apnea.Value;
        fields["sleepRiskFlags"] = apnea is null ? new(sleepFlags, "count", sleepFlags is null ? "missing" : "manual_or_preserved")
            : new(apnea.Value, "count", "health_test_result", apnea.Code, apnea.EvaluationId, apnea.ObservedAt);
        var adherence = Category("testAdherence", ["iac_adresd", "iac-adresd"], Total, x => x switch
        {
            "a0" => "A0", "a1" or "alto" or "high" or "critical" => "A1", "a2" or "moderado" or "moderate" => "A2",
            "a3" or "bajo" or "low" => "A3", "a4" => "A4", _ => null
        }, manual?.Adh ?? fallback?.Adh);
        var risk = Category("cardiometabolicRisk", ["orp", "riesgo_cardiometabolico"], Total, x => x switch
        {
            "bajo" or "low" => "1", "moderado" or "moderate" => "2", "alto" or "high" => "3",
            "muy_alto" or "critical" or "critico" => "4", _ => null
        }, (manual?.RiesgoNum ?? fallback?.RiesgoNum)?.ToString(CultureInfo.InvariantCulture));
        var stress = Category("stress", ["ers", "estres"], Total, x => x switch
        {
            "bajo" or "low" => "bajo", "moderado" or "moderate" => "moderado", "elevado" or "alto" or "high" => "elevado",
            "muy_elevado" or "critical" or "critico" => "muy_elevado", _ => null
        }, manual?.Estres ?? fallback?.Estres);
        var missing = new List<string>();
        if (!context.TestsComplete) missing.Add("bateria_9_tests");
        foreach (var field in new[] { "weightKg", "heightCm", "birthDate", "sex", "hba1c", "movement", "nutritionRisk", "sleepLevel", "sleepRiskFlags", "testAdherence", "cardiometabolicRisk", "stress" })
            if (fields[field].Value is null) missing.Add(field);
        var minimum = weight > 0 && height > 0 && age is >= 18 and <= 120 && sex is "F" or "M";
        NutritionPatient? patient = minimum ? (manual ?? fallback ?? new(weight!.Value, height!.Value, age!.Value, sex!, bmi ?? 0)) with
        {
            Peso = weight!.Value, Talla = height!.Value, Edad = age!.Value, Sexo = sex!, BirthDate = birth, Imc = bmi ?? 0,
            Hba1c = hba, Mov = movement, NutRiesgo = nutritionRisk, SleepRF = sleepFlags, SleepNivel = sleep,
            Adh = adherence, RiesgoNum = double.TryParse(risk, NumberStyles.Float, CultureInfo.InvariantCulture, out var r) ? r : null, Estres = stress
        } : null;
        var hasMeasured = fields.Any(x => x.Value.Value is not null && x.Value.Source is "health_test_response" or "health_test_result" or "clinical_measurements" or "clinical_context");
        var withoutManual = minimum && fields["weightKg"].Source != "manual_or_preserved" && fields["heightCm"].Source != "manual_or_preserved"
            && fields["birthDate"].Source == "patient_profile" && fields["sex"].Source == "patient_profile";
        return new(hasMeasured, withoutManual, fields, missing, results, patient);
    }
}
