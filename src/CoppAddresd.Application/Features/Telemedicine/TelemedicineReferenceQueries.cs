using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using MediatR;

namespace CoppAddresd.Application.Features.Telemedicine;

// DTOs mínimos para el microservicio de Telemedicina. El microservicio no
// posee los datos maestros del ERP: los consulta aquí por Id (referencias
// débiles) en cada operación de agendamiento. Se devuelve solo lo necesario
// para validar existencia y poblar la UI; sin PHI innecesaria.

/// <summary>Profesional (extensión clínica del empleado). Id = id de <c>erp.professionals</c>.</summary>
public sealed record TelemedicineProfessionalRefDto(
    Guid Id,
    Guid EmployeeId,
    Guid? UserId,
    string FullName,
    string? ProfessionalTypeName,
    IReadOnlyList<Guid> SpecialtyIds,
    IReadOnlyList<Guid> LocationIds,
    IReadOnlyList<Guid> ClinicIds);

/// <summary>Paciente del schema <c>app</c> (sin PHI clínica, solo identidad + contexto).</summary>
public sealed record TelemedicinePatientRefDto(
    Guid Id,
    string FullName,
    string? Email,
    Guid? ClinicId,
    Guid? LocationId);

public sealed record TelemedicineSpecialtyRefDto(
    Guid Id,
    string Code,
    string Name,
    string Category);

public sealed record TelemedicineLocationRefDto(
    Guid Id,
    string Name,
    Guid ClinicId,
    bool IsActive);

// Queries (retornan null si el id no existe → el microservicio traduce a su
// propia NotFoundException con el mensaje de dominio correcto).

public sealed record GetTelemedicineProfessionalRefQuery(Guid ProfessionalId)
    : IRequest<TelemedicineProfessionalRefDto?>;

public sealed record GetTelemedicinePatientRefQuery(Guid PatientId)
    : IRequest<TelemedicinePatientRefDto?>;

public sealed record GetTelemedicineSpecialtyRefQuery(Guid SpecialtyId)
    : IRequest<TelemedicineSpecialtyRefDto?>;

public sealed record GetTelemedicineLocationRefQuery(Guid LocationId)
    : IRequest<TelemedicineLocationRefDto?>;

// Resolución por usuario de Auth (contexto del JWT). El microservicio la usa
// para autorizar acceso a una sala: el participante debe ser el profesional o
// el paciente de la cita, y esto se deriva del userId del token, nunca de un
// id enviado por el cliente.

public sealed record GetTelemedicineProfessionalByUserIdQuery(Guid UserId)
    : IRequest<TelemedicineProfessionalRefDto?>;

public sealed record GetTelemedicinePatientByUserIdQuery(Guid UserId)
    : IRequest<TelemedicinePatientRefDto?>;

public sealed class GetTelemedicineProfessionalRefQueryHandler(
    IEmployeeRepository employees) : IRequestHandler<GetTelemedicineProfessionalRefQuery, TelemedicineProfessionalRefDto?>
{
    public async Task<TelemedicineProfessionalRefDto?> Handle(
        GetTelemedicineProfessionalRefQuery request,
        CancellationToken ct)
    {
        var employee = await employees.GetByProfessionalIdAsync(request.ProfessionalId, ct);
        if (employee?.Professional is null)
        {
            return null;
        }

        return new TelemedicineProfessionalRefDto(
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

public sealed class GetTelemedicinePatientRefQueryHandler(
    IPatientRepository patients) : IRequestHandler<GetTelemedicinePatientRefQuery, TelemedicinePatientRefDto?>
{
    public async Task<TelemedicinePatientRefDto?> Handle(
        GetTelemedicinePatientRefQuery request,
        CancellationToken ct)
    {
        var patient = await patients.GetByIdAsync(request.PatientId, ct);
        if (patient is null)
        {
            return null;
        }

        return new TelemedicinePatientRefDto(
            patient.Id,
            $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Trim(),
            patient.Email,
            patient.ClinicId,
            patient.LocationId);
    }
}

public sealed class GetTelemedicineSpecialtyRefQueryHandler(
    IOrganizationRepository organizations) : IRequestHandler<GetTelemedicineSpecialtyRefQuery, TelemedicineSpecialtyRefDto?>
{
    public async Task<TelemedicineSpecialtyRefDto?> Handle(
        GetTelemedicineSpecialtyRefQuery request,
        CancellationToken ct)
    {
        var specialty = await organizations.GetSpecialtyByIdAsync(request.SpecialtyId, ct);
        if (specialty is null)
        {
            return null;
        }

        return new TelemedicineSpecialtyRefDto(
            specialty.Id,
            specialty.Code,
            specialty.Name,
            specialty.Category);
    }
}

public sealed class GetTelemedicineLocationRefQueryHandler(
    IOrganizationRepository organizations) : IRequestHandler<GetTelemedicineLocationRefQuery, TelemedicineLocationRefDto?>
{
    public async Task<TelemedicineLocationRefDto?> Handle(
        GetTelemedicineLocationRefQuery request,
        CancellationToken ct)
    {
        var location = await organizations.GetLocationByIdAsync(request.LocationId, ct);
        if (location is null)
        {
            return null;
        }

        return new TelemedicineLocationRefDto(
            location.Id,
            location.Name,
            location.ClinicId,
            location.IsActive);
    }
}

public sealed class GetTelemedicineProfessionalByUserIdQueryHandler(
    IEmployeeRepository employees) : IRequestHandler<GetTelemedicineProfessionalByUserIdQuery, TelemedicineProfessionalRefDto?>
{
    public async Task<TelemedicineProfessionalRefDto?> Handle(
        GetTelemedicineProfessionalByUserIdQuery request,
        CancellationToken ct)
    {
        var employee = await employees.GetByUserIdAsync(request.UserId, ct);
        if (employee?.Professional is null)
        {
            return null;
        }

        return new TelemedicineProfessionalRefDto(
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

public sealed class GetTelemedicinePatientByUserIdQueryHandler(
    IPatientRepository patients) : IRequestHandler<GetTelemedicinePatientByUserIdQuery, TelemedicinePatientRefDto?>
{
    public async Task<TelemedicinePatientRefDto?> Handle(
        GetTelemedicinePatientByUserIdQuery request,
        CancellationToken ct)
    {
        var patient = await patients.GetByUserIdAsync(request.UserId, ct);
        if (patient is null)
        {
            return null;
        }

        return new TelemedicinePatientRefDto(
            patient.Id,
            $"{patient.FirstName} {patient.MiddleName} {patient.LastName}".Trim(),
            patient.Email,
            patient.ClinicId,
            patient.LocationId);
    }
}
