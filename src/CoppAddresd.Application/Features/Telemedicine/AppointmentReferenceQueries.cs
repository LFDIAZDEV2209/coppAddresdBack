using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;

namespace CoppAddresd.Application.Features.Telemedicine;

// DTOs mínimos para el microservicio de Telemedicina. El microservicio no
// posee los datos maestros del ERP: los consulta aquí por Id (referencias
// débiles) en cada operación de agendamiento. Se devuelve solo lo necesario
// para validar existencia y poblar la UI; sin PHI innecesaria.

/// <summary>Profesional (extensión clínica del empleado). Id = id de <c>erp.professionals</c>.</summary>
public sealed record AppointmentProfessionalRefDto(
    Guid Id,
    Guid EmployeeId,
    Guid? UserId,
    string FullName,
    string? ProfessionalTypeName,
    IReadOnlyList<Guid> SpecialtyIds,
    IReadOnlyList<Guid> LocationIds,
    IReadOnlyList<Guid> ClinicIds);

/// <summary>Paciente del schema <c>app</c> (sin PHI clínica, solo identidad + contexto).</summary>
public sealed record AppointmentPatientRefDto(
    Guid Id,
    string FullName,
    string? Email,
    Guid? ClinicId,
    Guid? LocationId);

public sealed record AppointmentSpecialtyRefDto(
    Guid Id,
    string Code,
    string Name,
    string Category);

public sealed record AppointmentLocationRefDto(
    Guid Id,
    string Name,
    Guid ClinicId,
    bool IsActive);

// Queries (retornan null si el id no existe → el microservicio traduce a su
// propia NotFoundException con el mensaje de dominio correcto).

public sealed record GetAppointmentProfessionalRefQuery(Guid ProfessionalId)
    : IRequest<AppointmentProfessionalRefDto?>;

public sealed record GetAppointmentPatientRefQuery(Guid PatientId)
    : IRequest<AppointmentPatientRefDto?>;

public sealed record GetAppointmentSpecialtyRefQuery(Guid SpecialtyId)
    : IRequest<AppointmentSpecialtyRefDto?>;

public sealed record GetAppointmentLocationRefQuery(Guid LocationId)
    : IRequest<AppointmentLocationRefDto?>;

// Resolución por usuario de Auth (contexto del JWT). El microservicio la usa
// para autorizar acceso a una sala: el participante debe ser el profesional o
// el paciente de la cita, y esto se deriva del userId del token, nunca de un
// id enviado por el cliente.

public sealed record GetAppointmentProfessionalByUserIdQuery(Guid UserId)
    : IRequest<AppointmentProfessionalRefDto?>;

public sealed record GetAppointmentPatientByUserIdQuery(Guid UserId)
    : IRequest<AppointmentPatientRefDto?>;

public sealed class GetAppointmentProfessionalRefQueryHandler(
    IEmployeeRepository employees) : IRequestHandler<GetAppointmentProfessionalRefQuery, AppointmentProfessionalRefDto?>
{
    public async Task<AppointmentProfessionalRefDto?> Handle(
        GetAppointmentProfessionalRefQuery request,
        CancellationToken ct)
    {
        var employee = await employees.GetByProfessionalIdAsync(request.ProfessionalId, ct);
        if (employee?.Professional is null)
        {
            return null;
        }

        return new AppointmentProfessionalRefDto(
            employee.Professional.Id,
            employee.Id,
            employee.UserId,
            $"{employee.FirstName} {employee.MiddleName} {employee.LastName}".Trim(),
            employee.Professional.ProfessionalType?.Name,
            employee.Professional.Specialties.Select(s => s.SpecialtyId).Distinct().Order().ToList(),
            employee.ClinicAssignments
                .SelectMany(a => a.Clinic.Locations)
                .Select(l => l.Id)
                .Distinct()
                .ToList(),
            employee.ClinicAssignments.Select(a => a.ClinicId).Distinct().ToList());
    }
}

public sealed class GetAppointmentPatientRefQueryHandler(
    IPatientRepository patients) : IRequestHandler<GetAppointmentPatientRefQuery, AppointmentPatientRefDto?>
{
    public async Task<AppointmentPatientRefDto?> Handle(
        GetAppointmentPatientRefQuery request,
        CancellationToken ct)
    {
        var patient = await patients.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
        {
            return null;
        }

        return new AppointmentPatientRefDto(
            patient.Id,
            $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Trim(),
            patient.Email,
            patient.ClinicId,
            patient.LocationId);
    }
}

public sealed class GetAppointmentSpecialtyRefQueryHandler(
    IOrganizationRepository organizations) : IRequestHandler<GetAppointmentSpecialtyRefQuery, AppointmentSpecialtyRefDto?>
{
    public async Task<AppointmentSpecialtyRefDto?> Handle(
        GetAppointmentSpecialtyRefQuery request,
        CancellationToken ct)
    {
        var specialty = await organizations.GetSpecialtyByIdAsync(request.SpecialtyId, ct);
        if (specialty is null)
        {
            return null;
        }

        return new AppointmentSpecialtyRefDto(
            specialty.Id,
            specialty.Code,
            specialty.Name,
            specialty.Category);
    }
}

public sealed class GetAppointmentLocationRefQueryHandler(
    IOrganizationRepository organizations) : IRequestHandler<GetAppointmentLocationRefQuery, AppointmentLocationRefDto?>
{
    public async Task<AppointmentLocationRefDto?> Handle(
        GetAppointmentLocationRefQuery request,
        CancellationToken ct)
    {
        var location = await organizations.GetLocationByIdAsync(request.LocationId, ct);
        if (location is null)
        {
            return null;
        }

        return new AppointmentLocationRefDto(
            location.Id,
            location.Name,
            location.ClinicId,
            location.IsActive);
    }
}

public sealed class GetAppointmentProfessionalByUserIdQueryHandler(
    IEmployeeRepository employees) : IRequestHandler<GetAppointmentProfessionalByUserIdQuery, AppointmentProfessionalRefDto?>
{
    public async Task<AppointmentProfessionalRefDto?> Handle(
        GetAppointmentProfessionalByUserIdQuery request,
        CancellationToken ct)
    {
        var employee = await employees.GetByUserIdAsync(request.UserId, ct);
        if (employee?.Professional is null)
        {
            return null;
        }

        return new AppointmentProfessionalRefDto(
            employee.Professional.Id,
            employee.Id,
            employee.UserId,
            $"{employee.FirstName} {employee.MiddleName} {employee.LastName}".Trim(),
            employee.Professional.ProfessionalType?.Name,
            employee.Professional.Specialties.Select(s => s.SpecialtyId).Distinct().Order().ToList(),
            employee.ClinicAssignments
                .SelectMany(a => a.Clinic.Locations)
                .Select(l => l.Id)
                .Distinct()
                .ToList(),
            employee.ClinicAssignments.Select(a => a.ClinicId).Distinct().ToList());
    }
}

public sealed class GetAppointmentPatientByUserIdQueryHandler(
    IPatientRepository patients) : IRequestHandler<GetAppointmentPatientByUserIdQuery, AppointmentPatientRefDto?>
{
    public async Task<AppointmentPatientRefDto?> Handle(
        GetAppointmentPatientByUserIdQuery request,
        CancellationToken ct)
    {
        var patient = await patients.GetByUserIdAsync(request.UserId, ct);
        if (patient is null)
        {
            return null;
        }

        return new AppointmentPatientRefDto(
            patient.Id,
            $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Trim(),
            patient.Email,
            patient.ClinicId,
            patient.LocationId);
    }
}
