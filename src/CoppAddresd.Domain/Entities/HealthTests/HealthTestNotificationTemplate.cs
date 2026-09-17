using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Plantilla editable de notificación a pacientes por alertas de tests de salud
/// (SPEC A13). Se administra desde el "Template Studio" del ERP: canal
/// (<c>community</c>/<c>sms</c>), alcance opcional (severidad, categoría de test,
/// indicador) y cuerpo bilingüe con placeholders (<c>[paciente]</c>,
/// <c>[test]</c>, <c>[indicador]</c>, <c>[valor]</c>, <c>[severidad]</c>,
/// <c>[fecha]</c>...). El español es obligatorio; el inglés es opcional y la UI
/// avisa cuando falta la traducción. Cada cambio de contenido genera una
/// <see cref="HealthTestNotificationTemplateVersion"/> para historial y
/// restauración.
/// </summary>
public sealed class HealthTestNotificationTemplate
{
    public Guid Id { get; set; }

    /// <summary>Código estable único (ej. <c>HT_ALERTA_ORP_ALTA</c>).</summary>
    public string Code { get; set; } = default!;

    /// <summary>Nombre en español (obligatorio).</summary>
    public string NameEs { get; set; } = default!;

    /// <summary>Nombre en inglés (null = falta traducción).</summary>
    public string? NameEn { get; set; }

    public NotificationChannel Channel { get; set; } = NotificationChannel.community;

    /// <summary>Severidad objetivo (null = aplica a cualquier severidad).</summary>
    public HealthTestSeverity? Severity { get; set; }

    /// <summary>Categoría de test objetivo (null = cualquiera).</summary>
    public string? TestCategory { get; set; }

    /// <summary>Código de indicador objetivo (null = cualquiera).</summary>
    public string? IndicatorCode { get; set; }

    /// <summary>Asunto opcional en español (para canales que lo soporten).</summary>
    public string? SubjectEs { get; set; }

    /// <summary>Asunto opcional en inglés (null = se usa el español).</summary>
    public string? SubjectEn { get; set; }

    /// <summary>Cuerpo en español con placeholders (obligatorio).</summary>
    public string BodyTemplateEs { get; set; } = default!;

    /// <summary>Cuerpo en inglés con placeholders (null = falta traducción).</summary>
    public string? BodyTemplateEn { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<HealthTestNotificationTemplateVersion> Versions { get; set; } = [];
}
