using CoppAddresd.Community.Entities;
using CoppAddresd.Community.Persistence;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Community.GraphQL.Resolvers;

/// <summary>Resolvers derivados (no almacenados) del perfil: nivel por XP, riesgo de inactividad y contadores.</summary>
[ExtendObjectType(typeof(Profile))]
public sealed class ProfileResolvers
{
    /// <summary>Nombre del nivel gamificado derivado del XP total acumulado.</summary>
    public string LevelName([Parent] Profile profile)
        => CommunityStats.LevelName(profile.XpTotal);

    /// <summary>Nivel de riesgo de inactividad derivado de LastPostAt/LastActiveAt.</summary>
    public RiskLevel RiskLevel([Parent] Profile profile)
        => CommunityStats.ComputeRiskLevel(profile.LastPostAt, profile.LastActiveAt);

    /// <summary>Cantidad de publicaciones activas del perfil (soft-delete excluido).</summary>
    public async Task<int> PostsCount(
        [Parent] Profile profile,
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => await db.Posts.CountAsync(p => p.ProfileId == profile.Id && p.DeletedAt == null, ct);

    /// <summary>Cantidad de comentarios activos del perfil (soft-delete excluido).</summary>
    public async Task<int> CommentsCount(
        [Parent] Profile profile,
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => await db.Comments.CountAsync(c => c.ProfileId == profile.Id && c.DeletedAt == null, ct);

    /// <summary>Cantidad de likes del perfil.</summary>
    public async Task<int> LikesCount(
        [Parent] Profile profile,
        [Service] CommunityDbContext db,
        CancellationToken ct)
        => await db.Likes.CountAsync(l => l.ProfileId == profile.Id, ct);
}
