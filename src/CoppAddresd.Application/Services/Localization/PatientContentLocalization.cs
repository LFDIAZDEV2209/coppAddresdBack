using System.Text.Json.Nodes;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;

namespace CoppAddresd.Application.Services.Localization;

public enum PatientContentProfile { NutritionPlan, ProgramSnapshot, Notifications, AssessmentAssignments, AssessmentBatteries, AssessmentDetail, AssessmentResults, AssessmentHistory, Measurements, Specialties, ProfessionalsCatalog }

/// <summary>Lista explícita de campos de presentación. Nunca recorre respuestas libres, códigos o identidades.</summary>
public sealed class PatientContentLocalization(IContentTranslationService translations)
{
    public async Task<JsonNode> TranslateAsync(JsonNode original, PatientContentProfile profile, string language, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(original);
        var root = original.DeepClone();
        if (language != "en") return root;
        var values = new List<JsonValue>();
        foreach (var path in Paths(profile)) Collect(root, path.Split('.'), 0, values);
        var pending = new List<(JsonNode Parent, string? Property, int Index, string Source)>();
        foreach (var value in values.Distinct())
        {
            if (!value.TryGetValue<string>(out var source) || string.IsNullOrWhiteSpace(source)) continue;
            if (value.Parent is JsonObject obj)
            {
                var property = obj.First(pair => ReferenceEquals(pair.Value, value)).Key;
                // Una traducción clínica ya aprobada prevalece sobre la traducción de presentación.
                if (property == "notes" && obj["notesEn"] is JsonValue approved && approved.TryGetValue<string>(out var english) && !string.IsNullOrWhiteSpace(english))
                { obj[property] = english; continue; }
                pending.Add((obj, property, -1, source));
            }
            else if (value.Parent is JsonArray array) pending.Add((array, null, array.IndexOf(value), source));
        }
        if (pending.Count == 0) return root;
        var map = await translations.TranslateAsync(pending.Select(x => x.Source).Distinct(StringComparer.Ordinal).ToArray(), language, ct);
        ct.ThrowIfCancellationRequested();
        if (map is null) throw new ContentTranslationUnavailableException();
        foreach (var item in pending)
        {
            if (!map.TryGetValue(item.Source, out var translated) || string.IsNullOrWhiteSpace(translated))
                throw new ContentTranslationUnavailableException();
            if (item.Parent is JsonObject obj) obj[item.Property!] = translated;
            else ((JsonArray)item.Parent)[item.Index] = translated;
        }
        return root;
    }

    private static void Collect(JsonNode? node, string[] path, int index, List<JsonValue> values)
    {
        if (node is null) return;
        if (index == path.Length) { if (node is JsonValue value) values.Add(value); return; }
        if (path[index] == "*" && node is JsonArray array)
        { foreach (var child in array) Collect(child, path, index + 1, values); }
        else if (node is JsonObject obj) Collect(obj[path[index]], path, index + 1, values);
    }

    private static IEnumerable<string> Paths(PatientContentProfile profile) => profile switch
    {
        PatientContentProfile.NutritionPlan => ["name", "description", "targetCondition", "allergens", "mealTiming", "meals.*.description", "meals.*.foods", "meals.*.notes", "days.*.meals.*.description", "days.*.meals.*.foods", "days.*.meals.*.notes"],
        PatientContentProfile.ProgramSnapshot => ["template.name", "todayTasks.*.content.title", "todayTasks.*.content.description", "todayTasks.*.content.nutritionPlanName", "todayTasks.*.content.exerciseRoutineName", "todayTasks.*.content.nutritionMeals.*.description", "todayTasks.*.content.nutritionMeals.*.foods", "todayTasks.*.content.nutritionMeals.*.notes", "todayTasks.*.content.chapters.*.label", "todayTasks.*.content.takeaways.*", "todayTasks.*.content.exercises.*.name", "todayTasks.*.content.exercises.*.description", "todayTasks.*.content.exercises.*.targetMuscle", "todayTasks.*.content.exercises.*.equipment", "todayTasks.*.content.exercises.*.tips"],
        PatientContentProfile.Notifications => ["data.*.title", "data.*.message", "items.*.title", "items.*.message"],
        PatientContentProfile.AssessmentAssignments => ["*.testName", "*.testCategory", "*.notes"],
        PatientContentProfile.AssessmentBatteries => ["*.batteryName"],
        PatientContentProfile.AssessmentDetail => ["name", "questions.*.section", "questions.*.text", "questions.*.minLabel", "questions.*.maxLabel", "questions.*.hint", "questions.*.options.*.text", "ranges.*.label"],
        PatientContentProfile.AssessmentResults => ["*.label", "*.qualifier"],
        PatientContentProfile.AssessmentHistory => ["*.testName", "*.results.*.label", "*.results.*.qualifier"],
        PatientContentProfile.Measurements => ["items.*.metricName"],
        PatientContentProfile.Specialties => ["*.name", "*.category", "*.description"],
        PatientContentProfile.ProfessionalsCatalog => ["data.*.professionalTypeDisplayName", "data.*.specialties.*.name"],
        _ => [],
    };
}
