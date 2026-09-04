namespace CoppAddresd.Community.Entities;

/// <summary>
/// Cálculos puros y deterministas para gamificación de la comunidad: niveles por XP,
/// nivel de riesgo por inactividad y rachas (streak) a partir de fechas de publicación.
/// Centralizado para poder testear la lógica sin base de datos.
/// </summary>
public static class CommunityStats
{
    /// <summary>Umbrales de XP por nivel (acumulado).</summary>
    public const int ExploradorThreshold = 1000;
    public const int IniciadoThreshold = 2500;
    public const int ConstanteThreshold = 5000;
    public const int DisciplinadoThreshold = 8500;

    /// <summary>Devuelve el nombre del nivel a partir del XP total acumulado.</summary>
    public static string LevelName(int xpTotal)
    {
        if (xpTotal >= DisciplinadoThreshold) return "Bienestar";
        if (xpTotal >= ConstanteThreshold) return "Disciplinado";
        if (xpTotal >= IniciadoThreshold) return "Constante";
        if (xpTotal >= ExploradorThreshold) return "Iniciado";
        return "Explorador";
    }

    /// <summary>
    /// Nivel de riesgo de inactividad: &gt;14 días sin actividad ⇒ Alto; 7–14 días ⇒ Medio;
    /// menos de 7 días (o perfil nuevo sin actividad) ⇒ Bajo.
    /// </summary>
    /// <param name="now">Instante de referencia (para determinismo en tests). Si es null, usa <c>DateTimeOffset.UtcNow</c>.</param>
    public static RiskLevel ComputeRiskLevel(DateTimeOffset? lastPostAt, DateTimeOffset? lastActiveAt, DateTimeOffset? now = null)
    {
        var last = MaxOrNull(lastPostAt, lastActiveAt);
        if (last is null)
            return RiskLevel.Bajo;

        var reference = now ?? DateTimeOffset.UtcNow;
        var days = (reference - last.Value).TotalDays;
        if (days > 14)
            return RiskLevel.Alto;
        if (days >= 7)
            return RiskLevel.Medio;
        return RiskLevel.Bajo;
    }

    /// <summary>
    /// Racha actual: días consecutivos con al menos una publicación, terminando hoy u
    /// ayer (tolerancia de un día). Sin publicaciones o brecha mayor ⇒ 0.
    /// </summary>
    public static int CurrentStreak(IEnumerable<DateTime> postDates)
    {
        var days = postDates
            .Select(d => d.ToUniversalTime().Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();
        if (days.Count == 0)
            return 0;

        var today = DateTime.UtcNow.Date;
        // La racha debe terminar hoy o ayer; si no, el usuario está "fueraa de racha".
        if (days[0] != today && days[0] != today.AddDays(-1))
            return 0;

        var streak = 0;
        var expected = days[0];
        foreach (var day in days)
        {
            if (day == expected)
            {
                streak++;
                expected = expected.AddDays(-1);
            }
            else if (day < expected)
            {
                break;
            }
        }
        return streak;
    }

    /// <summary>Mayor racha histórica de días consecutivos con publicación.</summary>
    public static int BestStreak(IEnumerable<DateTime> postDates)
    {
        var days = postDates
            .Select(d => d.ToUniversalTime().Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
        if (days.Count == 0)
            return 0;

        var best = 1;
        var current = 1;
        for (var i = 1; i < days.Count; i++)
        {
            if (days[i] == days[i - 1].AddDays(1))
            {
                current++;
                best = Math.Max(best, current);
            }
            else
            {
                current = 1;
            }
        }
        return best;
    }

    private static DateTimeOffset? MaxOrNull(DateTimeOffset? a, DateTimeOffset? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a.Value > b.Value ? a : b;
    }
}
