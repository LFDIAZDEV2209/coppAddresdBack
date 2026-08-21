using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Rol efectivo de un participante de la sala de una cita, derivado de la
/// identidad del JWT (nunca de un id enviado por el cliente) o del permiso de
/// supervisión. El profesional/paciente de la cita se resuelve contra el backend.
/// </summary>
internal enum SessionParticipant
{
    None,
    Professional,
    Patient,
    Supervisor
}

/// <summary>
/// Reglas compartidas del flujo de sala/sesión: autorización por identidad +
/// permiso de supervisión, ventana de acceso y creación de sala.
/// </summary>
internal static class SessionSupport
{
    /// <summary>
    /// Autoriza el acceso a la sala (join-token / ver sala): el profesional de la
    /// cita, el paciente de la cita (resueltos por usuario del JWT) o un
    /// supervisor (permiso <c>Telemedicine.SessionsManage</c>). Si no es ninguno,
    /// 403.
    /// </summary>
    public static async Task<SessionParticipant> RequireParticipantAsync(
        ITelemedicineReferenceDataService referenceData,
        TelemedicineAppointment appointment,
        Guid userId,
        bool hasManagePermission,
        CancellationToken ct)
    {
        if (hasManagePermission)
        {
            return SessionParticipant.Supervisor;
        }

        var professional = await referenceData.GetProfessionalByUserIdAsync(userId, ct);
        if (professional is not null && professional.Id == appointment.ProfessionalId)
        {
            return SessionParticipant.Professional;
        }

        var patient = await referenceData.GetPatientByUserIdAsync(userId, ct);
        if (patient is not null && patient.Id == appointment.PatientId)
        {
            return SessionParticipant.Patient;
        }

        throw new ForbiddenException(
            "No eres participante de esta cita ni tienes permiso para supervisar la sala.");
    }

    /// <summary>
    /// Autoriza iniciar/finalizar la sesión: solo el profesional de la cita o un
    /// supervisor. El paciente no puede iniciar ni finalizar la consulta.
    /// </summary>
    public static async Task RequireSessionOwnerAsync(
        ITelemedicineReferenceDataService referenceData,
        TelemedicineAppointment appointment,
        Guid userId,
        bool hasManagePermission,
        CancellationToken ct)
    {
        if (hasManagePermission)
        {
            return;
        }

        var professional = await referenceData.GetProfessionalByUserIdAsync(userId, ct);
        if (professional is not null && professional.Id == appointment.ProfessionalId)
        {
            return;
        }

        throw new ForbiddenException(
            "Solo el profesional asignado a la cita puede iniciar o finalizar la sesión.");
    }

    /// <summary>
    /// Ventana de acceso a la sala, desde la configuración efectiva de la
    /// organización/clínica: abre <c>RoomOpenBeforeMinutes</c> antes del inicio y
    /// cierra <c>RoomCloseAfterMinutes</c> después.
    /// </summary>
    public static (DateTimeOffset Open, DateTimeOffset Close) Window(
        TelemedicineAppointment appointment,
        TelemedicineSettings settings)
        => (appointment.ScheduledStart.AddMinutes(-settings.RoomOpenBeforeMinutes),
            appointment.ScheduledStart.AddMinutes(settings.RoomCloseAfterMinutes));

    /// <summary>La cita debe estar en un estado que admita sala/sesión (confirmada o en curso).</summary>
    public static void EnsureCanStartOrJoin(AppointmentStatus status)
    {
        if (status is not (AppointmentStatus.Confirmed or AppointmentStatus.InProgress))
        {
            throw new BusinessRuleViolationException(
                $"La cita no puede abrir su sala en el estado actual ({status}).");
        }
    }

    /// <summary>Valida que <paramref name="now"/> esté dentro de la ventana de acceso.</summary>
    public static void EnsureWithinWindow(
        TelemedicineAppointment appointment,
        TelemedicineSettings settings,
        DateTimeOffset now)
    {
        var (open, close) = Window(appointment, settings);

        if (now < open)
        {
            throw new BusinessRuleViolationException(
                $"La sala abre el {open:O} (los participantes pueden entrar hasta {settings.RoomOpenBeforeMinutes} min antes del inicio).");
        }

        if (now >= close)
        {
            throw new BusinessRuleViolationException(
                $"La sala ya cerró el {close:O}; la ventana de acceso expiró.");
        }
    }

    /// <summary>Nombre determinista de la sala en el proveedor (base de la idempotencia).</summary>
    public static string ProviderRoomName(Guid appointmentId)
        => $"apt-{appointmentId:N}";

    /// <summary>Instancia la sala de dominio para una cita (sin persistir).</summary>
    public static VirtualRoom NewRoom(
        TelemedicineAppointment appointment,
        TelemedicineSettings settings,
        string providerRoomName,
        string providerRoomSid,
        Guid createdBy)
    {
        var (open, close) = Window(appointment, settings);

        return new VirtualRoom
        {
            AppointmentId = appointment.Id,
            Provider = "twilio",
            ProviderRoomSid = providerRoomSid,
            ProviderRoomName = providerRoomName,
            ScheduledOpenAt = open,
            ScheduledCloseAt = close,
            MaxParticipants = settings.MaxParticipants,
            CreatedBy = createdBy,
        };
    }
}

/// <summary>Construcción de DTOs de sala/sesión para la API.</summary>
internal static class RoomDtos
{
    public static VirtualRoomDto Build(VirtualRoom room, IReadOnlyList<RoomParticipantDto>? participants = null)
    {
        var activeSession = room.Sessions
            .Where(s => s.Status == TelemedicineSessionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        return new VirtualRoomDto(
            room.Id,
            room.Provider,
            room.ProviderRoomName,
            room.Status,
            room.ScheduledOpenAt,
            room.ScheduledCloseAt,
            activeSession?.Id,
            activeSession?.Status,
            participants ?? []);
    }
}
