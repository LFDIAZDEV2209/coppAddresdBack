using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Plantilla editable de notificación a pacientes por alertas de tests de salud
/// (SPEC A13). Se administra desde el "Template Studio" del ERP: canal
/// (<c>community</c>/<c>sms</c>), alcance opcional (severidad, categoría de test,
/// indicador) y cuerpo con placeholders (<c>{paciente}</c>, <c>{test}</c>,
/// <c>{indicador}</c>, <c>{valor}</c>, <c>{severidad}</c>, <c>{fecha}</c>...).
/// Cada cambio de contenido genera una
/// <see cref="HealthTestNotificationTemplateVersion"/> para historial y
/// restauración.
/// </summary>
public sealed class HealthTestNotificationTemplate
{
    public Guid Id { get; set; }

    /// <summary>Código estable único (ej. <c>HT_ALERTA_ORP_ALTA</c>).</summary>
    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public NotificationChannel Channel { get; set; } = NotificationChannel.community;

    /// <summary>Severidad objetivo (null = aplica a cualquier severidad).</summary>
    public HealthTestSeverity? Severity { get; set; }

    /// <summary>Categoría de test objetivo (null = cualquiera).</summary>
    public string? TestCategory { get; set; }

    /// <summary>Código de indicador objetivo (null = cualquiera).</summary>
    public string? IndicatorCode { get; set; }

    /// <summary>Asunto opcional (para canales que lo soporten).</summary>
    public string? Subject { get; set; }

    /// <summary>Cuerpo con placeholders, en español por convención del repo.</summary>
    public string BodyTemplate { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<HealthTestNotificationTemplateVersion> Versions { get; set; } = [];
}
