using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Common;

/// <summary>
/// Resultado de la sincronización de scopes de un profesional.
/// </summary>
public record ProfessionalScopeSyncResult(bool Ok, int Granted);

/// <summary>
/// Helper estático (best-effort) que asegura que un profesional tenga el
/// rol "Professional" con scope de clínica para cada una de sus clínicas
/// activas. Solo agrega; nunca remueve ni toca permisos personalizados.
/// Lanzar en el handler de UpdateEmployeeCommand y en el backfill.
/// </summary>
public static class ProfessionalScopeSync
{
    private const string ProfessionalRoleName = "Professional";
    private const string ClinicScopeType = "Clinic";

    /// <summary>
    /// Asegura que el usuario tenga el rol "Professional" con scope de clínica
    /// para cada una de las clínicas indicadas. Si el usuario no tiene userId
    /// o no hay clínicas, retorna ok sin hacer nada. Si el rol "Professional"
    /// no se encuentra, se tolera (warning log) y se retorna ok.
    /// </summary>
    public static async Task<ProfessionalScopeSyncResult> TryEnsureProfessionalScopesAsync(
        IAuthScopedAssignmentsClient client,
        IAuthRolesClient rolesClient,
        Guid? userId,
        IEnumerable<Guid> clinicIds,
        ILogger logger,
        CancellationToken ct)
    {
        if (userId is null)
            return new ProfessionalScopeSyncResult(true, 0);

        var distinctClinicIds = clinicIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (distinctClinicIds.Count == 0)
            return new ProfessionalScopeSyncResult(true, 0);

        try
        {
            // Resolver el rol "Professional" por nombre.
            var role = await rolesClient.GetRoleByNameAsync(ProfessionalRoleName, ct);
            if (role is null)
            {
                logger.LogWarning(
                    "El rol '{RoleName}' no fue encontrado en el Auth Service. " +
                    "Se omite la sincronización de scopes para el usuario {UserId}.",
                    ProfessionalRoleName, userId.Value);
                return new ProfessionalScopeSyncResult(true, 0);
            }

            // Leer asignaciones scoped actuales del usuario.
            var current = await client.GetAsync(userId.Value, ct);

            // Determinar clínicas que faltan: no tienen el rol Professional con ese scope.
            var existingProfessionalClinicScopeIds = current.Roles
                .Where(r => r.RoleId == role.Id
                    && r.ScopeType == ClinicScopeType
                    && r.ScopeId.HasValue)
                .Select(r => r.ScopeId!.Value)
                .ToHashSet();

            var missing = distinctClinicIds
                .Where(c => !existingProfessionalClinicScopeIds.Contains(c))
                .ToList();

            if (missing.Count == 0)
                return new ProfessionalScopeSyncResult(true, 0);

            // Merge: preservar roles existentes + agregar los faltantes.
            var mergedRoles = current.Roles
                .Select(r => new ScopedRoleAssignmentInput(r.RoleId, r.ScopeType, r.ScopeId))
                .Concat(missing.Select(c => new ScopedRoleAssignmentInput(role.Id, ClinicScopeType, c)))
                .ToList();

            // Permisos: pasar sin cambios (convertir a Input).
            var mergedPermissions = current.Permissions
                .Select(p => new ScopedPermissionAssignmentInput(
                    p.PermissionId, p.ScopeType, p.ScopeId, p.Effect))
                .ToList();

            await client.ReplaceAsync(
                userId.Value,
                mergedRoles,
                mergedPermissions,
                grantedBy: null,
                ct);

            logger.LogInformation(
                "Se agregaron {Count} scopes de clínica Professional para el usuario {UserId}.",
                missing.Count, userId.Value);

            return new ProfessionalScopeSyncResult(true, missing.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Error al sincronizar scopes de clínica Professional para el usuario {UserId}. " +
                "La operación se tolera (best-effort).",
                userId.Value);
            return new ProfessionalScopeSyncResult(false, 0);
        }
    }
}
