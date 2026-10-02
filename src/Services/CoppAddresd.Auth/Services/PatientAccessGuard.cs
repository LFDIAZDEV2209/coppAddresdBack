using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Interfaces;
using CoppAddresd.Auth.Security;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Auth.Services;

/// <summary>
/// Consulta de solo lectura a <c>app.patient_profiles</c> por <c>user_id</c>
/// (índice único) para bloquear el acceso de pacientes explícitamente
/// inactivos. El Auth Service no posee el esquema <c>app.</c>, por eso usa SQL
/// crudo (mismo patrón que <see cref="PatientLookupService"/> y el store de
/// preferencias de avatar). La comparación del estado se delega en
/// <see cref="PatientStatusRules"/>.
/// </summary>
public sealed class PatientAccessGuard(AuthDbContext dbContext) : IPatientAccessGuard
{
    /// <inheritdoc />
    public async Task<bool> IsBlockedAsync(Guid userId, CancellationToken ct = default)
    {
        var status = await dbContext
            .Database.SqlQueryRaw<string?>(
                """SELECT status AS "Value" FROM app.patient_profiles WHERE user_id = {0} LIMIT 1""",
                userId
            )
            .FirstOrDefaultAsync(ct);

        return PatientStatusRules.BlocksAppAccess(status);
    }
}
