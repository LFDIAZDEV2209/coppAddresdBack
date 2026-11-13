using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services;
using CoppAddresd.Domain.Exceptions;
using NSubstitute;

namespace CoppAddresd.UnitTests;

public sealed class PatientScopeGuardTests
{
    private static readonly Guid Own = Guid.NewGuid(), Foreign = Guid.NewGuid();

    private sealed class Store : IPatientScopeStore
    {
        public IQueryable<Guid> VisiblePatients(Guid? professionalId, bool isAdmin)
            => (isAdmin ? new[] { Own, Foreign } : professionalId is null ? [] : new[] { Own }).AsQueryable();
        public Task<bool> ContainsAsync(IQueryable<Guid> patientIds, Guid patientId, CancellationToken ct)
            => Task.FromResult(patientIds.Contains(patientId));
        public Task<Guid?> OwnerAsync(PatientResource resource, Guid id, CancellationToken ct)
            => Task.FromResult<Guid?>(id == Own ? Own : id == Foreign ? Foreign : null);
    }

    private static PatientScopeGuard Guard(bool permission = true, bool admin = false, bool professional = true)
    {
        var actor = Substitute.For<IPatientScopeActor>();
        actor.UserId.Returns(Guid.NewGuid()); actor.IsAdmin.Returns(admin);
        actor.ProfessionalIdAsync(Arg.Any<CancellationToken>()).Returns(professional ? Guid.NewGuid() : null);
        actor.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(permission);
        return new(actor, new Store());
    }

    [Fact] public Task Assigned_WithPermission_Allows() => Guard().EnsureAsync(Own, CancellationToken.None);
    [Fact] public Task Admin_WithPermission_AllowsForeign() => Guard(admin: true, professional: false).EnsureAsync(Foreign, CancellationToken.None);
    [Fact] public async Task Assigned_WithoutPermission_Denies()
        => await Assert.ThrowsAsync<ForbiddenException>(() => Guard(permission: false).EnsureAsync(Own, CancellationToken.None));
    [Fact] public async Task Admin_WithoutPermission_Denies()
        => await Assert.ThrowsAsync<ForbiddenException>(() => Guard(permission: false, admin: true).EnsureAsync(Foreign, CancellationToken.None));
    [Fact] public async Task ForeignAndMissing_HaveIdenticalNotFound()
    {
        var guard = Guard();
        var foreign = await Assert.ThrowsAsync<NotFoundException>(() => guard.EnsureAsync(Foreign, CancellationToken.None));
        var absent = await Assert.ThrowsAsync<NotFoundException>(() => guard.EnsureAsync(Guid.NewGuid(), CancellationToken.None));
        Assert.Equal(foreign.Message, absent.Message);
    }
    [Fact] public async Task NoProfessional_DeniesEvenWithPermission()
        => await Assert.ThrowsAsync<NotFoundException>(() => Guard(professional: false).EnsureAsync(Own, CancellationToken.None));

    [Theory]
    [InlineData(PatientResource.Cycle)] [InlineData(PatientResource.Intake)] [InlineData(PatientResource.Document)]
    [InlineData(PatientResource.FoodAnalysis)] [InlineData(PatientResource.Enrollment)] [InlineData(PatientResource.Week)]
    public async Task IndirectResource_ForeignAndMissing_Denied(PatientResource resource)
    {
        var guard = Guard();
        await Assert.ThrowsAsync<NotFoundException>(() => guard.EnsureResourceAsync(resource, Foreign, "Nutrition.View", CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => guard.EnsureResourceAsync(resource, Guid.NewGuid(), "Nutrition.View", CancellationToken.None));
        Assert.Equal(Own, await guard.EnsureResourceAsync(resource, Own, "Nutrition.View", CancellationToken.None));
    }

    [Theory] [InlineData(false, 1)] [InlineData(true, 2)]
    public async Task Lists_FilterBeforeCountAndPage(bool admin, int expected)
    {
        var rows = new[] { new Row(Own), new Row(Foreign) }.AsQueryable();
        var query = await Guard(admin: admin).FilterAsync(rows, x => x.PatientId, "Nutrition.View", CancellationToken.None);
        Assert.Equal(expected, query.Count());
        Assert.Equal(expected, query.OrderBy(x => x.PatientId).Take(10).Count());
    }

    [Fact] public async Task Cancelled_DoesNotContinue()
    {
        using var cts = new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Guard().EnsureAsync(Own, cts.Token));
    }
    private sealed record Row(Guid PatientId);
}
