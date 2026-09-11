using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Maintenance;

/// <summary>
/// Resultado del backfill de scopes de profesional.
/// </summary>
public record BackfillProfessionalScopesResult(
    int Processed,
    int Granted,
    int SkippedNoUser,
    int Failed,
    string Message);

/// <summary>
/// Comando de mantenimiento que recorre todos los profesionales con usuario
/// y asegura que tengan el rol "Professional" con scope de clínica para cada
/// una de sus clínicas activas. Idempotente: si ya tienen los scopes, no
/// modifica nada.
/// </summary>
public record BackfillProfessionalScopesCommand(
    Guid? GrantedBy) : IRequest<BackfillProfessionalScopesResult>;

public sealed class BackfillProfessionalScopesCommandHandler(
    IEmployeeRepository employeeRepository,
    IAuthScopedAssignmentsClient scopedAssignmentsClient,
    IAuthRolesClient authRolesClient,
    ILogger<BackfillProfessionalScopesCommandHandler> logger)
    : IRequestHandler<BackfillProfessionalScopesCommand, BackfillProfessionalScopesResult>
{
    public async Task<BackfillProfessionalScopesResult> Handle(
        BackfillProfessionalScopesCommand request,
        CancellationToken ct)
    {
        // Verificar que el rol "Professional" existe antes de procesar.
        var professionalRole = await authRolesClient.GetRoleByNameAsync("Professional", ct);
        if (professionalRole is null)
        {
            return new BackfillProfessionalScopesResult(
                0, 0, 0, 0,
                "El rol 'Professional' no fue encontrado en el Auth Service. Backfill omitido.");
        }

        var memberships = await employeeRepository.GetProfessionalClinicMembershipsAsync(ct);

        var processed = 0;
        var granted = 0;
        var skippedNoUser = 0;
        var failed = 0;

        foreach (var membership in memberships)
        {
            processed++;

            if (membership.UserId is null)
            {
                skippedNoUser++;
                continue;
            }

            if (membership.ActiveClinicIds.Count == 0)
                continue;

            var result = await ProfessionalScopeSync.TryEnsureProfessionalScopesAsync(
                scopedAssignmentsClient,
                authRolesClient,
                membership.UserId,
                membership.ActiveClinicIds,
                logger,
                ct);

            if (result.Ok)
                granted += result.Granted;
            else
                failed++;
        }

        var message = $"Backfill completado: {processed} procesados, {granted} scopes " +
            $"agregados, {skippedNoUser} omitidos (sin usuario), {failed} fallidos.";

        logger.LogInformation(message);

        return new BackfillProfessionalScopesResult(processed, granted, skippedNoUser, failed, message);
    }
}
