using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.Telemedicine.Infrastructure.Persistence;

/// <summary>
/// Utilidades para traducir errores de guardado de EF. SaveChangesAsync envuelve
/// la <see cref="PostgresException"/> dentro de una <see cref="DbUpdateException"/>,
/// por lo que los repositorios deben inspeccionar el InnerException (no capturar
/// la PostgresException directa, que no llega a propagarse sola).
/// </summary>
internal static class DbUpdateExceptionExtensions
{
    /// <summary>¿El guardado falló por una violación de exclusión (solapamiento GiST)?</summary>
    public static bool IsExclusionViolation(this DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: var state }
           && state == PostgresErrorCodes.ExclusionViolation;

    /// <summary>¿El guardado falló por una violación de índice único?</summary>
    public static bool IsUniqueViolation(this DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: var state }
           && state == PostgresErrorCodes.UniqueViolation;
}
