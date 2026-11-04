using System.Text.Json;
using System.Text.Json.Nodes;
using CoppAddresd.Application.Common;
using CoppAddresd.Domain.Entities.ProgramProgress;

namespace CoppAddresd.Application.Features.ProgramProgress.Assignment;

/// <summary>Motor puro: evalúa valores medidos y reglas editables por datos; la IA no elige contenido.</summary>
public sealed class AssignmentEngine
{
    public const string Version = "assignment-v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public AssignmentDecision Build(AssignmentProfileData data, AssignmentCatalog catalog,
        ProgramAssignmentSettings settings, DateTime capturedAt)
    {
        var snapshot = BuildSnapshot(data, catalog, capturedAt);
        return new(snapshot.ToJsonString(Json), Evaluate(snapshot, catalog, settings).ToJsonString(Json));
    }

    public JsonObject BuildSnapshot(AssignmentProfileData data, AssignmentCatalog catalog, DateTime capturedAt)
    {
        var facts = new JsonObject();
        if (data.Sex is not null) facts["sex"] = data.Sex.ToLowerInvariant() switch
        {
            "masculino" or "male" => "male", "femenino" or "female" => "female", _ => data.Sex,
        };
        if (data.Ethnicity is not null)
            facts["ethnicity"] = data.Ethnicity.Contains("ASIAN", StringComparison.OrdinalIgnoreCase) ? "asian" : data.Ethnicity;
        var biometrics = new JsonArray();
        foreach (var value in data.Biometrics.OrderBy(v => v.QuestionCode, StringComparer.Ordinal))
        {
            var valid = (!value.Minimum.HasValue || value.Value >= value.Minimum)
                && (!value.Maximum.HasValue || value.Value <= value.Maximum);
            biometrics.Add(JsonSerializer.SerializeToNode(new { value.EvaluationId, value.QuestionCode,
                value.Value, value.Unit, value.Minimum, value.Maximum, valid }, Json));
            if (!valid) continue;
            facts["questions." + value.QuestionCode] = value.Value;
            var canonical = Canonical(value.QuestionCode, value.Unit);
            if (canonical is not null) facts[canonical] = value.Value;
        }
        var heightRow = data.Biometrics.FirstOrDefault(v => Canonical(v.QuestionCode, v.Unit) == "height"
            && (!v.Minimum.HasValue || v.Value >= v.Minimum) && (!v.Maximum.HasValue || v.Value <= v.Maximum));
        if (heightRow is not null && heightRow.Unit == "cm") facts["height"] = heightRow.Value / 100m;
        if (facts["bmi"] is null && Number(facts["weight"]) is decimal weight && weight > 0
            && Number(facts["height"]) is decimal height && height > 0)
            facts["bmi"] = Math.Round(weight / (height * height), 2);
        var results = new JsonArray();
        foreach (var result in data.Results.OrderBy(r => r.TestCode, StringComparer.Ordinal)
            .ThenBy(r => r.Type, StringComparer.Ordinal).ThenBy(r => r.Code, StringComparer.Ordinal))
        {
            results.Add(JsonSerializer.SerializeToNode(result, Json));
            facts[$"results.{result.TestCode}.{result.Code}"] = result.Value;
        }
        var movement = data.Results.Where(r => r.TestCode == "movimiento" && r.Type == "subscale").ToArray();
        if (movement.Length > 0)
        {
            var subscales = new JsonObject();
            foreach (var r in movement) subscales[r.Code] = r.Value;
            facts["movement.subscales"] = subscales;
            // Sin máximos publicados no se inventa capacidad ni dificultad.
            if (movement.All(r => r.Maximum > 0))
                facts["movement.percentage"] = Math.Clamp(movement.Sum(r => r.Value) / movement.Sum(r => r.Maximum!.Value) * 100m, 0m, 100m);
        }
        var conditions = new JsonArray();
        foreach (var condition in catalog.Conditions.OrderBy(c => c.Code, StringComparer.Ordinal))
        {
            var criteria = JsonNode.Parse(condition.Criteria)!.AsObject();
            var assignable = criteria["assignable"]?.GetValue<bool>() != false;
            var active = condition.IsActive && assignable && Matches(criteria, facts);
            string? severity = null;
            if (assignable && active && criteria["severity"] is JsonArray levels)
                severity = levels.FirstOrDefault(x => x is JsonObject o && Matches(o, facts))?["code"]?.GetValue<string>();
            if (!assignable && criteria["scale"]?.GetValue<string>() is string scale)
            {
                var score = data.Results.FirstOrDefault(r => r.TestCode == scale && r.Type == "score");
                if (score is not null && criteria["severity"] is JsonArray cuts)
                    severity = cuts.LastOrDefault(x => Number(x?["minInclusive"]) <= score.Value
                        && (x?["maxExclusive"] is null || score.Value < Number(x["maxExclusive"])))?["code"]?.GetValue<string>();
            }
            facts[$"conditions.{condition.Code}.active"] = active;
            if (severity is not null) facts[$"conditions.{condition.Code}.severity"] = severity;
            conditions.Add(new JsonObject { ["code"] = condition.Code, ["active"] = active,
                ["assignable"] = assignable, ["severity"] = severity, ["sourceRef"] = condition.SourceRef,
                ["criteria"] = criteria.DeepClone() });
        }
        return new JsonObject { ["patientId"] = data.PatientId, ["batteryAssignmentId"] = data.BatteryAssignmentId,
            ["capturedAt"] = capturedAt, ["engineVersion"] = Version, ["results"] = results,
            ["biometrics"] = biometrics, ["facts"] = facts, ["conditions"] = conditions };
    }

    /// <summary>La propuesta depende solo del snapshot, el catálogo y la política operativa explícita.</summary>
    public JsonObject Evaluate(JsonObject snapshot, AssignmentCatalog catalog, ProgramAssignmentSettings settings)
    {
        var facts = snapshot["facts"]!.AsObject();
        AssignmentContent? nutrition = null, routine = null;
        var matches = new JsonArray();
        foreach (var rule in catalog.Rules.Where(r => r.IsActive)
            .OrderByDescending(r => r.Priority).ThenByDescending(r => r.Weight).ThenBy(r => r.Code, StringComparer.Ordinal))
        {
            if (rule.ConditionCode is not null && facts[$"conditions.{rule.ConditionCode}.active"]?.GetValue<bool>() != true) continue;
            var criteria = JsonNode.Parse(rule.Criteria)!.AsObject();
            if (!Matches(criteria, facts)) continue;
            if (!MatchesSubscaleRanges(criteria, snapshot["results"]!.AsArray())) continue;
            var target = JsonNode.Parse(rule.Target)!.AsObject();
            var plan = Resolve(catalog.NutritionPlans, target["nutritionPlanName"]?.GetValue<string>());
            var selectedRoutine = Resolve(catalog.Routines, target["routineName"]?.GetValue<string>());
            if (target["resolver"]?.GetValue<string>() == "movement-subscales")
            {
                if (Number(facts["movement.percentage"]) is not decimal percentage) continue;
                var difficulty = percentage <= settings.MovementLowMaxPercentage ? 1
                    : percentage <= settings.MovementMediumMaxPercentage ? 2 : 3;
                // Política v1 aprobada: tercios configurables y nombres del catálogo, sin IDs fijos.
                string[] names = difficulty switch
                {
                    1 => ["Caminata Activa", "Flexibilidad y Movilidad"],
                    2 => ["Full Body", "Core y Abdominales", "Core"],
                    _ => ["Cardio HIIT"],
                };
                if (True(facts["severeRetinopathy"]) || True(facts["uncontrolledHypertension"]))
                    names = ["Flexibilidad y Movilidad", "Caminata Activa"];
                selectedRoutine = names.Select(n => Resolve(catalog.Routines, n)).FirstOrDefault(r => r is not null);
            }
            // Un nombre que no existe no se reemplaza por contenido clínico inventado.
            if (plan is null && selectedRoutine is null) continue;
            var appliesNutrition = nutrition is null && plan is not null;
            var appliesRoutine = routine is null && selectedRoutine is not null;
            nutrition ??= plan;
            routine ??= selectedRoutine;
            matches.Add(new JsonObject { ["ruleId"] = rule.Id, ["ruleCode"] = rule.Code,
                ["conditionCode"] = rule.ConditionCode, ["targetNutritionPlanName"] = plan?.Name,
                ["targetRoutineName"] = selectedRoutine?.Name, ["priority"] = rule.Priority, ["weight"] = rule.Weight,
                ["reason"] = "Criterios satisfechos por los valores del snapshot; precedencia por prioridad, peso y código.",
                ["criteria"] = criteria.DeepClone(), ["target"] = target.DeepClone(),
                ["sourceRef"] = catalog.Conditions.FirstOrDefault(c => c.Code == rule.ConditionCode)?.SourceRef,
                ["appliesNutrition"] = appliesNutrition, ["appliesRoutine"] = appliesRoutine });
        }
        return new JsonObject { ["engineVersion"] = Version, ["nutritionPlan"] = JsonSerializer.SerializeToNode(nutrition, Json),
            ["routine"] = JsonSerializer.SerializeToNode(routine, Json), ["matchedRules"] = matches,
            ["useTemplateNutritionDefault"] = nutrition is null, ["useTemplateRoutineDefault"] = routine is null,
            ["movementPolicy"] = JsonSerializer.SerializeToNode(new { settings.MovementLowMaxPercentage, settings.MovementMediumMaxPercentage }, Json) };
    }

    private static AssignmentContent? Resolve(IReadOnlyList<AssignmentContent> catalog, string? name) =>
        name is null ? null : catalog.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Id).FirstOrDefault();

    private static bool MatchesSubscaleRanges(JsonObject criteria, JsonArray results)
    {
        var ranges = criteria["subscaleRanges"] as JsonArray;
        if (ranges is null) return true;
        return ranges.All(range => results.Any(r => r?["type"]?.GetValue<string>() == "subscale"
            && r["testCode"]?.GetValue<string>() == range?["testCode"]?.GetValue<string>()
            && r["code"]?.GetValue<string>() == range?["code"]?.GetValue<string>()
            && (range?["min"] is null || Number(r["value"]) >= Number(range["min"]))
            && (range?["max"] is null || Number(r["value"]) <= Number(range["max"]))));
    }

    private static bool Matches(JsonObject expression, JsonObject facts)
    {
        if (expression["all"] is JsonArray all && !all.All(x => x is JsonObject o && Matches(o, facts))) return false;
        if (expression["any"] is JsonArray any && !any.Any(x => x is JsonObject o && Matches(o, facts))) return false;
        if (expression["field"]?.GetValue<string>() is string field)
        {
            var actual = facts[field];
            var op = expression["op"]?.GetValue<string>();
            if (op == "exists") return (actual is not null) == (expression["value"]?.GetValue<bool>() ?? true);
            if (actual is null) return false;
            var expected = expression["value"];
            return op switch
            {
                "eq" => JsonNode.DeepEquals(actual, expected),
                "ne" => !JsonNode.DeepEquals(actual, expected),
                "gte" => Number(actual) is decimal n && Number(expected) is decimal v && n >= v,
                "gt" => Number(actual) is decimal n && Number(expected) is decimal v && n > v,
                "lte" => Number(actual) is decimal n && Number(expected) is decimal v && n <= v,
                "lt" => Number(actual) is decimal n && Number(expected) is decimal v && n < v,
                "in" => expected is JsonArray values && values.Any(v => JsonNode.DeepEquals(v, actual)),
                _ => false,
            };
        }
        // Un objeto con metadatos y sin predicado solo es válido para reglas sin filtros.
        return expression.ContainsKey("all") || expression.ContainsKey("any") || expression.Count == 0 || expression.ContainsKey("subscaleRanges");
    }

    private static bool True(JsonNode? node) => node?.ToJsonString() is "true" or "1";
    private static decimal? Number(JsonNode? node) => node is JsonValue value && value.TryGetValue<decimal>(out var n) ? n : null;
    private static string? Canonical(string code, string? unit) => (code.ToLowerInvariant(), unit) switch
    {
        ("peso" or "weight", "kg") => "weight",
        ("talla" or "height", "cm" or "m") => "height",
        ("imc" or "bmi", "kg/m²" or "kg/m2") => "bmi",
        ("cintura" or "waist", "cm") => "waist",
        ("gluc_ayunas" or "glucosa_ayunas" or "fpg", "mg/dL") => "fpg",
        ("a1c" or "hba1c", "%") => "a1c",
        ("ogtt2h", "mg/dL") => "ogtt2h",
        ("sistolica" or "pa_sistolica" or "systolic", "mmHg") => "systolic",
        ("diastolica" or "pa_diastolica" or "diastolic", "mmHg") => "diastolic",
        ("stop_bang" or "stopbang", _) => "stopBang",
        ("ahi", _) => "ahi",
        _ => null,
    };
}
