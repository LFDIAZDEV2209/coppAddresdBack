using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Application.VideoProvider;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Logging;

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
    /// <summary>Mínimo de participantes concurrentes de una sala (profesional + paciente).</summary>
    public const int MinRoomMaxParticipants = 2;

    /// <summary>Máximo de participantes concurrentes soportado por el producto (Twilio group: 50).</summary>
    public const int MaxRoomMaxParticipants = 10;

    /// <summary>
    /// Autoriza el acceso a la sala (join-token / ver sala): el profesional de la
    /// cita, el paciente de la cita (resueltos por usuario del JWT) o un
    /// supervisor (permiso <c>Telemedicine.SessionsManage</c>). Si no es ninguno,
    /// 403.
    /// </summary>
    public static async Task<SessionParticipant> RequireParticipantAsync(
        IAppointmentReferenceDataService referenceData,
        Appointment appointment,
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
        IAppointmentReferenceDataService referenceData,
        Appointment appointment,
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
    /// cierra <c>RoomCloseAfterMinutes</c> después del fin de la cita. Si la cita
    /// fue reabierta, el ciclo nuevo corre desde <c>ReopenedAt</c> y el cierre
    /// usa el mayor de los fines (programado o reapertura + duración).
    /// </summary>
    public static (DateTimeOffset Open, DateTimeOffset Close) Window(
        Appointment appointment,
        TelemedicineSettings settings)
    {
        if (appointment.ReopenedAt is { } reopenedAt)
        {
            var reopenedEnd = reopenedAt.AddMinutes(appointment.DurationMinutes);
            var end = reopenedEnd > appointment.ScheduledEnd ? reopenedEnd : appointment.ScheduledEnd;

            return (reopenedAt.AddMinutes(-settings.RoomOpenBeforeMinutes),
                end.AddMinutes(settings.RoomCloseAfterMinutes));
        }

        return (appointment.ScheduledStart.AddMinutes(-settings.RoomOpenBeforeMinutes),
            appointment.ScheduledEnd.AddMinutes(settings.RoomCloseAfterMinutes));
    }

    /// <summary>La cita debe estar en un estado que admita sala/sesión (confirmada o en curso).</summary>
    public static void EnsureCanStartOrJoin(AppointmentStatus status)
    {
        if (status is not (AppointmentStatus.Confirmed or AppointmentStatus.InProgress))
        {
            throw new BusinessRuleViolationException(
                $"La cita no puede abrir su sala en el estado actual ({status}).");
        }
    }

    /// <summary>
    /// El chat de la consulta está disponible con la cita confirmada, en curso o
    /// completada (F3): el paciente puede esperar en la sala con la cita aún
    /// confirmada y el acceso posterior a la consulta se conserva. Solo los
    /// estados sin atención (Requested/Cancelled/NoShow) responden 409.
    /// </summary>
    public static void EnsureChatAllowed(AppointmentStatus status)
    {
        if (status is not (AppointmentStatus.Confirmed
            or AppointmentStatus.InProgress
            or AppointmentStatus.Completed))
        {
            throw new BusinessRuleViolationException(
                $"El chat no está disponible en el estado actual de la cita ({status}).");
        }
    }

    /// <summary>
    /// Valida el rango del máximo de participantes efectivo (2–10). Protege
    /// contra filas de settings corruptas o editadas manualmente: un valor fuera
    /// de rango produciría un error del proveedor (Twilio 53107) o una sala
    /// inutilizable.
    /// </summary>
    public static void EnsureValidMaxParticipants(int maxParticipants)
    {
        if (maxParticipants is < MinRoomMaxParticipants or > MaxRoomMaxParticipants)
        {
            throw new BusinessRuleViolationException(
                $"La capacidad de la sala debe estar entre {MinRoomMaxParticipants} y {MaxRoomMaxParticipants} participantes (valor efectivo: {maxParticipants}).");
        }
    }

    /// <summary>
    /// Eleva la capacidad de una sala ya creada cuando el settings efectivo la
    /// superó: actualiza la sala del proveedor (best-effort, solo si sigue
    /// admitiéndolo) y devuelve el nuevo límite para persistirlo. La
    /// persistencia local la decide el llamador (join actualiza la sala; start
    /// la persiste con el agregado de la cita). Un fallo del proveedor NUNCA
    /// interrumpe el join: se loguea como warning y la regla de negocio sigue
    /// vigente en la próxima reapertura/sala nueva.
    /// </summary>
    public static async Task<bool> ElevateRoomCapacityIfNeededAsync(
        IVideoProvider videoProvider,
        VirtualRoom room,
        int effectiveMaxParticipants,
        ILogger logger,
        CancellationToken ct)
    {
        if (room.MaxParticipants >= effectiveMaxParticipants)
        {
            return false;
        }

        try
        {
            await videoProvider.UpdateRoomMaxParticipantsAsync(
                room.ProviderRoomSid, effectiveMaxParticipants, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(
                ex,
                "No se pudo elevar la capacidad de la sala {RoomSid} a {MaxParticipants}; la actualización es best-effort y el flujo continúa.",
                room.ProviderRoomSid, effectiveMaxParticipants);
        }

        room.MaxParticipants = effectiveMaxParticipants;
        room.UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>Valida que <paramref name="now"/> esté dentro de la ventana de acceso.</summary>
    public static void EnsureWithinWindow(
        Appointment appointment,
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

    /// <summary>
    /// Nombre de la sala del ciclo actual: la cita original usa el nombre base y
    /// cada reapertura un sufijo propio (el proveedor no permite reusar salas
    /// completadas).
    /// </summary>
    public static string ProviderRoomName(Guid appointmentId, int reopenCount)
        => reopenCount <= 0
            ? ProviderRoomName(appointmentId)
            : $"apt-{appointmentId:N}-r{reopenCount}";

    /// <summary>
    /// Cierra la sesión activa más reciente de la sala (si existe): la marca
    /// como terminada, calcula la duración y registra motivo/autor. La usan el
    /// webhook del proveedor y el barrido de sesiones estancadas.
    /// </summary>
    public static void EndActiveSession(
        VirtualRoom room,
        string endReason,
        Guid? endedBy,
        DateTimeOffset now)
    {
        var active = room.Sessions
            .Where(s => s.Status == TelemedicineSessionStatus.Active)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (active is null)
        {
            return;
        }

        active.Status = TelemedicineSessionStatus.Ended;
        active.EndedAt = now;
        active.DurationSeconds = active.StartedAt is { } startedAt
            ? (long)Math.Max(0, (now - startedAt).TotalSeconds)
            : null;
        active.EndedBy = endedBy;
        active.EndReason = endReason;
    }

    /// <summary>Instancia la sala de dominio para una cita (sin persistir).</summary>
    public static VirtualRoom NewRoom(
        Appointment appointment,
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
