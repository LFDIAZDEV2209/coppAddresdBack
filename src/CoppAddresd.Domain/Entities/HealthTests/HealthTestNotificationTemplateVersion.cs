using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Domain.Entities.HealthTests;

/// <summary>
/// Snapshot inmutable del contenido de una
/// <see cref="HealthTestNotificationTemplate"/> (SPEC A13). Se genera al crear o
/// editar la plantilla y permite historial y restauración desde el Template
/// Studio del ERP. Conserva el contenido bilingüe (español obligatorio, inglés
/// opcional).
/// </summary>
public sealed class HealthTestNotificationTemplateVersion
{
    public Guid Id { get; set; }

    public Guid TemplateId { get; set; }

    /// <summary>Número de versión incremental por plantilla (1, 2, 3...).</summary>
    public int Version { get; set; }

    public string NameEs { get; set; } = default!;

    public string? NameEn { get; set; }

    public NotificationChannel Channel { get; set; }

    public HealthTestSeverity? Severity { get; set; }

    public string? TestCategory { get; set; }

    public string? IndicatorCode { get; set; }

    public string? SubjectEs { get; set; }

    public string? SubjectEn { get; set; }

    public string BodyTemplateEs { get; set; } = default!;

    public string? BodyTemplateEn { get; set; }

    /// <summary>Nota opcional del cambio (ej. "ajuste de tono").</summary>
    public string? Note { get; set; }

    /// <summary>Usuario (auth.users) que generó la versión (null = seeder/sistema).</summary>
    public Guid? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public HealthTestNotificationTemplate? Template { get; set; }
}
