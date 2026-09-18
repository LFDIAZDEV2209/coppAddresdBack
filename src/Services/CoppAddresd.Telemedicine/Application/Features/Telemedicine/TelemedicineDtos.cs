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
    DateTimeOffset? RoomClosesAt = null
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
