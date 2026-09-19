using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia de la pre-consulta del paciente (F4). Es 1:1 con la cita
/// (índice único en <c>appointment_id</c>): la escritura es un upsert y la
/// creación concurrente se resuelve devolviendo la fila existente TRACKEADA.
/// La tabla es PHI: las mutaciones usan transacción explícita corta para que el
/// trigger de auditoría reciba el actor del JWT.
/// </summary>
public interface IPreVisitIntakeRepository
{
    /// <summary>Pre-consulta de una cita (lectura sin tracking). <c>null</c> si no existe.</summary>
    Task<PreVisitIntake?> GetByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    /// <summary>Pre-consulta de una cita TRACKEADA (para el upsert).</summary>
    Task<PreVisitIntake?> GetForUpdateByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Crea la pre-consulta. Idempotente ante concurrencia: una violación del
    /// índice único significa que ya existe → devuelve la existente TRACKEADA
    /// (el handler aplica sus cambios sobre ella).
    /// </summary>
    Task<PreVisitIntake> AddAsync(PreVisitIntake intake, CancellationToken ct = default);

    /// <summary>Persiste cambios de una pre-consulta cargada con <c>GetForUpdateByAppointmentIdAsync</c>.</summary>
    Task UpdateAsync(PreVisitIntake intake, CancellationToken ct = default);
}
