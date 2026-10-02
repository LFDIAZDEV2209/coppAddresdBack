using CoppAddresd.Auth.Data;
using CoppAddresd.Auth.Services;
using CoppAddresd.UnitTests.Auth.TestDoubles;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.UnitTests.Auth.Services;

/// <summary>
/// Verifica contra SQLite en memoria el SQL crudo del guard. SQLite no tiene
/// esquemas: se emula <c>app.patient_profiles</c> adjuntando una base en
/// memoria como <c>app</c> y creando la tabla (la query real del Auth Service
/// apunta al esquema <c>app.</c> de PostgreSQL). El llamador debe disponer el
/// contexto y la conexión abierta que devuelve el helper.
/// </summary>
public sealed class PatientAccessGuardTests
{
    private static async Task<(AuthDbContext Db, SqliteConnection Connection)> CreateContextWithProfileAsync(
        Guid userId,
        string? status
    )
    {
        var (db, connection) = IdentityTestDoubles.CreateSqliteDbContext();
        await db.Database.ExecuteSqlRawAsync("ATTACH DATABASE ':memory:' AS app;");
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TABLE app.patient_profiles (user_id TEXT NOT NULL, status TEXT NULL);"
        );
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO app.patient_profiles (user_id, status) VALUES ({userId}, {status})"
        );
        return (db, connection);
    }

    [Theory]
    [InlineData("Inactivo", true)]
    [InlineData("Activo", false)]
    [InlineData("inactivo", false)]
    [InlineData(null, false)]
    public async Task IsBlockedAsync_EvaluaElEstadoDelPerfil(string? status, bool expected)
    {
        var userId = Guid.NewGuid();
        var (db, connection) = await CreateContextWithProfileAsync(userId, status);
        await using var _ = db;
        using var __ = connection;
        var guard = new PatientAccessGuard(db);

        Assert.Equal(expected, await guard.IsBlockedAsync(userId));
    }

    [Fact]
    public async Task IsBlockedAsync_StaffSinPerfilDePaciente_NoBloquea()
    {
        // Hay una fila inactiva en la tabla, pero pertenece a otro usuario:
        // el staff sin perfil propio no debe quedar bloqueado.
        var (db, connection) = await CreateContextWithProfileAsync(Guid.NewGuid(), "Inactivo");
        await using var _ = db;
        using var __ = connection;
        var guard = new PatientAccessGuard(db);

        Assert.False(await guard.IsBlockedAsync(Guid.NewGuid()));
    }
}
