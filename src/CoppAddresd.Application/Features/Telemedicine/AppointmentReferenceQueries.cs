using CoppAddresd.Application.Features.Professionals;
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
    IReadOnlyList<Guid> ClinicIds
);

/// <summary>Paciente del schema <c>app</c> (sin PHI clínica, solo identidad + contexto).</summary>
public sealed record AppointmentPatientRefDto(
    Guid Id,
    string FullName,
    string? Email,
    Guid? ClinicId,
    Guid? LocationId,
    string? StateCode,
    /// <summary>
    /// Usuario de Auth del paciente (destinatario de notificaciones). Null =
    /// paciente sin cuenta (no notificable); el emisor omite el envío.
    /// </summary>
    Guid? UserId = null,
    /// <summary>
    /// Organización del ERP de la clínica asignada al paciente (FASE 6): la APP
    /// la usa al crear solicitudes — nunca un id hardcodeado ni el árbol ERP.
    /// Null = paciente sin clínica (el backend rechaza la solicitud con su
    /// mensaje de dominio).
    /// </summary>
    Guid? OrganizationId = null
);

public sealed record AppointmentSpecialtyRefDto(Guid Id, string Code, string Name, string Category);

public sealed record AppointmentLocationRefDto(Guid Id, string Name, Guid ClinicId, bool IsActive);

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

// Candidatos por especialidad (modo specialty de /availability, citas-e2e 1.1a):
// profesionales ACTIVOS asociados a la especialidad con sus horarios semanales,
// filtrados por contexto organización/clínica/sede. Null si la especialidad no
// existe; lista vacía si existe pero sin candidatos (p. ej. inactiva o sin
// profesionales). Una sola respuesta batch: evita una llamada HTTP por profesional.

/// <summary>Profesional candidato con sus turnos semanales.</summary>
public sealed record AppointmentProfessionalCandidateDto(
    Guid ProfessionalId,
    Guid EmployeeId,
    Guid? UserId,
    string FullName,
    IReadOnlyList<Guid> ClinicIds,
    IReadOnlyList<Guid> LocationIds,
    IReadOnlyList<ProfessionalScheduleDto> Schedules
);

public sealed record GetAppointmentCandidatesBySpecialtyQuery(
    Guid SpecialtyId,
    Guid? OrganizationId,
    Guid? ClinicId,
    Guid? LocationId
) : IRequest<IReadOnlyList<AppointmentProfessionalCandidateDto>?>;

public sealed class GetAppointmentCandidatesBySpecialtyQueryHandler(
    IEmployeeRepository employees,
    IOrganizationRepository organizations
)
    : IRequestHandler<
        GetAppointmentCandidatesBySpecialtyQuery,
        IReadOnlyList<AppointmentProfessionalCandidateDto>?
    >
{
    // Tope interno del batch (una especialidad por contexto tiene decenas de
    // profesionales como máximo; evita respuestas desbordadas).
    private const int MaxCandidates = 500;

    public async Task<IReadOnlyList<AppointmentProfessionalCandidateDto>?> Handle(
        GetAppointmentCandidatesBySpecialtyQuery request,
        CancellationToken ct
    )
    {
        var specialty = await organizations.GetSpecialtyByIdAsync(request.SpecialtyId, ct);
        if (specialty is null)
        {
            return null;
        }

        var (items, _) = await employees.ListProfessionalsAsync(
            1,
            MaxCandidates,
            null,
            "Active",
            request.SpecialtyId,
            request.LocationId,
            request.OrganizationId,
            request.ClinicId,
            ct
        );

        if (items.Count == 0)
        {
            return [];
        }

        var professionalIds = items.Select(e => e.Professional!.Id).Distinct().ToList();

        var schedules = await employees.GetSchedulesByProfessionalIdsAsync(professionalIds, ct);

        var byProfessional = schedules
            .GroupBy(s => s.ProfessionalId)
            .ToDictionary(
                g => g.Key,
                g =>
                    (IReadOnlyList<ProfessionalScheduleDto>)
                        g.OrderBy(s => s.Weekday)
                            .Select(ProfessionalScheduleDto.FromEntity)
                            .ToList()
            );

        return items
            .Select(e => new AppointmentProfessionalCandidateDto(
                e.Professional!.Id,
                e.Id,
                e.UserId,
                $"{e.FirstName} {e.MiddleName} {e.LastName}".Trim(),
                e.ClinicAssignments.Select(a => a.ClinicId).Distinct().ToList(),
                e.ClinicAssignments.SelectMany(a => a.Clinic.Locations)
                    .Select(l => l.Id)
                    .Distinct()
                    .ToList(),
                byProfessional.GetValueOrDefault(e.Professional.Id) ?? []
            ))
            .ToList();
    }
}

