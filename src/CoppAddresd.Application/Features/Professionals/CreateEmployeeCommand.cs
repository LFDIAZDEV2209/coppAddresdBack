using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Payload de asignación a clínica dentro del comando de empleado.</summary>
public record ClinicAssignmentInput(Guid ClinicId, bool IsPrimary, string Status);

/// <summary>Payload de licencia dentro del comando de empleado.</summary>
public record LicenseInput(
    string LicenseType,
    Guid? SpecialtyId,
    string? Number,
    Guid? StateId,
    string? Issuer,
    DateOnly? IssuedAt,
    DateOnly? ExpiresAt,
    string VerificationStatus);

/// <summary>
/// Crea un empleado (núcleo HR) con sus asignaciones de clínicas y,
/// opcionalmente, la extensión profesional (tipo, especialidades, licencias).
/// La creación del usuario en el Auth Service y la invitación llegan en Fase 3:
/// hoy el empleado nace con <c>UserId = null</c> y estado Invited.
/// </summary>
public record CreateEmployeeCommand(
    Guid OrganizationId,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? JobTitle,
    string? Department,
    DateOnly? HireDate,
    string? Status,
    Guid? ProfessionalTypeId,
    string? Bio,
    IReadOnlyList<ClinicAssignmentInput>? Clinics,
    IReadOnlyList<Guid>? SpecialtyIds,
    IReadOnlyList<LicenseInput>? Licenses) : IRequest<EmployeeDto>;

public sealed class CreateEmployeeCommandHandler(
    IEmployeeRepository repository,
    IOrganizationRepository organizationRepository,
    ILogger<CreateEmployeeCommandHandler> logger) : IRequestHandler<CreateEmployeeCommand, EmployeeDto>
{
    public async Task<EmployeeDto> Handle(CreateEmployeeCommand request, CancellationToken ct)
    {
        var organization = await organizationRepository.GetOrganizationByIdAsync(request.OrganizationId, ct)
            ?? throw new UnprocessableEntityException("La organización no existe.");

        var email = request.Email.Trim().ToLowerInvariant();
        if (await repository.EmailExistsInOrganizationAsync(organization.Id, email, ct: ct))
            throw new BusinessRuleViolationException(
                $"Ya existe un empleado con el correo '{email}' en esta organización.");

        var hasClinicalExtension = request.ProfessionalTypeId is not null
            || request.SpecialtyIds is { Count: > 0 }
            || request.Licenses is { Count: > 0 };

        if (hasClinicalExtension)
        {
            await ValidateClinicalPayloadAsync(request, ct);
        }

        var entity = new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            FirstName = request.FirstName.Trim(),
            MiddleName = ProfessionalOptions.Normalize(request.MiddleName),
            LastName = request.LastName.Trim(),
            Email = email,
            PhoneCountryCode = ProfessionalOptions.Normalize(request.PhoneCountryCode),
            PhoneNumber = ProfessionalOptions.Normalize(request.PhoneNumber),
            JobTitle = ProfessionalOptions.Normalize(request.JobTitle),
            Department = ProfessionalOptions.Normalize(request.Department),
            HireDate = request.HireDate,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Invited" : request.Status.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        ApplyClinicAssignments(entity, request.Clinics);

        if (hasClinicalExtension)
        {
            entity.Professional = BuildProfessionalExtension(request);
        }

        await repository.AddAsync(entity, ct);

        logger.LogInformation("Empleado creado: {Id} ({FirstName} {LastName}, profesional: {IsProfessional})",
            entity.Id, entity.FirstName, entity.LastName, entity.Professional is not null);

        var created = await repository.GetByIdAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el empleado creado.");

        return EmployeeDto.FromEntity(created);
    }

    private async Task ValidateClinicalPayloadAsync(CreateEmployeeCommand request, CancellationToken ct)
    {
        if (request.ProfessionalTypeId is not null
            && await organizationRepository.GetProfessionalTypeByIdAsync(request.ProfessionalTypeId.Value, ct) is null)
        {
            throw new UnprocessableEntityException("El tipo de profesional no existe en el catálogo.");
        }

        if (request.SpecialtyIds is { Count: > 0 })
        {
            foreach (var specialtyId in request.SpecialtyIds.Distinct())
            {
                if (!await repository.SpecialtyExistsAsync(specialtyId, ct))
                    throw new UnprocessableEntityException($"La especialidad {specialtyId} no existe en el catálogo.");
            }
        }
    }

    private static void ApplyClinicAssignments(
        Employee entity,
        IReadOnlyList<ClinicAssignmentInput>? assignments)
    {
        var now = DateTime.UtcNow;
        entity.ClinicAssignments = (assignments ?? [])
            .Select(a => new EmployeeClinic
            {
                ClinicId = a.ClinicId,
                IsPrimary = a.IsPrimary,
                Status = string.IsNullOrWhiteSpace(a.Status) ? "Active" : a.Status.Trim(),
                CreatedAt = now,
            })
            .ToList();
    }

    private static Professional BuildProfessionalExtension(CreateEmployeeCommand request)
    {
        var now = DateTime.UtcNow;

        return new Professional
        {
            Id = Guid.NewGuid(),
            ProfessionalTypeId = request.ProfessionalTypeId,
            Bio = ProfessionalOptions.Normalize(request.Bio),
            CreatedAt = now,
            Specialties = (request.SpecialtyIds ?? [])
                .Distinct()
                .Select(specialtyId => new ProfessionalSpecialty
                {
                    SpecialtyId = specialtyId,
                    IsPrimary = false,
                    CreatedAt = now,
                })
                .ToList(),
            Licenses = (request.Licenses ?? [])
                .Select(l => new ProfessionalLicense
                {
                    Id = Guid.NewGuid(),
                    LicenseType = l.LicenseType.Trim(),
                    SpecialtyId = l.SpecialtyId,
                    Number = ProfessionalOptions.Normalize(l.Number),
                    StateId = l.StateId,
                    Issuer = ProfessionalOptions.Normalize(l.Issuer),
                    IssuedAt = l.IssuedAt,
                    ExpiresAt = l.ExpiresAt,
                    VerificationStatus = string.IsNullOrWhiteSpace(l.VerificationStatus) ? "Pending" : l.VerificationStatus.Trim(),
                    CreatedAt = now,
                })
                .ToList(),
        };
    }
}
