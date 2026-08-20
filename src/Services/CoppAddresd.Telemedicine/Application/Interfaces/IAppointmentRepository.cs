using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia del agregado cita de telemedicina y su historial
/// (cancelaciones/reprogramaciones). La integridad del calendario se protege
/// en dos capas: verificación de solapamiento en aplicación (mensaje de error
/// amigable) y constraint en base de datos (índice único parcial + exclusión
/// de solapamiento) como garantía real ante concurrencia.
/// </summary>
public interface IAppointmentRepository
{
    /// <summary>Cita por id, lectura sin tracking (solo lectura).</summary>
    Task<TelemedicineAppointment?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>
    /// Cita por id TRACKEADA con su historial (cancelaciones/reprogramaciones),
    /// sala virtual y sesiones, para mutaciones: añadir hijos al agregado y
    /// persistir con SaveChanges (flujos de agendamiento y de sala/sesión).
    /// </summary>
    Task<TelemedicineAppointment?> GetForUpdateAsync(
        Guid id,
        CancellationToken ct = default);

    /// <summary>Crea la cita. Traduce conflictos de concurrencia (exclusión de solapamiento / request_id único) a una violación de regla de negocio.</summary>
    Task<TelemedicineAppointment> AddAsync(
        TelemedicineAppointment appointment,
        CancellationToken ct = default);

    /// <summary>Persiste cambios de una cita cargada con <see cref="GetForUpdateAsync"/> (con control de concurrencia xmin).</summary>
    Task UpdateAsync(
        TelemedicineAppointment appointment,
        CancellationToken ct = default);

    /// <summary>
    /// ¿Existe una cita ACTIVA del profesional que se solape con el rango
    /// <c>[start, end)</c>? <paramref name="excludeAppointmentId"/> excluye la
    /// propia cita (reprogramación). Verificación en aplicación; la garantía
    /// real ante carreras la da el constraint de exclusión en BD.
    /// </summary>
    Task<bool> HasActiveOverlapAsync(
        Guid professionalId,
        DateTimeOffset start,
        DateTimeOffset end,
        Guid? excludeAppointmentId = null,
        CancellationToken ct = default);

    /// <summary>Citas del profesional en el rango, ordenadas por inicio (agenda/calendario).</summary>
    Task<IReadOnlyList<TelemedicineAppointment>> ListByProfessionalAsync(
        Guid professionalId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);

    /// <summary>Citas de un paciente, de más reciente a más antigua (historial del paciente).</summary>
    Task<IReadOnlyList<TelemedicineAppointment>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default);
}
