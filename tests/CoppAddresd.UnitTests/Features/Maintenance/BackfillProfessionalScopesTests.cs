using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Maintenance;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace CoppAddresd.UnitTests.Features.Maintenance;

public class BackfillProfessionalScopesTests
{
    private readonly IEmployeeRepository _employeeRepository = Substitute.For<IEmployeeRepository>();
    private readonly IAuthScopedAssignmentsClient _scoped = Substitute.For<IAuthScopedAssignmentsClient>();
    private readonly IAuthRolesClient _roles = Substitute.For<IAuthRolesClient>();
    private readonly ILogger<BackfillProfessionalScopesCommandHandler> _logger =
        Substitute.For<ILogger<BackfillProfessionalScopesCommandHandler>>();

    private BackfillProfessionalScopesCommandHandler CreateHandler()
        => new(_employeeRepository, _scoped, _roles, _logger);

    [Fact]
    public async Task Handle_RolNoEncontrado_ReturnAllSkipped()
    {
        _roles.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns((AuthRoleLookupResult?)null);

        var handler = CreateHandler();

        var result = await handler.Handle(
            new BackfillProfessionalScopesCommand(null), CancellationToken.None);

        Assert.Equal(0, Assert.Single([result.Processed]));
        Assert.Contains("no fue encontrado", result.Message);
        await _employeeRepository.DidNotReceive()
            .GetProfessionalClinicMembershipsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_LoteMixto_CuentaCorrectamente()
    {
        var roleId = Guid.NewGuid();
        _roles.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var clinicA = Guid.NewGuid();
        var clinicB = Guid.NewGuid();

        _employeeRepository.GetProfessionalClinicMembershipsAsync(Arg.Any<CancellationToken>())
        .Returns([
            new ProfessionalClinicMembership(Guid.NewGuid(), Guid.NewGuid(), userId1, [clinicA]),
            new ProfessionalClinicMembership(Guid.NewGuid(), Guid.NewGuid(), userId2, [clinicB]),
            new ProfessionalClinicMembership(Guid.NewGuid(), Guid.NewGuid(), null, [clinicA]), // Sin usuario
        ]);

        // userId1: scopes faltantes → se agregan 1.
        _scoped.GetAsync(userId1, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult([], []));

        // userId2: ya tiene los scopes → no-op.
        _scoped.GetAsync(userId2, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult(
                [new ScopedRoleAssignmentView(roleId, "Professional", "Clinic", clinicB)],
                []));

        var handler = CreateHandler();

        var result = await handler.Handle(
            new BackfillProfessionalScopesCommand(null), CancellationToken.None);

        Assert.Equal(3, result.Processed);
        Assert.Equal(1, result.Granted);
        Assert.Equal(1, result.SkippedNoUser);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task Handle_Idempotente_SegundaEjecucionCeroGrants()
    {
        var roleId = Guid.NewGuid();
        _roles.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        var userId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();

        _employeeRepository.GetProfessionalClinicMembershipsAsync(Arg.Any<CancellationToken>())
            .Returns([new ProfessionalClinicMembership(Guid.NewGuid(), Guid.NewGuid(), userId, [clinicId])]);

        // Ya tiene los scopes.
        _scoped.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult(
                [new ScopedRoleAssignmentView(roleId, "Professional", "Clinic", clinicId)],
                []));

        var handler = CreateHandler();

        // Primera ejecución.
        var r1 = await handler.Handle(
            new BackfillProfessionalScopesCommand(null), CancellationToken.None);
        Assert.Equal(0, r1.Granted);

        // Segunda ejecución (idempotente).
        var r2 = await handler.Handle(
            new BackfillProfessionalScopesCommand(null), CancellationToken.None);
        Assert.Equal(0, r2.Granted);
    }

    [Fact]
    public async Task Handle_ExcepcionEnSync_CuentaComoFailed()
    {
        var roleId = Guid.NewGuid();
        _roles.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        var userId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();

        _employeeRepository.GetProfessionalClinicMembershipsAsync(Arg.Any<CancellationToken>())
            .Returns([new ProfessionalClinicMembership(Guid.NewGuid(), Guid.NewGuid(), userId, [clinicId])]);

        _scoped.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult([], []));

        _scoped.ReplaceAsync(
                Arg.Any<Guid>(),
                Arg.Any<IReadOnlyList<ScopedRoleAssignmentInput>>(),
                Arg.Any<IReadOnlyList<ScopedPermissionAssignmentInput>>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Auth falló"));

        var handler = CreateHandler();

        var result = await handler.Handle(
            new BackfillProfessionalScopesCommand(null), CancellationToken.None);

        Assert.Equal(1, result.Processed);
        Assert.Equal(0, result.Granted);
        Assert.Equal(1, result.Failed);
    }
}
