using CoppAddresd.Community.Entities;

namespace CoppAddresd.Community.GraphQL.Types;

/// <summary>Estadísticas de la comunidad por región.</summary>
public sealed record RegionStat
{
    public ProfileRegion Region { get; init; }
    public int Members { get; init; }
    public double PostsPerWeek { get; init; }
}

/// <summary>Estadísticas de la comunidad por diagnóstico.</summary>
public sealed record DiagnosticStat
{
    public ProfileDiagnosis Diagnosis { get; init; }
    public int Members { get; init; }
    public double PostsPerWeek { get; init; }
    public double AvgStreak { get; init; }
    public double AvgXp { get; init; }
    public double Adherence { get; init; }
}

/// <summary>Resumen de actividad de feed en las últimas 24 horas.</summary>
public sealed record FeedToday
{
    public int Posts { get; init; }
    public int Comments { get; init; }
    public int Reactions { get; init; }
    public int NewMembers { get; init; }
    public int StreaksBroken { get; init; }
    public int XpDelivered { get; init; }
}

/// <summary>Rango para distribuciones (streak, inactividad).</summary>
public sealed record RangeBucket
{
    public string Range { get; init; } = default!;
    public int Value { get; init; }
}

/// <summary>Resumen general de rachas.</summary>
public sealed record StreakOverview
{
    public int LongestStreak { get; init; }
    public Guid? LongestProfileId { get; init; }
    public string? LongestProfileName { get; init; }
    public int MembersOverSevenDays { get; init; }
    public int MilestonesThisMonth { get; init; }
    public int StreaksBroken { get; init; }
    public IReadOnlyList<RangeBucket> Distribution { get; init; } = [];
}

/// <summary>Punto de la serie semanal de XP.</summary>
public sealed record XpWeekPoint
{
    /// <summary>Etiqueta de la semana en formato "d/M" (estilo español, ej. "24/8").</summary>
    public string Label { get; init; } = default!;

    /// <summary>XP otorgado por razón de racha.</summary>
    public int Rachas { get; init; }

    /// <summary>XP otorgado por razón de post.</summary>
    public int Posts { get; init; }

    /// <summary>XP otorgado por otras razones (ERP).</summary>
    public int Erp { get; init; }
}

/// <summary>Analytics completo del dashboard de la comunidad.</summary>
public sealed record CommunityAnalytics
{
    public FeedToday FeedToday { get; init; } = null!;
    public StreakOverview StreakOverview { get; init; } = null!;
    public IReadOnlyList<RangeBucket> InactivityDistribution { get; init; } = [];
    public IReadOnlyList<XpWeekPoint> XpDeliveredSeries { get; init; } = [];
}

/// <summary>Grupo de chat resumido (para la consulta communityGroups).</summary>
public sealed record GroupSummary
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public int MemberCount { get; init; }
    public int MessageCount { get; init; }
    public DateTimeOffset? LastActivityAt { get; init; }
}

/// <summary>Alcance de mensajes del sistema (TODOS, INACTIVOS, ACTIVOS7).</summary>
public sealed record MessageReach
{
    public string Scope { get; init; } = default!;
    public int Total { get; init; }
    public int Reached { get; init; }
}

/// <summary>Perfil resumido para reconocimientos (id, displayName, isSystem).</summary>
public sealed record RecognitionProfile
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = default!;
    public bool IsSystem { get; init; }
}

/// <summary>Reconocimiento enriquecido con perfil (para GraphQL).</summary>
public sealed record RecognitionDto
{
    public Guid Id { get; init; }
    public Guid ProfileId { get; init; }
    public string TypeLabel { get; init; } = default!;
    public int Xp { get; init; }
    public string Status { get; init; } = default!;
    public DateTimeOffset CreatedAt { get; init; }
    public RecognitionProfile? Profile { get; init; }
}
