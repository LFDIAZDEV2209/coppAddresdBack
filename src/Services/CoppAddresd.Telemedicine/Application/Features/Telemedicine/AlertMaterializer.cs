using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Fábrica de alertas de la bandeja: convierte un evento de dominio en una
/// <see cref="TelemedicineAlert"/> (materialización). Puro y sin dependencias de
/// infraestructura: el destinatario (usuario de <c>auth.users</c>) y los nombres
/// para el texto los resuelve el handler que emite la alerta. Si el destinatario
/// no se puede resolver (p. ej. el paciente aún no es usuario del ERP), las
/// fábricas devuelven <c>null</c> y el handler omite la alerta.
/// </summary>
internal static class AlertMaterializer
{
    /// <summary>
    /// Nueva solicitud de un paciente → aviso al profesional que la confirmará.
    /// </summary>
    public static TelemedicineAlert? NewRequest(
        Guid? professionalUserId,
        Guid requestId,
        Guid specialtyId,
        string patientName,
        string specialtyName
    ) =>
        Build(
            professionalUserId,
            AlertType.NewRequest,
            AlertSeverity.Info,
            "Nueva solicitud de telemedicina",
            $"{patientName} solicitó una cita de {specialtyName}.",
            relatedAppointmentId: null
        );

    /// <summary>
    /// Solicitud aprobada (por el administrador) → aviso al profesional asignado.
    /// </summary>
    public static TelemedicineAlert? RequestApproved(
        Guid? professionalUserId,
        Guid requestId,
        Guid specialtyId,
        string patientName,
        string specialtyName
    ) =>
        Build(
            professionalUserId,
            AlertType.RequestApproved,
            AlertSeverity.Info,
            "Solicitud aprobada",
            $"La solicitud de {patientName} ({specialtyName}) fue aprobada.",
            relatedAppointmentId: null
        );

    /// <summary>Solicitud rechazada → aviso al profesional asignado (si es resoluble).</summary>
    public static TelemedicineAlert? RequestRejected(
        Guid? professionalUserId,
        Guid requestId,
        Guid specialtyId,
        string reason
    ) =>
        Build(
            professionalUserId,
            AlertType.RequestRejected,
            AlertSeverity.Warning,
            "Solicitud rechazada",
            $"La solicitud fue rechazada: {reason}",
            relatedAppointmentId: null
        );

    /// <summary>Cita creada/confirmada → aviso al profesional asignado.</summary>
    public static TelemedicineAlert? NewAppointment(
        Guid? professionalUserId,
        Guid appointmentId,
        Guid specialtyId,
        string patientName,
        string specialtyName,
        DateTimeOffset scheduledStart
    ) =>
        Build(
            professionalUserId,
            AlertType.NewAppointment,
            AlertSeverity.Info,
            "Nueva cita asignada",
            $"{patientName} · {specialtyName} · {scheduledStart:g}.",
            appointmentId
        );

    /// <summary>Reprogramación de una cita → aviso al profesional.</summary>
    public static TelemedicineAlert? AppointmentRescheduled(
        Guid? professionalUserId,
        Guid appointmentId,
        string patientName,
        DateTimeOffset newStart
    ) =>
        Build(
            professionalUserId,
            AlertType.AppointmentRescheduled,
            AlertSeverity.Warning,
            "Cita reprogramada",
            $"La cita de {patientName} se reprogramó para {newStart:g}.",
            appointmentId
        );

    /// <summary>Cancelación de una cita → aviso al profesional.</summary>
    public static TelemedicineAlert? AppointmentCancelled(
        Guid? professionalUserId,
        Guid appointmentId,
        string patientName,
        string reason
    ) =>
        Build(
            professionalUserId,
            AlertType.AppointmentCancelled,
            AlertSeverity.Warning,
            "Cita cancelada",
            $"La cita de {patientName} fue cancelada{(string.IsNullOrWhiteSpace(reason) ? "." : $": {reason}")}",
            appointmentId
        );

    /// <summary>El paciente ingresó a la sala y está esperando → aviso al profesional.</summary>
    public static TelemedicineAlert? PatientWaiting(
        Guid? professionalUserId,
        Guid appointmentId,
        string patientName
    ) =>
        Build(
            professionalUserId,
            AlertType.PatientWaiting,
            AlertSeverity.Info,
            "El paciente está esperando",
            $"{patientName} ingresó a la sala virtual.",
            appointmentId
        );

    /// <summary>El profesional ingresó a la sala (la consulta puede comenzar) → aviso al profesional.</summary>
    public static TelemedicineAlert? ProfessionalJoined(
        Guid? professionalUserId,
        Guid appointmentId,
        string patientName
    ) =>
        Build(
            professionalUserId,
            AlertType.PatientJoined,
            AlertSeverity.Info,
            "Te uniste a la sala",
            $"La cita con {patientName} está en curso.",
            appointmentId
        );

    /// <summary>Un participante abandonó la sala → aviso al profesional.</summary>
    public static TelemedicineAlert? ParticipantLeft(
        Guid? professionalUserId,
        Guid appointmentId,
        string patientName
    ) =>
        Build(
            professionalUserId,
            AlertType.ParticipantLeft,
            AlertSeverity.Warning,
            "Un participante abandonó la sala",
            $"{patientName} abandonó la sala virtual.",
            appointmentId
        );

    /// <summary>Sesión finalizada → aviso al profesional.</summary>
    public static TelemedicineAlert? SessionEnded(
        Guid? professionalUserId,
        Guid appointmentId,
        string patientName
    ) =>
        Build(
            professionalUserId,
            AlertType.SessionEnded,
            AlertSeverity.Info,
            "Sesión finalizada",
            $"La consulta con {patientName} finalizó.",
            appointmentId
        );

    private static TelemedicineAlert? Build(
        Guid? recipientUserId,
        AlertType type,
        AlertSeverity severity,
        string title,
        string body,
        Guid? relatedAppointmentId
    )
    {
        // Sin destinatario resoluble (p. ej. profesional sin usuario del ERP):
        // la alerta no tiene a quién entregarse → se omite (no se inventa).
        if (recipientUserId is null)
        {
            return null;
        }

        return new TelemedicineAlert
        {
            RecipientType = AlertRecipientType.User,
            RecipientUserId = recipientUserId,
            Type = type,
            Severity = severity,
            Title = title,
            Body = body,
            RelatedAppointmentId = relatedAppointmentId,
        };
    }
}
