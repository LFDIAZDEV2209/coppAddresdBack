using CoppAddresd.Auth.Data;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.UnitTests;

public sealed class AuthMigrationHistoryRelocatorTests
{
    [Fact]
    public void SelectMatchingIds_KeepsOnlyAuthMigrations_InSharedOrder()
    {
        var auth = new[]
        {
            "20260810221731_InitialCreate",
            "20260810221740_AddRefreshTokens",
            "20260813200500_AddApplicationsAndUserApplications",
            "20260819201840_AddOtpCodes",
            "20260819214445_AddScopedAssignments",
            "20260819221901_AddInvitations"
        };

        var shared = new[]
        {
            "20260810195245_InitialAuditSchema",
            "20260810221731_InitialCreate",
            "20260810221740_AddRefreshTokens",
            "20260813222001_AddPatientsModule",
            "20260819214445_AddScopedAssignments",
            "20260819221901_AddInvitations"
        };

        var matching = AuthMigrationHistoryRelocator.SelectMatchingIds(auth, shared);

        Assert.Equal(
            [
                "20260810221731_InitialCreate",
                "20260810221740_AddRefreshTokens",
                "20260819214445_AddScopedAssignments",
                "20260819221901_AddInvitations"
            ],
            matching);
        Assert.DoesNotContain("20260819201840_AddOtpCodes", matching);
        Assert.DoesNotContain("20260813222001_AddPatientsModule", matching);
    }

    [Fact]
    public void SelectMatchingIds_EmptySharedHistory_ReturnsEmpty()
    {
        var matching = AuthMigrationHistoryRelocator.SelectMatchingIds(
            ["20260810221731_InitialCreate"],
            []);

        Assert.Empty(matching);
    }

    [Fact]
    public void GetMigrations_IncludesAuthAssemblyMigrations()
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql("Host=localhost;Database=unused")
            .Options;

        using var context = new AuthDbContext(options);
        var ids = context.Database.GetMigrations().ToArray();

        Assert.Contains("20260810221731_InitialCreate", ids);
        Assert.Contains("20260819201840_AddOtpCodes", ids);
        Assert.Contains("20260819221901_AddInvitations", ids);
        Assert.All(
            AuthMigrationHistoryRelocator.TableSentinels,
            sentinel => Assert.Contains(sentinel.MigrationId, ids));
    }
}
