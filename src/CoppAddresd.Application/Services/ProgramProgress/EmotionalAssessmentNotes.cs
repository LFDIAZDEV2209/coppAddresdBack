using System.Globalization;
using CoppAddresd.Application.DTOs.ProgramProgress;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>Conserva todos los datos reportados en las notas clínicas existentes.</summary>
public static class EmotionalAssessmentNotes
{
    public static string? Format(EmotionalAssessmentPayload? assessment)
    {
        if (assessment is null) return null;
        var lines = new List<string>();
        if (assessment.StressScore is { } stress) lines.Add($"Estrés: {stress}/10.");
        if (assessment.MotivationScore is { } motivation) lines.Add($"Motivación: {motivation}/10.");
        if (assessment.SleepMinutes is { } sleep)
        {
            var source = assessment.SleepSource == "wearable" ? "wearable" : "autorreporte";
            lines.Add($"Sueño: {sleep.ToString("0.##", CultureInfo.InvariantCulture)} minutos ({source}).");
            if (assessment.SleepSource == "wearable" && assessment.SleepRecordedAt is { } recorded)
                lines.Add($"Fecha del registro de sueño: {recorded.ToUniversalTime():O}.");
        }
        if (!string.IsNullOrWhiteSpace(assessment.Notes)) lines.Add(assessment.Notes.Trim());
        if (assessment.SleepSource == "unknown") lines.Add("Sueño: sin dato reportado.");
        if (assessment.SleepQuality is { } quality)
            lines.Add("Descanso percibido: " + (quality switch { "poor" => "poco reparador", "fair" => "regular", "restorative" => "reparador", _ => quality }) + ".");
        return lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
    }
}
