using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Validación de datos de referencia del ERP en los casos de uso de
/// agendamiento. El microservicio no posee los datos maestros: verifica
/// existencia contra el backend (internal endpoints) y lanza
/// <see cref="NotFoundException"/> con el nombre del recurso correcto.
/// </summary>
internal static class ReferenceDataGuard
{
    public static async Task<ProfessionalRefDto> RequireProfessionalAsync(
        ITelemedicineReferenceDataService service,
        Guid id,
        CancellationToken ct)
        => await service.GetProfessionalAsync(id, ct)
           ?? throw new NotFoundException("Profesional", id);

    public static async Task<PatientRefDto> RequirePatientAsync(
        ITelemedicineReferenceDataService service,
        Guid id,
        CancellationToken ct)
        => await service.GetPatientAsync(id, ct)
           ?? throw new NotFoundException("Paciente", id);

    public static async Task<SpecialtyRefDto> RequireSpecialtyAsync(
        ITelemedicineReferenceDataService service,
        Guid id,
        CancellationToken ct)
        => await service.GetSpecialtyAsync(id, ct)
           ?? throw new NotFoundException("Especialidad", id);

    public static async Task<LocationRefDto?> RequireLocationAsync(
        ITelemedicineReferenceDataService service,
        Guid? id,
        CancellationToken ct)
    {
        if (id is not { } locationId)
        {
            return null;
        }

        return await service.GetLocationAsync(locationId, ct)
               ?? throw new NotFoundException("Sede", locationId);
    }
}

/// <summary>
/// Reglas parametrizadas de agendamiento compartidas por los casos de uso
/// (crear/confirmar/agendar/reprogramar). Toda regla sale de
/// <see cref="TelemedicineSettings"/>; el horario se expresa en el timezone
/// del cliente (DateTimeOffset) y se almacena tal cual.
/// </summary>
internal static class SchedulingRules
{
    public const int MaxDurationMinutes = 240;

    /// <summary>
    /// Resuelve el slot (inicio, fin, duración) validando anticipación mínima y
    /// ventana máxima contra la configuración efectiva de la organización/clínica.
    /// </summary>
    public static (DateTimeOffset Start, DateTimeOffset End, int DurationMinutes) ResolveSlot(
        DateTimeOffset scheduledStart,
        int? requestedDurationMinutes,
        TelemedicineSettings settings,
        DateTimeOffset now)
    {
        var duration = requestedDurationMinutes is { } d and > 0 and <= MaxDurationMinutes
            ? d
            : settings.DefaultAppointmentDurationMinutes;

        // Npgsql solo admite DateTimeOffset UTC en columnas timestamptz: el
        // horario llega con el offset del cliente y se normaliza a UTC. Toda
        // comparación/almacenamiento posterior usa UTC; la UI convierte a local.
        var start = scheduledStart.ToUniversalTime();
        var end = start.AddMinutes(duration);

        if (start < now.AddHours(settings.MinAdvanceBookingHours))
        {
            throw new BusinessRuleViolationException(
                $"Las citas deben agendarse con al menos {settings.MinAdvanceBookingHours} h de anticipación.");
        }

        if (start > now.AddDays(settings.MaxAdvanceBookingDays))
        {
            throw new BusinessRuleViolationException(
                $"No se pueden agendar citas más allá de {settings.MaxAdvanceBookingDays} días.");
        }

        return (start, end, duration);
    }
}
