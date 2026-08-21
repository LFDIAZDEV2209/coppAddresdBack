using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Asigna un profesional clínico a un paciente (relación del dominio
/// "mis pacientes"). La asigna el staff administrativo (Patients.Update);
/// la auto-asignación al crear un paciente la hace el propio flujo de
/// creación. Requiere que ambos existan y que el profesional sea clínico.
/// </summary>
public sealed record AssignPatientProfessionalCommand(
    Guid PatientId,
    Guid ProfessionalId,
    Guid? ClinicId,
    string? RelationshipType,
    Guid? GrantedBy)
    : IRequest<PatientProfessionalAssignmentView>;

public sealed class AssignPatientProfessionalCommandHandler(
    IPatientRepository repository,
    IEmployeeRepository employees)
    : IRequestHandler<AssignPatientProfessionalCommand, PatientProfessionalAssignmentView>
{
    public async Task<PatientProfessionalAssignmentView> Handle(
        AssignPatientProfessionalCommand request,
        CancellationToken ct)
    {
        if (!await repository.ExistsAsync(request.PatientId, ct))
        {
            throw new NotFoundException("Paciente no encontrado.");
        }

        // El profesional debe existir y tener extensión clínica (erp.professionals).
        var employee = await employees.GetByProfessionalIdAsync(request.ProfessionalId, ct);
        if (employee?.Professional is null)
        {
            throw new NotFoundException("Profesional no encontrado.");
        }

        var relationshipType = string.IsNullOrWhiteSpace(request.RelationshipType)
            ? "Assigned"
            : request.RelationshipType.Trim();

        await repository.AssignProfessionalAsync(
            request.PatientId,
            request.ProfessionalId,
            request.ClinicId,
            relationshipType,
            request.GrantedBy,
            ct);

        return (await repository.ListAssignmentsAsync(request.PatientId, ct))
            .First(a => a.ProfessionalId == request.ProfessionalId);
    }
}

/// <summary>Desasigna un profesional de un paciente (soft: marca Inactive).</summary>
public sealed record RemovePatientProfessionalCommand(
    Guid PatientId,
    Guid ProfessionalId)
    : IRequest<bool>;

public sealed class RemovePatientProfessionalCommandHandler(
    IPatientRepository repository)
    : IRequestHandler<RemovePatientProfessionalCommand, bool>
{
    public async Task<bool> Handle(RemovePatientProfessionalCommand request, CancellationToken ct)
    {
        if (!await repository.ExistsAsync(request.PatientId, ct))
        {
            throw new NotFoundException("Paciente no encontrado.");
        }

        await repository.RemoveProfessionalAsync(request.PatientId, request.ProfessionalId, ct);
        return true;
    }
}

/// <summary>Asignaciones de un paciente (para el detalle: quién lo atiende).</summary>
public sealed record ListPatientAssignmentsQuery(Guid PatientId)
    : IRequest<IReadOnlyList<PatientProfessionalAssignmentView>>;

public sealed class ListPatientAssignmentsQueryHandler(
    IPatientRepository repository)
    : IRequestHandler<ListPatientAssignmentsQuery, IReadOnlyList<PatientProfessionalAssignmentView>>
{
    public async Task<IReadOnlyList<PatientProfessionalAssignmentView>> Handle(
        ListPatientAssignmentsQuery request,
        CancellationToken ct)
    {
        if (!await repository.ExistsAsync(request.PatientId, ct))
        {
            throw new NotFoundException("Paciente no encontrado.");
        }

        return await repository.ListAssignmentsAsync(request.PatientId, ct);
    }
}

/// <summary>
/// ¿El paciente tiene una asignación activa con el profesional dado? (alcance
/// de datos "propios"). El professionalId lo resuelve el backend desde la
/// identidad del JWT; el query nunca acepta ids del cliente.
/// </summary>
public sealed record PatientIsAssignedQuery(Guid PatientId, Guid ProfessionalId)
    : IRequest<bool>;

public sealed class PatientIsAssignedQueryHandler(
    IPatientRepository repository)
    : IRequestHandler<PatientIsAssignedQuery, bool>
{
    public Task<bool> Handle(PatientIsAssignedQuery request, CancellationToken ct)
        => repository.IsAssignedToProfessionalAsync(request.PatientId, request.ProfessionalId, ct);
}