using System.Text.RegularExpressions;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

/// <summary>
/// Datos disponibles para renderizar una plantilla de notificación (SPEC A13).
/// Un valor <c>null</c> se resuelve a cadena vacía al renderizar.
/// </summary>
public sealed record HealthTestNotificationRenderContext(
    string? PatientName = null,
    string? PatientDocument = null,
    string? TestName = null,
    string? IndicatorName = null,
    string? Value = null,
    string? Threshold = null,
    HealthTestSeverity? Severity = null,
    string? RecommendedAction = null,
    string? ProfessionalName = null,
    DateTime? Date = null
);

/// <summary>
/// Sustituye placeholders <c>{clave}</c> en las plantillas de notificación por
/// los datos del contexto (SPEC A13).
/// </summary>
public interface IHealthTestTemplateRenderer
{
    /// <summary>Placeholders soportados (sin llaves).</summary>
    IReadOnlyList<string> SupportedPlaceholders { get; }

    /// <summary>Claves usadas en una plantilla (minúsculas, ordenadas).</summary>
    IReadOnlyList<string> ExtractPlaceholders(string? template);

    string Render(string template, HealthTestNotificationRenderContext context);
}

/// <summary>
/// Implementación pura (sin dependencias de infraestructura) del renderizador.
/// Los placeholders desconocidos se dejan intactos para hacer visible el error
/// de tipeo; los conocidos sin dato se resuelven a cadena vacía.
/// </summary>
public sealed partial class HealthTestTemplateRenderer : IHealthTestTemplateRenderer
{
    public static readonly IReadOnlyList<string> Placeholders =
    [
        "paciente",
        "documento",
        "test",
        "indicador",
        "valor",
        "umbral",
        "severidad",
        "accion",
        "profesional",
        "fecha",
    ];

    [GeneratedRegex(@"\{([a-zA-Z0-9_]+)\}")]
    private static partial Regex PlaceholderRegex();

    public IReadOnlyList<string> SupportedPlaceholders => Placeholders;

    public IReadOnlyList<string> ExtractPlaceholders(string? template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return [];
        }

        return PlaceholderRegex()
            .Matches(template)
            .Select(m => m.Groups[1].Value.ToLowerInvariant())
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
    }

    public string Render(string template, HealthTestNotificationRenderContext context)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return string.Empty;
        }

        var values = BuildValues(context);
        return PlaceholderRegex()
            .Replace(template, match =>
            {
                var key = match.Groups[1].Value.ToLowerInvariant();
                return values.TryGetValue(key, out var value) ? value : match.Value;
            });
    }

    private static Dictionary<string, string> BuildValues(HealthTestNotificationRenderContext c) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["paciente"] = c.PatientName?.Trim() ?? string.Empty,
            ["documento"] = c.PatientDocument?.Trim() ?? string.Empty,
            ["test"] = c.TestName?.Trim() ?? string.Empty,
            ["indicador"] = c.IndicatorName?.Trim() ?? string.Empty,
            ["valor"] = c.Value?.Trim() ?? string.Empty,
            ["umbral"] = c.Threshold?.Trim() ?? string.Empty,
            ["severidad"] = SeverityLabel(c.Severity),
            ["accion"] = c.RecommendedAction?.Trim() ?? string.Empty,
            ["profesional"] = c.ProfessionalName?.Trim() ?? string.Empty,
            ["fecha"] = (c.Date ?? DateTime.UtcNow).ToString("dd/MM/yyyy"),
        };

    /// <summary>Etiqueta es-CO de severidad (misma que usa la UI del ERP).</summary>
    public static string SeverityLabel(HealthTestSeverity? severity) =>
        severity switch
        {
            HealthTestSeverity.low => "baja",
            HealthTestSeverity.moderate => "media",
            HealthTestSeverity.high => "alta",
            HealthTestSeverity.critical => "crítica",
            _ => string.Empty,
        };
}
