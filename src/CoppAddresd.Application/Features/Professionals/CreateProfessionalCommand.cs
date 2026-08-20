using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>
/// Resultado del flujo orquestado de creación de profesional.
/// </summary>
public record CreateProfessionalResult(
    Guid EmployeeId,
    Guid? InvitationId,
    DateTime? InvitationExpiresAt,
    string? InvitationLink);

/// <summary>
/// Crea un profesional de extremo a extremo en una sola operación:
///
/// <code>
/// crear empleado + extensión clínica + clínicas
///   → invitar (usuario + token en el Auth Service + correo)
///   → aplicar roles/permisos scoped por clínica (reemplazo atómico en Auth)
/// </code>
///
/// Con compensación: si la aplicación de scopes falla se revoca la invitación
/// y se elimina el empleado recién creado (nunca quedan estados a medias).
/// Si <paramref name="SendInvitation"/> es false el empleado se crea sin
/// usuario (estado Invited) y los scopes se configuran después, en el detalle.
/// </summary>
public record CreateProfessionalCommand(
    Guid OrganizationId,
    string FirstName,
    string? MiddleName,
    string LastName,
    string Email,
    string? PhoneCountryCode,
    string? PhoneNumber,
    string? JobTitle,
    DateOnly? HireDate,
    Guid? ProfessionalTypeId,
    string? Bio,
    IReadOnlyList<ClinicAssignmentInput>? Clinics,
    IReadOnlyList<Guid>? SpecialtyIds,
    IReadOnlyList<LicenseInput>? Licenses,
    IReadOnlyList<ScopedRoleAssignmentInput>? ScopedRoles,
    IReadOnlyList<ScopedPermissionAssignmentInput>? ScopedPermissions,
    bool SendInvitation,
    Guid? GrantedBy) : IRequest<CreateProfessionalResult>;

public sealed class CreateProfessionalCommandHandler(
    IMediator mediator,
    IEmployeeRepository employees,
    IAuthInvitationsClient auth,
    IAuthScopedAssignmentsClient scoped,
    ILogger<CreateProfessionalCommandHandler> logger) : IRequestHandler<CreateProfessionalCommand, CreateProfessionalResult>
{
    public async Task<CreateProfessionalResult> Handle(CreateProfessionalCommand request, CancellationToken ct)
    {
        var wantsScopes = request.ScopedRoles is { Count: > 0 }
            || request.ScopedPermissions is { Count: > 0 };

        if (wantsScopes && !request.SendInvitation)
        {
            throw new BusinessRuleViolationException(
                "Para asignar permisos por clínica se debe enviar la invitación: el usuario aún no existe.");
        }

        // 1. Crear el empleado (reutiliza el comando existente: validación +
        //    extensión profesional + clínicas en una sola transacción).
        var created = await mediator.Send(new CreateEmployeeCommand(
            request.OrganizationId,
            request.FirstName,
            request.MiddleName,
            request.LastName,
            request.Email,
            request.PhoneCountryCode,
            request.PhoneNumber,
            request.JobTitle,
            null,
            request.HireDate,
            null,
            request.ProfessionalTypeId,
            request.Bio,
            request.Clinics,
            request.SpecialtyIds,
            request.Licenses), ct);

        InvitationCreationResult? invitation = null;

        try
        {
            // 2. Invitación (crea usuario en Auth + correo con enlace).
            if (request.SendInvitation)
            {
                invitation = await auth.CreateInvitationAsync(
                    created.Email, created.FirstName, created.LastName, ct);

                await employees.SetUserIdAsync(created.Id, invitation.UserId, ct);
            }

            // 3. Scopes por clínica (reemplazo atómico en el Auth Service).
            if (request.SendInvitation && wantsScopes)
            {
                await scoped.ReplaceAsync(
                    invitation!.UserId,
                    request.ScopedRoles ?? [],
                    request.ScopedPermissions ?? [],
                    request.GrantedBy,
                    ct);
            }
        }
        catch
        {
            // Compensación: revoca la invitación y elimina el empleado recién
            // creado para no dejar estados a medias.
            if (invitation is not null)
            {
                try
                {
                    await auth.RevokeAsync(invitation.InvitationId, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "No se pudo revocar la invitación {InvitationId} durante la compensación",
                        invitation.InvitationId);
                }
            }

            await employees.DeleteAsync(created.Id, ct);
            throw;
        }

        logger.LogInformation(
            "Profesional creado e invitado: {EmployeeId} (usuario {UserId}, scopes aplicados: {Scopes})",
            created.Id, invitation?.UserId, wantsScopes);

        return new CreateProfessionalResult(
            created.Id,
            invitation?.InvitationId,
            invitation?.ExpiresAt,
            invitation?.Link);
    }
}