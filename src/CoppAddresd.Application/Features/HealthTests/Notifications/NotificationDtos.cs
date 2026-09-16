using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Features.HealthTests.Notifications;

// --- Plantillas ---

public sealed record HealthTestNotificationTemplateDto(
    Guid Id,
    string Code,
    string Name,
    NotificationChannel Channel,
    HealthTestSeverity? Severity,
    string? TestCategory,
    string? IndicatorCode,
    string? Subject,
    string BodyTemplate,
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
            e.Name,
            e.Channel,
            e.Severity,
            e.TestCategory,
            e.IndicatorCode,
            e.Subject,
            e.BodyTemplate,
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
    string Name,
    NotificationChannel Channel,
    HealthTestSeverity? Severity,
    string? TestCategory,
    string? IndicatorCode,
    string? Subject,
    string BodyTemplate,
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
            v.Name,
            v.Channel,
            v.Severity,
            v.TestCategory,
            v.IndicatorCode,
            v.Subject,
            v.BodyTemplate,
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
            n.TemplateId,
            n.Template?.Name,
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
    string Name,
    NotificationChannel Channel,
    string BodyTemplate,
    HealthTestSeverity? Severity = null,
    string? TestCategory = null,
    string? IndicatorCode = null,
    string? Subject = null,
    bool IsActive = true,
    string? Note = null
);

public sealed record UpdateNotificationTemplateRequest(
    string Name,
    NotificationChannel Channel,
    string BodyTemplate,
    HealthTestSeverity? Severity = null,
    string? TestCategory = null,
    string? IndicatorCode = null,
    string? Subject = null,
    bool IsActive = true,
    string? Note = null
);

public sealed record CloneNotificationTemplateRequest(string Code, string? Name = null);

public sealed record SendTestNotificationRequest(
    NotificationChannel Channel,
    string? PhoneNumber = null,
    Guid? PatientId = null,
    string? BodyOverride = null
);

public sealed record NotifyAlertsRequest(
    IReadOnlyList<Guid> AlertIds,
    IReadOnlyList<NotificationChannel> Channels,
    Guid? TemplateId = null,
    string? BodyOverride = null,
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
    Guid? AlertId,
    Guid? PatientId,
    string BodyTemplate,
    string RenderedBody,
    string? Recipient,
    bool IsReachable,
    string? SkipReason,
    IReadOnlyList<string> MissingPlaceholders
);
