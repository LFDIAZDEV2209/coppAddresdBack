using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia del agregado de sala virtual y sus sesiones, y del registro de
/// webhooks procesados (clave de idempotencia del proveedor). La sala es 1:1 con
/// la cita; su ciclo de vida es independiente del de la cita, por eso vive en su
/// propio repositorio (los webhooks llegan por sid del proveedor, no por id de
/// cita).
/// </summary>
public interface IRoomRepository
{
    /// <summary>Sala de una cita (lectura sin tracking). <c>null</c> si aún no existe.</summary>
    Task<VirtualRoom?> GetByAppointmentIdAsync(
        Guid appointmentId,
        bool includeSessions = false,
        CancellationToken ct = default
    );

    /// <summary>Sala por su sid en el proveedor (lectura sin tracking; contexto de webhooks).</summary>
    Task<VirtualRoom?> GetByProviderRoomSidAsync(
        string providerRoomSid,
        bool includeSessions = false,
        CancellationToken ct = default
    );

    /// <summary>
    /// Salas de un conjunto de citas (lectura sin tracking) en una sola consulta:
    /// evita el N+1 al enriquecer listados con la ventana persistida de la sala.
    /// </summary>
    Task<IReadOnlyList<VirtualRoom>> ListByAppointmentIdsAsync(
        IReadOnlyCollection<Guid> appointmentIds,
        CancellationToken ct = default
    );

    /// <summary>Sala de una cita TRACKEADA con sus sesiones (para mutaciones del flujo de sala).</summary>
    Task<VirtualRoom?> GetForUpdateAsync(Guid appointmentId, CancellationToken ct = default);

    /// <summary>Sala TRACKEADA con sus sesiones, por sid del proveedor (mutaciones de webhook).</summary>
    Task<VirtualRoom?> GetForUpdateByProviderRoomSidAsync(
        string providerRoomSid,
        CancellationToken ct = default
    );

    /// <summary>
    /// Crea la sala. Idempotente: como la sala es 1:1 con la cita, una violación
    /// del índice único (carrera entre dos <c>join-token</c> concurrentes)
    /// significa que la sala ya existe → devuelve la existente.
    /// </summary>
    Task<VirtualRoom> AddAsync(VirtualRoom room, CancellationToken ct = default);

    /// <summary>Persiste cambios de una sala cargada con <c>GetForUpdate*</c> (sesiones nuevas como Added).</summary>
    Task UpdateAsync(VirtualRoom room, CancellationToken ct = default);

    /// <summary>
    /// Registra un webhook como procesado. Una violaciA3n del A-ndice A�nico
    /// (duplicado concurrente o reintento) se traduce a
    /// <see cref="CoppAddresd.Telemedicine.Domain.Exceptions.BusinessRuleViolationException"/>:
    /// en el flujo transaccional del webhook, el rollback tambiAcn deshace las
    /// mutaciones del duplicado perdedor.
    /// </summary>
    Task AddWebhookEventAsync(
        TelemedicineWebhookEvent webhookEvent,
        CancellationToken ct = default
    );

    /// <summary>
    /// Listado administrativo de sesiones de video, paginado y con su cita
    /// incluida (para resolver paciente/profesional en la UI admin). Filtros
    /// opcionales por cita y rango de inicio.
    /// </summary>
    Task<(IReadOnlyList<TelemedicineSession> Items, int Total)> ListSessionsAsync(
        Guid? appointmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>Cuenta las sesiones de video activas (KPI del dashboard admin).</summary>
    Task<int> CountActiveSessionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Cuenta las sesiones de video activas de las citas de un profesional
    /// (KPI del dashboard "Mis citas" del profesional, alcance por identidad).
    /// </summary>
    Task<int> CountActiveSessionsAsync(Guid professionalId, CancellationToken ct = default);
}
