using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Resultado de la creación transaccional de la alerta (con outbox).</summary>
public enum SosCreateResult
{
    /// <summary>Alerta creada con su outbox de deduplicación.</summary>
    Created,

    /// <summary>Otra petición (carrera) creó una fila con la misma clave de idempotencia.</summary>
    IdempotencyCollision,

    /// <summary>Otra petición (carrera) creó una alerta activa para el paciente.</summary>
    ActiveCollision,
}

/// <summary>Salida de <see cref="ISosAlertRepository.AddWithOutboxAsync"/>.</summary>
public sealed record SosCreateOutcome(SosCreateResult Result, SosAlert? ConflictingAlert);

/// <summary>
/// Persistencia del módulo SOS (change sos-panic-real). La creación de la
/// alerta y su outbox de deduplicación ocurren en UNA transacción (un solo
/// <c>SaveChanges</c>): las violaciones de unicidad se discriminan por
/// constraint de PostgreSQL para resolver carrera/replay sin consultas extra.
/// </summary>
public interface ISosAlertRepository
{
    /// <summary>Alerta por id (con paciente para el snapshot de contacto).</summary>
    Task<SosAlert?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Alerta por (patientId, idempotencyKey) — fast path del replay.</summary>
    Task<SosAlert?> GetByPatientAndKeyAsync(
        Guid patientId,
        string idempotencyKey,
        CancellationToken ct = default
    );

    /// <summary>Alerta activa del paciente (índice parcial), o null si no hay.</summary>
    Task<SosAlert?> GetActiveByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>Perfil del paciente (nombres + contacto de emergencia).</summary>
    Task<PatientProfile?> GetPatientProfileAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Crea la alerta y sus filas de dedupe (<c>app.notification_dedupe_keys</c>)
    /// en una sola transacción (outbox durable). Si otra petición concurrente
    /// viola el índice único de idempotencia o el parcial de alerta activa,
    /// NO lanza: recarga la fila conflictiva y devuelve el resultado para que
    /// el handler responda replay/409.
    /// </summary>
    /// <param name="alert">Alerta a persistir.</param>
    /// <param name="dedupeKeys">Claves de outbox (<c>sos:sms:{id}</c>, <c>sos:push:{id}:{userId}</c>).</param>
    Task<SosCreateOutcome> AddWithOutboxAsync(
        SosAlert alert,
        IReadOnlyCollection<string> dedupeKeys,
        CancellationToken ct = default
    );

    /// <summary>Persiste los cambios de la alerta (estados de canal, transición terminal).</summary>
    Task UpdateAsync(SosAlert alert, CancellationToken ct = default);

    /// <summary>
    /// Transición terminal atómica Activa → Atendida (compare-and-set en SQL:
    /// <c>UPDATE ... WHERE id = @id AND status = 'Activa'</c>). Devuelve false
    /// si otra transición ganó la carrera (el caller responde 409). Registra
    /// actor y marca de tiempo (REQ-SOS-05).
    /// </summary>
    Task<bool> AttendAsync(Guid alertId, Guid staffUserId, CancellationToken ct = default);

    /// <summary>
    /// Transición terminal atómica Activa → Cancelada (idem attend).</summary>
    Task<bool> CancelAsync(Guid alertId, Guid cancelledByUserId, CancellationToken ct = default);

    /// <summary>
    /// Listado paginado de alertas para el staff ERP. <paramref name="patientIds"/>
    /// <c>null</c> = sin filtro (bypass de roles de administración org/clínica);
    /// lista vacía = denegar (0 filas — un clínico sin alcance no ve nada).
    /// </summary>
    Task<(IReadOnlyList<SosAlertListItemDto> Items, int Total)> ListForStaffAsync(
        IReadOnlyCollection<Guid>? patientIds,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );

    /// <summary>
    /// Ids de paciente alcanzables por el staff según el scope D5 (unión de
    /// asignación directa activa, pacientes de la clínica activa y de la
    /// organización activa, DISTINCT). Devuelve lista (posiblemente vacía =
    /// denegar); el bypass de roles lo decide el caller antes de llamar.
    /// </summary>
    Task<IReadOnlyList<Guid>> ResolveScopedPatientIdsAsync(
        Guid? professionalId,
        Guid? activeClinicId,
        Guid? activeOrganizationId,
        CancellationToken ct = default
    );

    /// <summary>
    /// Ids de usuario (auth.users) de los profesionales con asignación activa
    /// al paciente (<c>app.patient_professionals</c> → erp.employees.user_id):
    /// destinatarios del push FCM.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAssignedStaffUserIdsAsync(
        Guid patientId,
        CancellationToken ct = default
    );

    /// <summary>
    /// ¿El staff (aud=erp) tiene scope clínico sobre el paciente? TRUE si:
    /// asignación directa activa en <c>patient_professionals</c>, el paciente
    /// pertenece a la clínica activa del contexto, o a la organización activa
    /// (D5). <paramref name="professionalId"/> null salta el chequeo directo.
    /// </summary>
    Task<bool> IsStaffScopedToPatientAsync(
        Guid patientId,
        Guid? professionalId,
        Guid? activeClinicId,
        Guid? activeOrganizationId,
        CancellationToken ct = default
    );
}