public sealed class GetAppointmentProfessionalRefQueryHandler(IEmployeeRepository employees)
    : IRequestHandler<GetAppointmentProfessionalRefQuery, AppointmentProfessionalRefDto?>
{
    public async Task<AppointmentProfessionalRefDto?> Handle(
        GetAppointmentProfessionalRefQuery request,
        CancellationToken ct
    )
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
            employee
                .Professional.Specialties.Select(s => s.SpecialtyId)
                .Distinct()
                .Order()
                .ToList(),
            employee
                .ClinicAssignments.SelectMany(a => a.Clinic.Locations)
                .Select(l => l.Id)
                .Distinct()
                .ToList(),
            employee.ClinicAssignments.Select(a => a.ClinicId).Distinct().ToList()
        );
    }
}

public sealed class GetAppointmentPatientRefQueryHandler(IPatientRepository patients)
    : IRequestHandler<GetAppointmentPatientRefQuery, AppointmentPatientRefDto?>
{
    public async Task<AppointmentPatientRefDto?> Handle(
        GetAppointmentPatientRefQuery request,
        CancellationToken ct
    )
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
            patient.LocationId,
            patient.State?.Code,
            patient.UserId,
            // La organización del paciente vive en su clínica asignada (ERP):
            // la clínica viene incluida por el detalle del repositorio.
            patient.Clinic?.OrganizationId
        );
    }
}

public sealed class GetAppointmentSpecialtyRefQueryHandler(IOrganizationRepository organizations)
    : IRequestHandler<GetAppointmentSpecialtyRefQuery, AppointmentSpecialtyRefDto?>
{
    public async Task<AppointmentSpecialtyRefDto?> Handle(
        GetAppointmentSpecialtyRefQuery request,
        CancellationToken ct
    )
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
            specialty.Category
        );
    }
}

public sealed class GetAppointmentLocationRefQueryHandler(IOrganizationRepository organizations)
    : IRequestHandler<GetAppointmentLocationRefQuery, AppointmentLocationRefDto?>
{
    public async Task<AppointmentLocationRefDto?> Handle(
        GetAppointmentLocationRefQuery request,
        CancellationToken ct
    )
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
            location.IsActive
        );
    }
}

public sealed class GetAppointmentProfessionalByUserIdQueryHandler(IEmployeeRepository employees)
    : IRequestHandler<GetAppointmentProfessionalByUserIdQuery, AppointmentProfessionalRefDto?>
{
    public async Task<AppointmentProfessionalRefDto?> Handle(
        GetAppointmentProfessionalByUserIdQuery request,
        CancellationToken ct
    )
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
            employee
                .Professional.Specialties.Select(s => s.SpecialtyId)
                .Distinct()
                .Order()
                .ToList(),
            employee
                .ClinicAssignments.SelectMany(a => a.Clinic.Locations)
                .Select(l => l.Id)
                .Distinct()
                .ToList(),
            employee.ClinicAssignments.Select(a => a.ClinicId).Distinct().ToList()
        );
    }
}

public sealed class GetAppointmentPatientByUserIdQueryHandler(IPatientRepository patients)
    : IRequestHandler<GetAppointmentPatientByUserIdQuery, AppointmentPatientRefDto?>
{
    public async Task<AppointmentPatientRefDto?> Handle(
        GetAppointmentPatientByUserIdQuery request,
        CancellationToken ct
    )
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
            patient.LocationId,
            patient.State?.Code,
            patient.UserId,
            // La organización del paciente vive en su clínica asignada (ERP):
            // la clínica viene incluida por el detalle del repositorio.
            patient.Clinic?.OrganizationId
        );
    }
}
