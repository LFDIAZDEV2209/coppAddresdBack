namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Vocabularios cerrados del módulo de pacientes. Los valores se guardan como
/// códigos estables en inglés (compatibles con el dato legacy importado) y el
/// frontend muestra etiquetas en español. Los validadores usan estas listas
/// como whitelist; no son catálogos en BD porque son vocabularios pequeños y
/// estables.
/// </summary>
public static class PatientOptions
{
    /// <summary>Estado de tabaquismo (USCDI smoking status).</summary>
    public static readonly IReadOnlyList<string> SmokingStatuses =
        ["Never", "Former", "Current", "Unknown"];

    /// <summary>Consumo de alcohol.</summary>
    public static readonly IReadOnlyList<string> AlcoholStatuses =
        ["Never", "Occasional", "Moderate", "Heavy", "Former", "Unknown"];

    /// <summary>Nivel de ejercicio.</summary>
    public static readonly IReadOnlyList<string> ExerciseLevels =
        ["Sedentary", "Light", "Moderate", "Active", "Unknown"];

    /// <summary>Discapacidad declarada.</summary>
    public static readonly IReadOnlyList<string> Disabilities =
        ["None", "Visual", "Hearing", "Mobility", "Cognitive", "Multiple", "Other", "Unknown"];

    /// <summary>Historial de hospitalizaciones.</summary>
    public static readonly IReadOnlyList<string> HospitalizationHistories =
        ["None", "Once", "Multiple", "Unknown"];

    /// <summary>
    /// Historial de cirugías: las categorías base más los procedimientos
    /// legacy ya presentes en el dato importado (se conservan para no romper
    /// la edición de pacientes existentes).
    /// </summary>
    public static readonly IReadOnlyList<string> SurgeryHistories =
    [
        "None",
        "Appendectomy",
        "CABG",
        "Cardiac Catheterization",
        "Cholecystectomy",
        "C-Section",
        "Hernia Repair",
        "Hip Replacement",
        "Hysterectomy",
        "Knee Replacement",
        "Prostatectomy",
        "Other",
    ];

    /// <summary>Estado civil.</summary>
    public static readonly IReadOnlyList<string> MaritalStatuses =
        ["Single", "Married", "Divorced", "Widowed", "Separated", "Domestic Partnership", "Prefer Not to Say"];

    /// <summary>Estados del paciente.</summary>
    public static readonly IReadOnlyList<string> Statuses =
        ["Activo", "Inactivo"];

    public static bool IsAllowed(IReadOnlyList<string> allowed, string? value)
        => string.IsNullOrWhiteSpace(value) || allowed.Contains(value);

    public static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}