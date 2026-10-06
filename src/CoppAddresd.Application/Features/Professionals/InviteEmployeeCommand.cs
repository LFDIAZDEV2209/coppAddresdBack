using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Resultado de invitar a un empleado.</summary>
public record InviteEmployeeResult(
    Guid EmployeeId,
    Guid UserId,
    Guid InvitationId,
    DateTime ExpiresAt,
    string? InvitationLink);

/// <summary>
/// Invita a un empleado: crea el usuario (sin password) en el Auth Service,
/// genera la invitación y vincula <c>user_id</c> al empleado. El correo con el
/// enlace lo envía el Auth Service; en dev el enlace viaja en la respuesta.
///
/// Si el usuario ya existe (adoptExisting), se vincula directamente sin duplicar.
///
/// Opcionalmente acepta <see cref="ScopedRoles"/> que se aplican best-effort
/// tras vincular el usuario mediante una unión pura: se preservan las
/// asignaciones existentes de la cuenta y se agregan las nuevas respetando el
/// scope (el wizard solo agrega; las remociones se hacen en Usuarios).
/// </summary>
public record InviteEmployeeCommand(
    Guid EmployeeId,
    Guid? InvitedBy,
    IReadOnlyList<ScopedRoleAssignmentInput>? ScopedRoles = null) : IRequest<InviteEmployeeResult>;

public sealed class InviteEmployeeCommandHandler(
    IEmployeeRepository employees,
    IAuthInvitationsClient auth,
    IAuthScopedAssignmentsClient scopedAssignmentsClient,
    ILogger<InviteEmployeeCommandHandler> logger) : IRequestHandler<InviteEmployeeCommand, InviteEmployeeResult>
{
    public async Task<InviteEmployeeResult> Handle(InviteEmployeeCommand request, CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(request.EmployeeId, ct)
            ?? throw new NotFoundException("Empleado no encontrado.");

        if (employee.UserId is not null)
        {
            throw new BusinessRuleViolationException(
                "El empleado ya tiene un usuario vinculado. Use reenviar invitación si es necesario.");
        }

        // adoptExisting: true — vincula usuarios existentes sin duplicar.
        var invitation = await auth.CreateInvitationAsync(
            employee.Email, employee.FirstName, employee.LastName,
            adoptExisting: true, ct: ct);

        await employees.SetUserIdAsync(employee.Id, invitation.UserId, ct);

        if (invitation.HasPassword)
        {
            // Usuario existente con contraseña vinculado directamente; no hay invitación nueva.
            logger.LogInformation(
                "Empleado {EmployeeId}: usuario existente con contraseña vinculado; sin nueva invitación",
                employee.Id);
        }
        else
        {
            logger.LogInformation("Empleado {EmployeeId} invitado (usuario {UserId})",
                employee.Id, invitation.UserId);
        }

        // Roles scoped best-effort: unión pura con lo que la cuenta ya tenga
        // (preserva asignaciones existentes y respeta el scope de cada una).
        if (request.ScopedRoles is { Count: > 0 })
        {
            var userId = invitation.UserId;

            try
            {
                var current = await scopedAssignmentsClient.GetAsync(userId, ct);
                var (mergedRoles, mergedPermissions) = ScopedAssignmentsMerge.Union(
                    current,
                    request.ScopedRoles,
                    []);

                await scopedAssignmentsClient.ReplaceAsync(
                    userId,
                    mergedRoles,
                    mergedPermissions,
                    request.InvitedBy,
                    ct);

                logger.LogInformation(
                    "Empleado {EmployeeId}: scopes fusionados (unión pura) para el usuario {UserId}",
                    employee.Id, userId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Empleado {EmployeeId}: no se pudieron fusionar los roles scoped al usuario {UserId}",
                    employee.Id, userId);
            }
        }

        return new InviteEmployeeResult(
            employee.Id,
            invitation.UserId,
            invitation.InvitationId,
            invitation.ExpiresAt,
            invitation.Link);
    }
}
