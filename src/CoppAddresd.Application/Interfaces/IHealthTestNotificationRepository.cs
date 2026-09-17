using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Persistencia del módulo de notificaciones de alertas de tests de salud
/// (SPEC A13): plantillas editables + historial de versiones, log de entregas y
/// agregados para los gráficos del ERP.
/// </summary>
public interface IHealthTestNotificationRepository
{
    // --- Plantillas ---

    Task<IReadOnlyList<HealthTestNotificationTemplate>> ListTemplatesAsync(
        NotificationChannel? channel,
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    Task<int> CountTemplatesAsync(
        NotificationChannel? channel,
        string? search,
        bool? isActive,
        CancellationToken ct = default
    );

    Task<HealthTestNotificationTemplate?> GetTemplateByIdAsync(
        Guid id,
        bool includeVersions = false,
        CancellationToken ct = default
    );

    Task<HealthTestNotificationTemplate?> GetTemplateByCodeAsync(
        string code,
        CancellationToken ct = default
    );

    Task AddTemplateAsync(HealthTestNotificationTemplate template, CancellationToken ct = default);

    Task UpdateTemplateAsync(HealthTestNotificationTemplate template, CancellationToken ct = default);

    Task AddTemplateVersionAsync(
        HealthTestNotificationTemplateVersion version,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<HealthTestNotificationTemplateVersion>> ListTemplateVersionsAsync(
        Guid templateId,
        CancellationToken ct = default
    );

    Task<HealthTestNotificationTemplateVersion?> GetTemplateVersionAsync(
        Guid templateId,
        int version,
        CancellationToken ct = default
    );

    Task<int> GetNextTemplateVersionNumberAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>Cantidad de envíos históricos por plantilla (para "usos").</summary>
    Task<IReadOnlyDictionary<Guid, int>> GetTemplateUsageCountsAsync(CancellationToken ct = default);

    // --- Log de entregas ---

    Task AddNotificationsAsync(
        IReadOnlyList<HealthTestNotification> notifications,
        CancellationToken ct = default
    );

    Task<(IReadOnlyList<HealthTestNotification> Items, int Total)> ListNotificationsAsync(
        Guid? alertId,
        Guid? patientId,
        NotificationChannel? channel,
        NotificationStatus? status,
        DateTime? from,
        DateTime? to,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    // --- Datos de apoyo para el envío ---

    Task<IReadOnlyList<HealthTestAlert>> ListAlertsByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default
    );

    Task<IReadOnlyDictionary<Guid, PatientProfile>> GetPatientsByIdsAsync(
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct = default
    );

    // --- Agregados para gráficos ---

    Task<IReadOnlyDictionary<string, int>> CountAlertsBySeverityAsync(
        Guid? professionalId,
        CancellationToken ct = default
    );

    Task<IReadOnlyDictionary<string, int>> CountAlertsByStatusAsync(
        Guid? professionalId,
        CancellationToken ct = default
    );

    Task<IReadOnlyDictionary<string, int>> CountAlertsByIndicatorAsync(
        Guid? professionalId,
        int top,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<(DateTime Date, int Count)>> CountAlertsByDayAsync(
        Guid? professionalId,
        int days,
        CancellationToken ct = default
    );

    Task<IReadOnlyDictionary<string, int>> CountNotificationsByChannelAsync(
        Guid? professionalId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default
    );

    Task<IReadOnlyDictionary<string, int>> CountNotificationsByStatusAsync(
        Guid? professionalId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default
    );

    Task<IReadOnlyList<(DateTime Date, int Count)>> CountNotificationsByDayAsync(
        Guid? professionalId,
        int days,
        CancellationToken ct = default
    );
}
