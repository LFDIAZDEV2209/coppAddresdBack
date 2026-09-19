using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>DTO de una solicitud de telemedicina para la API.</summary>
public sealed record TelemedicineRequestDto(
    Guid Id,
    Guid PatientId,
    string? PatientName,
    Guid? ProfessionalId,
    Guid SpecialtyId,
    string? SpecialtyName,
    Guid OrganizationId,
    Guid? ClinicId,
    Guid? LocationId,
    DateTimeOffset? PreferredStart,
    string? Reason,
    AppointmentRequestStatus Status,
    DateTime CreatedAt,
    string? RejectionReason
);

/// <summary>DTO de una cita de telemedicina (fila de agenda/calendario y detalle).</summary>
public sealed record AppointmentDto(
    Guid Id,
    Guid? RequestId,
    Guid PatientId,
    string? PatientName,
    Guid ProfessionalId,
    string? ProfessionalName,
    Guid SpecialtyId,
    string? SpecialtyName,
    Guid OrganizationId,
    Guid? ClinicId,
    Guid? LocationId,
    string? LocationName,
    DateTimeOffset ScheduledStart,
    DateTimeOffset ScheduledEnd,
    int DurationMinutes,
    AppointmentStatus Status,
    int RescheduleCount,
    string? CancellationReason,
    DateTime? CreatedAt,
    DateTimeOffset? RoomOpensAt = null,
    DateTimeOffset? RoomClosesAt = null,
    DateTimeOffset? CompletedAt = null,
    int? ReopenGraceMinutes = null
);

/// <summary>Resultado paginado de solicitudes (bandeja administrativa).</summary>
public sealed record PaginatedRequestsResult(
    IReadOnlyList<TelemedicineRequestDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>Participante de una sala según el proveedor de video.</summary>
public sealed record RoomParticipantDto(
    string ParticipantSid,
    string Identity,
    bool IsConnected,
    DateTimeOffset? ConnectedAt,
    DateTimeOffset? DisconnectedAt
);

/// <summary>Sala virtual de una cita (estado de dominio + ventana + participantes).</summary>
public sealed record VirtualRoomDto(
    Guid Id,
    string Provider,
    string ProviderRoomName,
    VirtualRoomStatus Status,
    DateTimeOffset ScheduledOpenAt,
    DateTimeOffset ScheduledCloseAt,
    Guid? ActiveSessionId,
    TelemedicineSessionStatus? ActiveSessionStatus,
    IReadOnlyList<RoomParticipantDto> Participants
);

/// <summary>Resultado del <c>join-token</c>: token de acceso + sala.</summary>
public sealed record JoinSessionResultDto(
    string Token,
    DateTimeOffset ExpiresAt,
    VirtualRoomDto Room
);

/// <summary>
/// Mensaje del chat de la consulta (F3). El rol del emisor va derivado del JWT
/// (Professional | Patient | Supervisor); el cuerpo es texto plano.
/// </summary>
public sealed record ChatMessageDto(
    Guid Id,
    Guid AppointmentId,
    Guid SenderUserId,
    string SenderRole,
    string Body,
    DateTimeOffset CreatedAt
)
{
    public static ChatMessageDto FromEntity(ChatMessage message) => new(
        message.Id,
        message.AppointmentId,
        message.SenderUserId,
        message.SenderRole,
        message.Body,
        new DateTimeOffset(message.CreatedAt, TimeSpan.Zero)
    );
}

/// <summary>
/// Pre-consulta del paciente (F4): motivo obligatorio y textos opcionales.
/// <c>patientId</c>/<c>createdBy</c> salen de la cita y del JWT, nunca del
/// cuerpo de la petición.
/// </summary>
public sealed record PreVisitIntakeDto(
    Guid Id,
    Guid AppointmentId,
    Guid PatientId,
    string Reason,
    string? Symptoms,
    string? Allergies,
    string? Medications,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
)
{
    public static PreVisitIntakeDto FromEntity(PreVisitIntake intake) => new(
        intake.Id,
        intake.AppointmentId,
        intake.PatientId,
        intake.Reason,
        intake.Symptoms,
        intake.Allergies,
        intake.Medications,
        new DateTimeOffset(intake.CreatedAt, TimeSpan.Zero),
        intake.UpdatedAt is { } updatedAt
            ? new DateTimeOffset(updatedAt, TimeSpan.Zero)
            : null
    );
}

/// <summary>
/// Adenda de un encuentro completado (F4, append-only). <c>authorName</c> es un
/// snapshot legible del autor al momento de firmar.
/// </summary>
public sealed record EncounterAddendumDto(
    Guid Id,
    Guid EncounterId,
    Guid AuthorUserId,
    string? AuthorName,
    string Body,
    DateTimeOffset CreatedAt
)
{
    public static EncounterAddendumDto FromEntity(EncounterAddendum addendum) => new(
        addendum.Id,
        addendum.EncounterId,
        addendum.AuthorUserId,
        addendum.AuthorName,
        addendum.Body,
        new DateTimeOffset(addendum.CreatedAt, TimeSpan.Zero)
    );
}
