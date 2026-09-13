using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Professionals;

public sealed record ChangeProfessionalAccessCommand(
    Guid EmployeeId,
    Guid ActorId,
    Guid OperationId,
    string Status
) : IRequest<ProfessionalAccessResult>;

public sealed record ProfessionalAccessResult(Guid OperationId, string Status, bool Pending);

public sealed class ChangeProfessionalAccessCommandHandler(
    IEmployeeRepository employees,
    IErpAccessClient auth,
    IProfessionalAccessProjectionRepository projection,
    ILogger<ChangeProfessionalAccessCommandHandler> logger
) : IRequestHandler<ChangeProfessionalAccessCommand, ProfessionalAccessResult>
{
    public async Task<ProfessionalAccessResult> Handle(
        ChangeProfessionalAccessCommand request,
        CancellationToken ct
    )
    {
        if (request.OperationId == Guid.Empty || request.Status is not ("Active" or "Inactive"))
            throw new BusinessRuleViolationException("Estado u operación inválidos.");
        var employee =
            await employees.GetByIdAsync(request.EmployeeId, ct)
            ?? throw new BusinessRuleViolationException("Profesional no encontrado.");
        if (
            employee.Professional is null
            || employee.UserId is null
            || employee.Status == "Invited"
        )
            throw new BusinessRuleViolationException(
                "El profesional debe completar su invitación antes de cambiar el acceso."
            );
        if (employee.UserId == request.ActorId && request.Status == "Inactive")
            throw new BusinessRuleViolationException(
                "No puedes desactivar tu propio acceso al ERP."
            );

        var operation = await auth.ChangeAsync(
            request.OperationId,
            employee.UserId.Value,
            employee.Id,
            request.Status,
            ct
        );
        try
        {
            if (
                !await projection.ApplyAsync(
                    employee.Id,
                    employee.UserId.Value,
                    operation.Status,
                    operation.SessionVersion,
                    ct
                )
            )
                throw new InvalidOperationException(
                    "El vínculo del empleado cambió durante la sincronización."
                );
            await auth.CompleteAsync(operation.Id, ct);
            return new(operation.Id, operation.Status, false);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(
                exception,
                "Acceso ERP {OperationId} pendiente de proyección",
                operation.Id
            );
            return new(operation.Id, operation.Status, true);
        }
    }
}
