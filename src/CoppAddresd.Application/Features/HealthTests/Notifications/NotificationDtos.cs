using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

// --- Plantillas ---

public sealed record HealthTestNotificationTemplateDto(
    Guid Id,
    string Code,
    string NameEs,
    string? NameEn,
    NotificationChannel Channel,
    HealthTestSeverity? Severity,
    string? TestCategory,
    string? IndicatorCode,
    string? SubjectEs,
    string? SubjectEn,
    string BodyTemplateEs,
    string? BodyTemplateEn,
    bool IsActive,
    int UsageCount,
    int VersionCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt
)
{
    public static HealthTestNotificationTemplateDto FromEntity(
        HealthTestNotificationTemplate e,
        int usageCount = 0,
        int versionCount = 0
    ) =>
        new(
            e.Id,
            e.Code,
            e.NameEs,
            e.NameEn,
            e.Channel,
            e.Severity,
            e.TestCategory,
            e.IndicatorCode,
            e.SubjectEs,
            e.SubjectEn,
            e.BodyTemplateEs,
            e.BodyTemplateEn,
            e.IsActive,
            usageCount,
            versionCount,
            e.CreatedAt,
            e.UpdatedAt
        );
}

public sealed record HealthTestNotificationTemplateVersionDto(
    Guid Id,
    Guid TemplateId,
    int Version,
    string NameEs,
    string? NameEn,
    NotificationChannel Channel,
    HealthTestSeverity? Severity,
    string? TestCategory,
    string? IndicatorCode,
    string? SubjectEs,
    string? SubjectEn,
    string BodyTemplateEs,
    string? BodyTemplateEn,
    string? Note,
    DateTime CreatedAt
)
{
    public static HealthTestNotificationTemplateVersionDto FromEntity(
        HealthTestNotificationTemplateVersion v
    ) =>
        new(
            v.Id,
            v.TemplateId,
            v.Version,
            v.NameEs,
            v.NameEn,
            v.Channel,
            v.Severity,
            v.TestCategory,
            v.IndicatorCode,
            v.SubjectEs,
            v.SubjectEn,
            v.BodyTemplateEs,
            v.BodyTemplateEn,
            v.Note,
            v.CreatedAt
        );
}

// --- Log de entregas ---

public sealed record HealthTestNotificationDto(
    Guid Id,
    Guid? AlertId,
    Guid? PatientId,
    string? PatientName,
    NotificationChannel Channel,
    NotificationLanguage Language,
    Guid? TemplateId,
    string? TemplateName,
    string Recipient,
    string RenderedBody,
    NotificationStatus Status,
    string Provider,
    string? ProviderMessageId,
    string? Error,
    DateTime CreatedAt,
    DateTime? SentAt
)
{
    public static HealthTestNotificationDto FromEntity(HealthTestNotification n) =>
        new(
            n.Id,
            n.AlertId,
            n.PatientId,
            n.Patient is null ? null : $"{n.Patient.FirstName} {n.Patient.LastName}".Trim(),
            n.Channel,
            n.Language,
            n.TemplateId,
            n.Language == NotificationLanguage.en
                ? (n.Template?.NameEn ?? n.Template?.NameEs)
                : n.Template?.NameEs,
            n.Recipient,
            n.RenderedBody,
            n.Status,
            n.Provider,
            n.ProviderMessageId,
            n.Error,
            n.CreatedAt,
            n.SentAt
        );
}

// --- Resultado de un envío (masivo o de prueba) ---

public sealed record NotifyAlertItemResultDto(
    Guid? AlertId,
    Guid? PatientId,
    string? PatientName,
    NotificationChannel Channel,
    NotificationLanguage Language,
    NotificationStatus Status,
    string? Reason,
    string RenderedBody,
    string Recipient
);

public sealed record NotifyAlertsResultDto(
    int Requested,
    int Sent,
    int Skipped,
    int Failed,
    bool Preview,
    IReadOnlyList<NotifyAlertItemResultDto> Items
);

// --- Gráficos ---

public sealed record HealthTestChartPointDto(string Label, int Value);

public sealed record HealthTestNotificationChartsDto(
    IReadOnlyDictionary<string, int> AlertsBySeverity,
    IReadOnlyDictionary<string, int> AlertsByStatus,
    IReadOnlyDictionary<string, int> AlertsByIndicator,
    IReadOnlyList<HealthTestChartPointDto> AlertsByDay,
    IReadOnlyDictionary<string, int> NotificationsByChannel,
    IReadOnlyDictionary<string, int> NotificationsByStatus,
    IReadOnlyList<HealthTestChartPointDto> NotificationsByDay
);

// --- Requests ---

public sealed record CreateNotificationTemplateRequest(
    string Code,
    string NameEs,
    NotificationChannel Channel,
    string BodyTemplateEs,
    string? NameEn = null,
    string? BodyTemplateEn = null,
    HealthTestSeverity? Severity = null,
    string? TestCategory = null,
    string? IndicatorCode = null,
    string? SubjectEs = null,
    string? SubjectEn = null,
    bool IsActive = true,
    string? Note = null
);

public sealed record UpdateNotificationTemplateRequest(
    string NameEs,
    NotificationChannel Channel,
    string BodyTemplateEs,
    string? NameEn = null,
    string? BodyTemplateEn = null,
    HealthTestSeverity? Severity = null,
    string? TestCategory = null,
    string? IndicatorCode = null,
    string? SubjectEs = null,
    string? SubjectEn = null,
    bool IsActive = true,
    string? Note = null
);

public sealed record CloneNotificationTemplateRequest(
    string Code,
    string? NameEs = null,
    string? NameEn = null
);

public sealed record SendTestNotificationRequest(
    NotificationChannel Channel,
    NotificationLanguage Language = NotificationLanguage.es,
    string? PhoneNumber = null,
    Guid? PatientId = null,
    string? BodyOverride = null
);

public sealed record NotifyAlertsRequest(
    IReadOnlyList<Guid> AlertIds,
    IReadOnlyList<NotificationChannel> Channels,
    Guid? TemplateId = null,
    string? BodyOverride = null,
    NotificationLanguage Language = NotificationLanguage.es,
    bool Preview = false
);

// --- Vista previa real de una plantilla ---

/// <summary>
/// Resultado de renderizar una plantilla con datos reales. <c>MissingPlaceholders</c>
/// lista las claves usadas por la plantilla para las que el contexto no tiene dato,
/// de modo que la UI pueda avisar antes de enviar.
/// </summary>
public sealed record NotificationTemplatePreviewDto(
    Guid TemplateId,
    string TemplateName,
    NotificationChannel Channel,
    NotificationLanguage Language,
    Guid? AlertId,
    Guid? PatientId,
    string BodyTemplate,
    string RenderedBody,
    string? Recipient,
    bool IsReachable,
    string? SkipReason,
    bool UsedFallbackLanguage,
    IReadOnlyList<string> MissingPlaceholders
);
