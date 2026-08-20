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
    DateTime CreatedAt);

/// <summary>DTO de una cita de telemedicina (fila de agenda/calendario y detalle).</summary>
public sealed record TelemedicineAppointmentDto(
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
    DateTime? CreatedAt);

/// <summary>Resultado paginado de solicitudes (bandeja administrativa).</summary>
public sealed record PaginatedRequestsResult(
    IReadOnlyList<TelemedicineRequestDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages);
