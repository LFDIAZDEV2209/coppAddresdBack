using CoppAddresd.Community.Entities;

namespace CoppAddresd.Community.UnitTests;

/// <summary>
/// Tests de la lógica pura de gamificación (CommunityStats): niveles por XP, riesgo de
/// inactividad y rachas. No requieren base de datos.
/// </summary>
public sealed class CommunityStatsTests
{
    [Theory]
    [InlineData(0, "Explorador")]
    [InlineData(999, "Explorador")]
    [InlineData(1000, "Iniciado")]
    [InlineData(2499, "Iniciado")]
    [InlineData(2500, "Constante")]
    [InlineData(4999, "Constante")]
    [InlineData(5000, "Disciplinado")]
    [InlineData(8499, "Disciplinado")]
    [InlineData(8500, "Bienestar")]
    [InlineData(99999, "Bienestar")]
    public void LevelName_MapsThresholds(int xp, string expected)
        => Assert.Equal(expected, CommunityStats.LevelName(xp));

    [Fact]
    public void RiskLevel_NullActivity_IsBajo()
        => Assert.Equal(RiskLevel.Bajo, CommunityStats.ComputeRiskLevel(null, null));

    [Fact]
    public void RiskLevel_RecentActivity_IsBajo()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(RiskLevel.Bajo, CommunityStats.ComputeRiskLevel(now.AddDays(-3), now.AddDays(-1)));
    }

    [Fact]
    public void RiskLevel_WeeklyActivity_IsMedio()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(RiskLevel.Medio, CommunityStats.ComputeRiskLevel(now.AddDays(-10), now.AddDays(-9)));
    }

    [Fact]
    public void RiskLevel_LongInactivity_IsAlto()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Equal(RiskLevel.Alto, CommunityStats.ComputeRiskLevel(now.AddDays(-20), now.AddDays(-15)));
    }

    [Fact]
    public void CurrentStreak_ConsecutiveEndingToday_Counts()
    {
        var today = DateTime.UtcNow.Date;
        var dates = new[]
        {
            today,
            today.AddDays(-1),
            today.AddDays(-2),
        };
        Assert.Equal(3, CommunityStats.CurrentStreak(dates));
    }

    [Fact]
    public void CurrentStreak_GapResetsToZero()
    {
        var today = DateTime.UtcNow.Date;
        var dates = new[]
        {
            today,
            today.AddDays(-2), // brecha de un día
        };
        Assert.Equal(1, CommunityStats.CurrentStreak(dates));
    }

    [Fact]
    public void CurrentStreak_NoPostTodayButYesterday_Tolerates()
    {
        var today = DateTime.UtcNow.Date;
        var dates = new[]
        {
            today.AddDays(-1),
            today.AddDays(-2),
        };
        Assert.Equal(2, CommunityStats.CurrentStreak(dates));
    }

    [Fact]
    public void CurrentStreak_StaleActivity_IsZero()
    {
        var today = DateTime.UtcNow.Date;
        var dates = new[] { today.AddDays(-5) };
        Assert.Equal(0, CommunityStats.CurrentStreak(dates));
    }

    [Fact]
    public void BestStreak_TracksMaximumRun()
    {
        var baseDay = new DateTime(2026, 1, 1);
        var dates = new[]
        {
            baseDay,
            baseDay.AddDays(1),
            baseDay.AddDays(2),
            baseDay.AddDays(10),
            baseDay.AddDays(11),
        };
        Assert.Equal(3, CommunityStats.BestStreak(dates));
    }
}
