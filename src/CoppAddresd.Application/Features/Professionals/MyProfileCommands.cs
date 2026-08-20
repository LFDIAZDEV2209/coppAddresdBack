using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Perfil del usuario actual (para el wizard de onboarding / mi perfil).</summary>
public record GetMyProfileQuery(Guid UserId) : IRequest<EmployeeDto?>;

public sealed class GetMyProfileQueryHandler(
    IEmployeeRepository repository) : IRequestHandler<GetMyProfileQuery, EmployeeDto?>
{
    public async Task<EmployeeDto?> Handle(GetMyProfileQuery request, CancellationToken ct)
    {
        var employee = await repository.GetByUserIdAsync(request.UserId, ct);
        return employee is null ? null : EmployeeDto.FromEntity(employee);
    }
}

/// <summary>
/// Actualiza el perfil del usuario actual (autogestión). Si
/// <c>CompleteOnboarding</c> es true, marca el onboarding terminado y activa
/// al empleado (transición Invited → Active). Solo toca datos propios; nunca
/// clínicas ni rol.
/// </summary>
public record UpdateMyProfileCommand(
    Guid UserId,
    Guid? ProfessionalTypeId,
    string? Bio,
    string? PhotoStorageKey,
    string? PhoneCountryCode,
    string? PhoneNumber,
    IReadOnlyList<Guid>? SpecialtyIds,
    IReadOnlyList<LicenseInput>? Licenses,
    bool CompleteOnboarding = false) : IRequest<EmployeeDto?>;

public sealed class UpdateMyProfileCommandHandler(
    IEmployeeRepository employees,
    IOrganizationRepository organizations,
    ILogger<UpdateMyProfileCommandHandler> logger) : IRequestHandler<UpdateMyProfileCommand, EmployeeDto?>
{
    public async Task<EmployeeDto?> Handle(UpdateMyProfileCommand request, CancellationToken ct)
    {
        var employee = await employees.GetByUserIdAsync(request.UserId, ct);
        if (employee is null)
        {
            throw new NotFoundException("No hay un perfil de empleado vinculado a tu cuenta.");
        }

        if (request.ProfessionalTypeId is not null
            && await organizations.GetProfessionalTypeByIdAsync(request.ProfessionalTypeId.Value, ct) is null)
        {
            throw new UnprocessableEntityException("El tipo de profesional no existe en el catálogo.");
        }

        if (request.SpecialtyIds is { Count: > 0 })
        {
            foreach (var specialtyId in request.SpecialtyIds.Distinct())
            {
                if (!await employees.SpecialtyExistsAsync(specialtyId, ct))
                    throw new UnprocessableEntityException($"La especialidad {specialtyId} no existe en el catálogo.");
            }
        }

        await employees.CompleteOnboardingAsync(
            employee.Id,
            request.ProfessionalTypeId,
            request.Bio,
            request.PhotoStorageKey,
            request.PhoneCountryCode,
            request.PhoneNumber,
            request.SpecialtyIds ?? [],
            request.Licenses ?? [],
            request.CompleteOnboarding,
            ct);

        logger.LogInformation("Perfil actualizado (onboarding completo: {Onboarding}) del empleado {EmployeeId}",
            request.CompleteOnboarding, employee.Id);

        var updated = await employees.GetByUserIdAsync(request.UserId, ct)
            ?? throw new InvalidOperationException("No se pudo releer el perfil actualizado.");

        return EmployeeDto.FromEntity(updated);
    }
}
