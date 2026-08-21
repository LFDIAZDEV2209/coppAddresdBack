using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Application.Interfaces;

/// <summary>
/// Persistencia de las solicitudes de telemedicina del paciente. La solicitud
/// es una entidad independiente de la cita: su confirmación deriva en una cita
/// (estado <c>Converted</c>), pero puede rechazarse o cancelarse sin crear cita.
/// </summary>
public interface IRequestRepository
{
    Task<TelemedicineRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<TelemedicineRequest> AddAsync(TelemedicineRequest request, CancellationToken ct = default);

    Task UpdateAsync(TelemedicineRequest request, CancellationToken ct = default);

    /// <summary>Cambio dirigido de estado (p. ej. a <c>Converted</c> tras confirmar la cita). Evita reescribir la entidad completa.</summary>
    Task SetStatusAsync(
        Guid requestId,
        AppointmentRequestStatus status,
        CancellationToken ct = default);

    /// <summary>Solicitudes de un paciente, de más reciente a más antigua.</summary>
    Task<IReadOnlyList<TelemedicineRequest>> ListByPatientAsync(
        Guid patientId,
        CancellationToken ct = default);

    /// <summary>Solicitudes de una organización (bandeja administrativa), paginadas y con filtro de estado.</summary>
    Task<(IReadOnlyList<TelemedicineRequest> Items, int Total)> ListByOrganizationAsync(
        Guid organizationId,
        AppointmentRequestStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Listado administrativo global de solicitudes con filtros opcionales
    /// (estado, profesional, paciente, rango), paginado. Base de la vista
    /// "Solicitudes" del admin y de la bandeja del profesional (sus solicitudes
    /// pendientes de confirmar).
    /// </summary>
    Task<(IReadOnlyList<TelemedicineRequest> Items, int Total)> ListAdminAsync(
        AppointmentRequestStatus? status,
        Guid? professionalId,
        Guid? patientId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Cuenta las solicitudes en un estado concreto (KPIs del dashboard admin).</summary>
    Task<int> CountByStatusAsync(
        AppointmentRequestStatus status,
        CancellationToken ct = default);
}
