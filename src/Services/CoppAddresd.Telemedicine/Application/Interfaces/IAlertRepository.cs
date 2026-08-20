using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia de la bandeja de alertas (<c>tele.telemedicine_alerts</c>).
/// Las alertas son la materialización de eventos de dominio; el canal de
/// entrega (email/push/SMS) es responsabilidad futura y vive fuera de este
/// agregado. Lecturas típicas: bandeja de un destinatario y recuento de no
/// leídas (badge), cubiertas por los índices de <see cref="TelemedicineAlert"/>.
/// </summary>
public interface IAlertRepository
{
    /// <summary>Crea una o varias alertas en una sola operación (mismo DbContext/transacción que el flujo que las emite).</summary>
    Task AddRangeAsync(IReadOnlyList<TelemedicineAlert> alerts, CancellationToken ct = default);

    /// <summary>
    /// Bandeja de un destinatario (usuario de <c>auth.users</c>): paginada,
    /// no leídas primero y luego por fecha descendente. La proyección evita
    /// cargar entidades completas.
    /// </summary>
    Task<(IReadOnlyList<TelemedicineAlert> Items, int Total)> ListForUserAsync(
        Guid recipientUserId,
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Cuenta las no leídas de un destinatario (badge de la bandeja).</summary>
    Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken ct = default);

    /// <summary>
    /// Vista administrativa: todas las alertas, paginadas y sin filtrar por
    /// destinatario (los admins no tienen una identidad profesional/paciente).
    /// Filtrar por clínica/organización es una evolución futura (Fase 7/11).
    /// </summary>
    Task<(IReadOnlyList<TelemedicineAlert> Items, int Total)> ListAllAsync(
        bool unreadOnly,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Marca una alerta como leída por id, sin restricción de destinatario (solo para admins).</summary>
    Task<bool> MarkReadByIdAsync(Guid alertId, CancellationToken ct = default);

    /// <summary>Marca TODAS las no leídas como leídas (vista administrativa global). Devuelve cuántas se marcaron.</summary>
    Task<int> MarkAllReadGlobalAsync(CancellationToken ct = default);

    /// <summary>Cuenta las no leídas de TODA la bandeja (badge de la vista administrativa).</summary>
    Task<int> CountUnreadGlobalAsync(CancellationToken ct = default);

    /// <summary>
    /// Marca una alerta como leída SOLO si pertenece al destinatario (evita que
    /// un usuario marque alertas ajenas). Devuelve si existía para ese usuario.
    /// </summary>
    Task<bool> MarkReadAsync(Guid alertId, Guid recipientUserId, CancellationToken ct = default);

    /// <summary>Marca todas las no leídas de un destinatario como leídas. Devuelve cuántas se marcaron.</summary>
    Task<int> MarkAllReadAsync(Guid recipientUserId, CancellationToken ct = default);
}
