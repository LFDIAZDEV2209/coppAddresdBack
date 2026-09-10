using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoppAddresd.UnitTests.Features.Professionals;

public class ProfessionalScopeSyncTests
{
    private readonly IAuthScopedAssignmentsClient _scoped = Substitute.For<IAuthScopedAssignmentsClient>();
    private readonly IAuthRolesClient _roles = Substitute.For<IAuthRolesClient>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public async Task UserIdNull_ReturnOkSinLlamadas()
    {
        var result = await ProfessionalScopeSync.TryEnsureProfessionalScopesAsync(
            _scoped, _roles, userId: null, [Guid.NewGuid()], _logger, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(0, result.Granted);
        await _roles.DidNotReceive().GetRoleByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SinClinicas_ReturnOkSinLlamadas()
    {
        var result = await ProfessionalScopeSync.TryEnsureProfessionalScopesAsync(
            _scoped, _roles, userId: Guid.NewGuid(), [], _logger, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(0, result.Granted);
        await _roles.DidNotReceive().GetRoleByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RolNoEncontrado_Tolerado_ReturnOkConWarning()
    {
        _roles.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns((AuthRoleLookupResult?)null);

        var result = await ProfessionalScopeSync.TryEnsureProfessionalScopesAsync(
            _scoped, _roles, userId: Guid.NewGuid(), [Guid.NewGuid()], _logger, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(0, result.Granted);
        await _scoped.DidNotReceive().GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TodasLasClinicasPresentas_NoOp()
    {
        var userId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _roles.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        _scoped.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult(
                [new ScopedRoleAssignmentView(roleId, "Professional", "Clinic", clinicId)],
                []));

        var result = await ProfessionalScopeSync.TryEnsureProfessionalScopesAsync(
            _scoped, _roles, userId, [clinicId], _logger, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(0, result.Granted);
        await _scoped.DidNotReceive().ReplaceAsync(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<ScopedRoleAssignmentInput>>(),
            Arg.Any<IReadOnlyList<ScopedPermissionAssignmentInput>>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ClinicaFaltante_AgregaYPreservaPermisos()
    {
        var userId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var permId = Guid.NewGuid();

        _roles.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        _scoped.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult(
                [], // Sin roles existentes
                [new ScopedPermissionAssignmentView(permId, "Docs.View", "Clinic", clinicId, "Grant")]));

        var result = await ProfessionalScopeSync.TryEnsureProfessionalScopesAsync(
            _scoped, _roles, userId, [clinicId], _logger, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(1, result.Granted);
        await _scoped.Received(1).ReplaceAsync(
            userId,
            Arg.Is<IReadOnlyList<ScopedRoleAssignmentInput>>(roles =>
                roles.Count == 1 && roles[0].RoleId == roleId && roles[0].ScopeId == clinicId),
            Arg.Is<IReadOnlyList<ScopedPermissionAssignmentInput>>(perms =>
                perms.Count == 1 && perms[0].PermissionId == permId),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultiplesClinicasFaltantes_AgregaTodas()
    {
        var userId = Guid.NewGuid();
        var clinicA = Guid.NewGuid();
        var clinicB = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _roles.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        // ClinicA ya tiene el rol, ClinicB no.
        _scoped.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult(
                [new ScopedRoleAssignmentView(roleId, "Professional", "Clinic", clinicA)],
                []));

        var result = await ProfessionalScopeSync.TryEnsureProfessionalScopesAsync(
            _scoped, _roles, userId, [clinicA, clinicB], _logger, CancellationToken.None);

        Assert.True(result.Ok);
        Assert.Equal(1, result.Granted);
        await _scoped.Received(1).ReplaceAsync(
            userId,
            Arg.Is<IReadOnlyList<ScopedRoleAssignmentInput>>(roles =>
                roles.Count == 2
                && roles.Any(r => r.ScopeId == clinicA)
                && roles.Any(r => r.ScopeId == clinicB && r.RoleId == roleId)),
            Arg.Any<IReadOnlyList<ScopedPermissionAssignmentInput>>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExcepcionEnReplace_ReturnFalseNoLanza()
    {
        var userId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        _roles.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        _scoped.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult([], []));

        _scoped.ReplaceAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<ScopedRoleAssignmentInput>>(),
                Arg.Any<IReadOnlyList<ScopedPermissionAssignmentInput>>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Auth falló"));

        var result = await ProfessionalScopeSync.TryEnsureProfessionalScopesAsync(
            _scoped, _roles, userId, [clinicId], _logger, CancellationToken.None);

        Assert.False(result.Ok);
        Assert.Equal(0, result.Granted);
    }
}
