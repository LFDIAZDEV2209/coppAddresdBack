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
/// </summary>
public record InviteEmployeeCommand(Guid EmployeeId, Guid? InvitedBy) : IRequest<InviteEmployeeResult>;

public sealed class InviteEmployeeCommandHandler(
    IEmployeeRepository employees,
    IAuthInvitationsClient auth,
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

        var invitation = await auth.CreateInvitationAsync(
            employee.Email, employee.FirstName, employee.LastName, ct);

        await employees.SetUserIdAsync(employee.Id, invitation.UserId, ct);

        logger.LogInformation("Empleado {EmployeeId} invitado (usuario {UserId})",
            employee.Id, invitation.UserId);

        return new InviteEmployeeResult(
            employee.Id,
            invitation.UserId,
            invitation.InvitationId,
            invitation.ExpiresAt,
            invitation.Link);
    }
}
