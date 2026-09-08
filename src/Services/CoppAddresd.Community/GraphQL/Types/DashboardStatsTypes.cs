using CoppAddresd.Community.Entities;

namespace CoppAddresd.Community.GraphQL.Types;

/// <summary>Tendencias (deltas %) de los KPIs del dashboard de la comunidad.</summary>
public sealed record DashboardKpiTrends
{
    /// <summary>% de crecimiento de miembros activos entre ventanas de 30 días (firmado).</summary>
    public double ActiveMembers { get; init; }

    /// <summary>% de crecimiento de publicaciones del mes vs mes anterior.</summary>
    public double PostsThisMonth { get; init; }

    /// <summary>Delta absoluto de la tasa de participación semanal vs semana anterior.</summary>
    public double ParticipationRate { get; init; }

    /// <summary>% de cambio de inactivos 7d vs hace 30 días (negativo = mejora).</summary>
    public double InactiveOver7Days { get; init; }
}

/// <summary>Punto diario de la serie temporal de actividad (últimos 30 días).</summary>
public sealed record ActivityDay
{
    /// <summary>
    /// Etiqueta del día en formato "MMM d" (ej. "sep 4"): incluye el mes para no
    /// colisionar cuando el rango de 30 días cruza de mes (dos etiquetas "1", etc.).
    /// </summary>
    public string Dia { get; init; } = default!;

    /// <summary>Publicaciones creadas ese día.</summary>
    public int Posts { get; init; }

    /// <summary>Comentarios creados ese día.</summary>
    public int Comentarios { get; init; }

    /// <summary>Reacciones (likes) creadas ese día.</summary>
    public int Reacciones { get; init; }
}

/// <summary>Conteo de publicaciones por tipo (un entry por valor del enum PostType).</summary>
public sealed record PostTypeCount
{
    public PostType Type { get; init; }
    public int Count { get; init; }
}

/// <summary>Conteo de actividad agrupada por hora del día (0..23).</summary>
public sealed record PeakHour
{
    public int Hora { get; init; }
    public int Count { get; init; }
}

/// <summary>Tasa de participación semanal de un grupo de diagnóstico.</summary>
public sealed record DiagnosisParticipation
{
    public ProfileDiagnosis Diagnosis { get; init; }
    public double Participation { get; init; }
}

/// <summary>Estadísticas agregadas del dashboard de la comunidad ERP.</summary>
public sealed record DashboardStats
{
    public int ActiveMembers { get; init; }
    public int PostsThisMonth { get; init; }
    public double ParticipationRate { get; init; }
    public int InactiveOver7Days { get; init; }
    public int InactiveAtRisk { get; init; }
    public DashboardKpiTrends KpiTrends { get; init; } = null!;
    public IReadOnlyList<ActivityDay> ActivitySeries { get; init; } = [];
    public IReadOnlyList<PostTypeCount> PostTypes { get; init; } = [];
    public IReadOnlyList<PeakHour> PeakHours { get; init; } = [];
    public IReadOnlyList<DiagnosisParticipation> DiagnosisParticipation { get; init; } = [];
}
