using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>
/// Actualiza un empleado: datos HR, asignaciones de clínicas (sync total) y
/// extensión profesional (sync total de especialidades y licencias). Los
/// campos null del payload no se tocan (PATCH semántico); las listas, si
/// llegan, reemplazan el estado actual.
/// </summary>
public record UpdateEmployeeCommand(
    Guid Id,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    string? Email,
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
    IReadOnlyList<LicenseInput>? Licenses,
    bool RemoveProfessionalExtension = false) : IRequest<EmployeeDto?>;

public sealed class UpdateEmployeeCommandHandler(
    IEmployeeRepository repository,
    IOrganizationRepository organizationRepository,
    IAuthScopedAssignmentsClient scopedAssignmentsClient,
    IAuthRolesClient authRolesClient,
    ILogger<UpdateEmployeeCommandHandler> logger) : IRequestHandler<UpdateEmployeeCommand, EmployeeDto?>
{
    public async Task<EmployeeDto?> Handle(UpdateEmployeeCommand request, CancellationToken ct)
    {
        var entity = await repository.GetByIdAsync(request.Id, ct);
        if (entity is null)
            return null;

        if (entity.Professional is not null && entity.UserId is not null && request.Status is not null
            && request.Status.Trim() != entity.Status)
            throw new BusinessRuleViolationException("Cambia el estado desde el control de acceso ERP del directorio.");

        // Capturar el estado anterior del profesional y sus clínicas antes de
        // las modificaciones, para calcular las clínicas recién agregadas.
        var hadProfessional = entity.Professional != null;
        var existingActiveClinicIds = entity.ClinicAssignments
            .Where(c => string.Equals(c.Status, "Active", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.ClinicId)
            .ToHashSet();

        // Email: normalizar y validar unicidad por organización.
        if (request.Email is not null)
        {
            var email = request.Email.Trim().ToLowerInvariant();
            if (await repository.EmailExistsInOrganizationAsync(
                    entity.OrganizationId, email, excludeEmployeeId: entity.Id, ct))
            {
                throw new BusinessRuleViolationException(
                    $"Ya existe un empleado con el correo '{email}' en esta organización.");
            }
            entity.Email = email;
        }

        if (request.FirstName is not null) entity.FirstName = request.FirstName.Trim();
        if (request.MiddleName is not null) entity.MiddleName = ProfessionalOptions.Normalize(request.MiddleName);
        if (request.LastName is not null) entity.LastName = request.LastName.Trim();
        if (request.PhoneCountryCode is not null) entity.PhoneCountryCode = ProfessionalOptions.Normalize(request.PhoneCountryCode);
        if (request.PhoneNumber is not null) entity.PhoneNumber = ProfessionalOptions.Normalize(request.PhoneNumber);
        if (request.JobTitle is not null) entity.JobTitle = ProfessionalOptions.Normalize(request.JobTitle);
        if (request.Department is not null) entity.Department = ProfessionalOptions.Normalize(request.Department);
        if (request.HireDate is not null) entity.HireDate = request.HireDate;
        if (request.Status is not null) entity.Status = request.Status.Trim();
        entity.UpdatedAt = DateTime.UtcNow;

        // Sync total de asignaciones de clínicas.
        if (request.Clinics is not null)
        {
            ApplyClinicAssignments(entity, request.Clinics);
        }

        // Extensión profesional: crear, actualizar o eliminar.
        if (request.RemoveProfessionalExtension)
        {
            entity.Professional = null;
        }
        else if (request.ProfessionalTypeId is not null
            || request.Bio is not null
            || request.SpecialtyIds is not null
            || request.Licenses is not null)
        {
            if (request.ProfessionalTypeId is not null
                && await organizationRepository.GetProfessionalTypeByIdAsync(request.ProfessionalTypeId.Value, ct) is null)
            {
                throw new UnprocessableEntityException("El tipo de profesional no existe en el catálogo.");
            }

            if (request.SpecialtyIds is not null)
            {
                foreach (var specialtyId in request.SpecialtyIds.Distinct())
                {
                    if (!await repository.SpecialtyExistsAsync(specialtyId, ct))
                        throw new UnprocessableEntityException($"La especialidad {specialtyId} no existe en el catálogo.");
                }
            }

            entity.Professional ??= new Professional
            {
                Id = Guid.NewGuid(),
                EmployeeId = entity.Id,
                CreatedAt = DateTime.UtcNow,
            };

            if (request.ProfessionalTypeId is not null)
                entity.Professional.ProfessionalTypeId = request.ProfessionalTypeId;
            if (request.Bio is not null)
                entity.Professional.Bio = ProfessionalOptions.Normalize(request.Bio);
            entity.Professional.UpdatedAt = DateTime.UtcNow;

            if (request.SpecialtyIds is not null)
            {
                var now = DateTime.UtcNow;
                entity.Professional.Specialties = request.SpecialtyIds
                    .Distinct()
                    .Select(specialtyId => new ProfessionalSpecialty
                    {
                        SpecialtyId = specialtyId,
                        IsPrimary = false,
                        CreatedAt = now,
                    })
                    .ToList();
            }

            if (request.Licenses is not null)
            {
                var now = DateTime.UtcNow;
                entity.Professional.Licenses = request.Licenses
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
                    .ToList();
            }
        }

        await repository.UpdateAsync(entity, ct);

        logger.LogInformation("Empleado actualizado: {Id}", entity.Id);

        // Invariante best-effort: si el empleado tiene extensión profesional
        // y usuario de Auth vinculado, asegurar que tenga el rol "Professional"
        // con scope de clínica para cada clínica activa recién agregada.
        if (entity.Professional is not null && entity.UserId is not null)
        {
            var currentActiveClinicIds = entity.ClinicAssignments
                .Where(c => string.Equals(c.Status, "Active", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.ClinicId)
                .ToHashSet();

            // Clínicas recién agregadas: las que están en las clínicas activas
            // actuales pero no estaban antes.
            // Si el profesional se acaba de crear (HR → profesional), tratar
            // TODAS las clínicas activas como "nuevas" para garantizar scopes.
            IEnumerable<Guid> newlyAdded;
            if (!hadProfessional)
            {
                // Conversión de HR a profesional: otorgar scopes para todas
                // las clínicas activas actuales.
                newlyAdded = currentActiveClinicIds;
            }
            else
            {
                newlyAdded = currentActiveClinicIds.Except(existingActiveClinicIds);
            }

            var clinicIdsToEnsure = newlyAdded.ToList();
            if (clinicIdsToEnsure.Count > 0)
            {
                await ProfessionalScopeSync.TryEnsureProfessionalScopesAsync(
                    scopedAssignmentsClient,
                    authRolesClient,
                    entity.UserId,
                    clinicIdsToEnsure,
                    logger,
                    ct);
            }
        }

        var updated = await repository.GetByIdAsync(entity.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer el empleado actualizado.");

        return EmployeeDto.FromEntity(updated);
    }

    private static void ApplyClinicAssignments(
        Employee entity,
        IReadOnlyList<ClinicAssignmentInput> assignments)
    {
        var now = DateTime.UtcNow;
        entity.ClinicAssignments = assignments
            .Select(a => new EmployeeClinic
            {
                ClinicId = a.ClinicId,
                IsPrimary = a.IsPrimary,
                Status = string.IsNullOrWhiteSpace(a.Status) ? "Active" : a.Status.Trim(),
                CreatedAt = now,
            })
            .ToList();
    }
}
