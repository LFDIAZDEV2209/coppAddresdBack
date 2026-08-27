using CoppAddresd.Community.Entities;
using HotChocolate;
using HotChocolate.Types;

namespace CoppAddresd.Community.GraphQL.Resolvers;

/// <summary>Resolvers derivados (no almacenados) del perfil: nivel por XP y riesgo de inactividad.</summary>
[ExtendObjectType(typeof(Profile))]
public sealed class ProfileResolvers
{
    /// <summary>Nombre del nivel gamificado derivado del XP total acumulado.</summary>
    public string LevelName([Parent] Profile profile)
        => CommunityStats.LevelName(profile.XpTotal);

    /// <summary>Nivel de riesgo de inactividad derivado de LastPostAt/LastActiveAt.</summary>
    public RiskLevel RiskLevel([Parent] Profile profile)
        => CommunityStats.ComputeRiskLevel(profile.LastPostAt, profile.LastActiveAt);
}
