using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia del encuentro clínico de una cita. El encuentro es 1:1 con la
/// cita (índice único en <c>appointment_id</c>); su creación es perezosa (se
/// crea en el primer guardado del profesional) y su estado es independiente del
/// de la cita y de la sesión.
/// </summary>
public interface IEncounterRepository
{
    /// <summary>Encuentro de una cita (lectura sin tracking). <c>null</c> si aún no existe.</summary>
    Task<ClinicalEncounter?> GetByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    /// <summary>Encuentro de una cita TRACKEADO (para mutaciones: guardar/completar).</summary>
    Task<ClinicalEncounter?> GetForUpdateByAppointmentIdAsync(
        Guid appointmentId,
        CancellationToken ct = default);

    /// <summary>
    /// Crea el encuentro. Idempotente: como es 1:1 con la cita, una violación del
    /// índice único (dos guardados concurrentes sobre la misma cita sin encuentro)
    /// significa que ya existe → devuelve el existente TRACKEADO (el handler
    /// aplica sus cambios sobre él).
    /// </summary>
    Task<ClinicalEncounter> AddAsync(ClinicalEncounter encounter, CancellationToken ct = default);

    /// <summary>Persiste cambios de un encuentro cargado con <c>GetForUpdateByAppointmentIdAsync</c>.</summary>
    Task UpdateAsync(ClinicalEncounter encounter, CancellationToken ct = default);
}
