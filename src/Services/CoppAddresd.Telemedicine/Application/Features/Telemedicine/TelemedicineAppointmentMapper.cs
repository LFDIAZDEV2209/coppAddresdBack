using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Entities;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Construye DTOs de cita enriqueciendo los datos de referencia del ERP.
/// Deduplica los fetch por entidad única (un paciente/especialidad/profesional
/// se consulta una sola vez aunque aparezca en varias citas): evita el patrón
/// N+1 sobre los internal endpoints del backend.
/// </summary>
internal static class TelemedicineAppointmentMapper
{
    public static async Task<IReadOnlyList<TelemedicineAppointmentDto>> BuildDtosAsync(
        IReadOnlyList<TelemedicineAppointment> appointments,
        ITelemedicineReferenceDataService referenceData,
        CancellationToken ct)
    {
        var patientIds = appointments.Select(a => a.PatientId).Distinct().ToList();
        var professionalIds = appointments.Select(a => a.ProfessionalId).Distinct().ToList();
        var specialtyIds = appointments.Select(a => a.SpecialtyId).Distinct().ToList();
        var locationIds = appointments
            .Select(a => a.LocationId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var patients = await FetchAllAsync(patientIds, id => referenceData.GetPatientAsync(id, ct));
        var professionals = await FetchAllAsync(professionalIds, id => referenceData.GetProfessionalAsync(id, ct));
        var specialties = await FetchAllAsync(specialtyIds, id => referenceData.GetSpecialtyAsync(id, ct));
        var locations = await FetchAllAsync(locationIds, id => referenceData.GetLocationAsync(id, ct));

        return appointments
            .Select(a => new TelemedicineAppointmentDto(
                a.Id,
                a.RequestId,
                a.PatientId,
                patients.GetValueOrDefault(a.PatientId)?.FullName,
                a.ProfessionalId,
                professionals.GetValueOrDefault(a.ProfessionalId)?.FullName,
                a.SpecialtyId,
                specialties.GetValueOrDefault(a.SpecialtyId)?.Name,
                a.OrganizationId,
                a.ClinicId,
                a.LocationId,
                a.LocationId is { } locationId
                    ? locations.GetValueOrDefault(locationId)?.Name
                    : null,
                a.ScheduledStart,
                a.ScheduledEnd,
                a.DurationMinutes,
                a.Status,
                a.RescheduleCount,
                a.CancellationReason,
                a.CreatedAt))
            .ToList();
    }

    private static async Task<Dictionary<Guid, T>> FetchAllAsync<T>(
        IReadOnlyList<Guid> ids,
        Func<Guid, Task<T?>> fetch)
        where T : class
    {
        var result = new Dictionary<Guid, T>();
        var tasks = ids.Select(async id => (Id: id, Value: await fetch(id)));
        foreach (var (id, value) in await Task.WhenAll(tasks))
        {
            if (value is not null)
            {
                result[id] = value;
            }
        }
        return result;
    }
}
