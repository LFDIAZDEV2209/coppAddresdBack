namespace CoppAddresd.Application.Features.Nutrition;

/// <summary>Separa el boilerplate interno de generación de las indicaciones que lee el paciente.</summary>
public static class PatientNutritionInstructions
{
    private static readonly HashSet<string> InternalLines = new(StringComparer.Ordinal)
    {
        "Propuesta determinista pendiente de revisión.",
        "Revisar porciones y alertas antes de aprobar.",
        "Propuesta determinista: revisa porciones y alertas antes de aprobar.",
        "Deterministic proposal pending review.",
        "Review portions and alerts before approval.",
        "Deterministic proposal: review portions and alerts before approval.",
    };

    public static string? ForPatient(string? source)
    {
        if (source is null) return null;
        var lines = source.Split('\n');
        var remaining = lines.Where(line => !InternalLines.Contains(line.TrimEnd('\r'))).ToArray();
        if (remaining.Length == lines.Length) return source;
        var result = string.Join('\n', remaining);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}
