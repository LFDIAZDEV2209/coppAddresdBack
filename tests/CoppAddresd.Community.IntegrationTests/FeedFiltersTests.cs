using CoppAddresd.Community.Entities;
using CoppAddresd.Community.GraphQL.Queries;
using CoppAddresd.Community.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.Community.IntegrationTests;

/// <summary>
/// Tests de los filtros de moderación del resolver <see cref="CommunityQuery.Feed"/>:
/// coincidencia parcial e insensible a acentos por autor, por palabra en el cuerpo
/// y por rango de fechas de creación. Cada test siembra usuarios únicos porque la BD
/// de la colección es compartida. El Feed es una vista de moderación que no requiere
/// el perfil del llamante, por lo que se invoca directamente sin contexto HTTP.
/// </summary>
[Collection(CommunityTestCollection.Name)]
public sealed class FeedFiltersTests(CommunityTestDatabase dbFixture) : IDisposable
{
    private readonly CommunityTestContext _factory = new(dbFixture.ConnectionString);

    public void Dispose() => NpgsqlConnection.ClearAllPools();

    private static readonly DateTime Base = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Siembra un perfil y las publicaciones indicadas (cuerpo + fecha) para el usuario.
    /// </summary>
    private async Task<CommunityDbContext> SeedPostsAsync(
        string displayName, (string Body, DateTime CreatedAt)[] posts, CancellationToken ct = default)
    {
        var db = _factory.Create();
        var user = Guid.NewGuid();
        var profile = await CommunityTestData.SeedProfileAsync(db, user, displayName, ct);
        foreach (var (body, createdAt) in posts)
        {
            db.Posts.Add(new Post
            {
                Id = Guid.NewGuid(),
                ProfileId = profile.Id,
                Body = body,
                CreatedAt = createdAt,
            });
        }
        await db.SaveChangesAsync(ct);
        return db;
    }

    [Fact]
    public async Task Feed_FiltersByAuthor_UnaccentInsensitive()
    {
        // "María" lleva tilde; el filtro por "maria" (sin tilde) debe encontrarla.
        var db = await SeedPostsAsync("María", [( "Hola comunidad", Base )]);
        await using var _ = db;
        var otro = await SeedPostsAsync("Pedro", [( "Mensaje de Pedro", Base.AddHours(1) )]);
        await using var __ = otro;

        var feed = await new CommunityQuery().Feed(db, author: "maria", ct: CancellationToken.None);

        Assert.Single(feed);
        Assert.Equal("María", feed[0].Profile!.DisplayName);
        Assert.Equal("Hola comunidad", feed[0].Body);
    }

    [Fact]
    public async Task Feed_FiltersByWordAndDateRange()
    {
        // Mismo día (Base) y con la palabra "Avena": dentro del rango y la búsqueda.
        var db = await SeedPostsAsync("Ana",
        [
            ( "Avena para desayunar", Base.AddHours(1) ),
            ( "Caminata matutina", Base.AddHours(2) ),   // mismo día pero sin la palabra
            ( "Avena con miel", Base.AddHours(3) ),
            ( "Avena para cenar", Base.AddDays(1).AddHours(2) ), // palabra pero fuera del día
        ]);
        await using var _ = db;

        var from = Base.Date;            // 2026-08-10 00:00
        var to = Base.Date;              // inclusive hasta 2026-08-11 00:00
        var feed = await new CommunityQuery().Feed(
            db, search: "avena", from: from, to: to, ct: CancellationToken.None);

        // Solo las dos publicaciones del día Base que contienen "Avena".
        Assert.Equal(2, feed.Count);
        Assert.All(feed, p => Assert.Contains("Avena", p.Body));
    }
}
