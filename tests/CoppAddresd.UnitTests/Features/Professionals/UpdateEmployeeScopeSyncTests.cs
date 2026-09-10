using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Professionals;

public class UpdateEmployeeScopeSyncTests
{
    private readonly IEmployeeRepository _employeeRepository = Substitute.For<IEmployeeRepository>();
    private readonly IOrganizationRepository _organizationRepository = Substitute.For<IOrganizationRepository>();
    private readonly IAuthScopedAssignmentsClient _scopedAssignmentsClient = Substitute.For<IAuthScopedAssignmentsClient>();
    private readonly IAuthRolesClient _authRolesClient = Substitute.For<IAuthRolesClient>();
    private readonly ILogger<UpdateEmployeeCommandHandler> _logger =
        Substitute.For<ILogger<UpdateEmployeeCommandHandler>>();

    private UpdateEmployeeCommandHandler CreateHandler()
        => new(_employeeRepository, _organizationRepository, _scopedAssignmentsClient, _authRolesClient, _logger);

    /// <summary>Crea un empleado base con Professional y UserId para los tests.</summary>
    private static Employee BuildExistingEmployee(
        Guid employeeId,
        Guid? userId = null,
        Guid? professionalId = null,
        params Guid[] clinicIds)
    {
        return new Employee
        {
            Id = employeeId,
            OrganizationId = Guid.NewGuid(),
            UserId = userId,
            Professional = professionalId is null
                ? null
                : new Professional
                {
                    Id = professionalId.Value,
                    EmployeeId = employeeId,
                    CreatedAt = DateTime.UtcNow,
                },
            ClinicAssignments = clinicIds.Select(cid => new EmployeeClinic
            {
                EmployeeId = employeeId,
                ClinicId = cid,
                IsPrimary = false,
                Status = "Active",
                Clinic = new Clinic { Id = cid, Name = $"Clinic-{cid.ToString()[..8]}" },
            }).ToList(),
            Organization = new Organization { Id = Guid.NewGuid(), Name = "TestOrg" },
        };
    }

    [Fact]
    public async Task Handle_NuevaClinicaAgregada_InvocaScopeSync()
    {
        var employeeId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var existingClinicId = Guid.NewGuid();
        var newClinicId = Guid.NewGuid();

        var existing = BuildExistingEmployee(employeeId, userId, professionalId, existingClinicId);
        var updated = BuildExistingEmployee(employeeId, userId, professionalId, existingClinicId, newClinicId);

        // NSubstitute: returns en orden → primera llamada = existing, segunda = updated.
        _employeeRepository.GetByIdAsync(employeeId, Arg.Any<CancellationToken>())
            .Returns(existing, updated);

        var roleId = Guid.NewGuid();
        _authRolesClient.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        _scopedAssignmentsClient.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult([], []));

        var handler = CreateHandler();

        await handler.Handle(new UpdateEmployeeCommand(
            employeeId,
            null, null, null, null, null, null, null, null, null, null, null, null,
            [new ClinicAssignmentInput(existingClinicId, false, "Active"),
             new ClinicAssignmentInput(newClinicId, true, "Active")],
            null, null), CancellationToken.None);

        // Se invocó TryEnsureProfessionalScopesAsync (via GetAsync + ReplaceAsync).
        await _scopedAssignmentsClient.Received(1).GetAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ClinicasSinCambios_NoInvocaScopeSync()
    {
        var employeeId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();

        var existing = BuildExistingEmployee(employeeId, userId, professionalId, clinicId);
        var sameState = BuildExistingEmployee(employeeId, userId, professionalId, clinicId);

        _employeeRepository.GetByIdAsync(employeeId, Arg.Any<CancellationToken>())
            .Returns(existing, sameState);

        var handler = CreateHandler();

        await handler.Handle(new UpdateEmployeeCommand(
            employeeId,
            "NombreActualizado", null, null, null, null, null, null, null, null, null, null, null,
            null, // Clinics null → no sync total
            null, null), CancellationToken.None);

        // Sin cambio de clínicas, no se invoca el scope sync.
        await _authRolesClient.DidNotReceive().GetRoleByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExtensionRecienCreada_SincronizaTodasLasClinicas()
    {
        var employeeId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        var professionalTypeId = Guid.NewGuid();

        // Empleado sin Professional (HR, recién se crea extensión).
        var existing = BuildExistingEmployee(employeeId, userId, professionalId: null, clinicId);

        _employeeRepository.GetByIdAsync(employeeId, Arg.Any<CancellationToken>())
            .Returns(existing);

        _organizationRepository.GetProfessionalTypeByIdAsync(professionalTypeId, Arg.Any<CancellationToken>())
            .Returns(new ProfessionalType { Id = professionalTypeId, Code = "PHYSICIAN" });

        // Después del update, tiene Professional y misma clínica.
        var updated = BuildExistingEmployee(employeeId, userId, Guid.NewGuid(), clinicId);
        // Segunda lectura devuelve updated (con Clinic nav).
        _employeeRepository.GetByIdAsync(employeeId, Arg.Any<CancellationToken>())
            .Returns(existing, updated);

        var roleId = Guid.NewGuid();
        _authRolesClient.GetRoleByNameAsync("Professional", Arg.Any<CancellationToken>())
            .Returns(new AuthRoleLookupResult(roleId, "Professional", true));

        _scopedAssignmentsClient.GetAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new ScopedAssignmentsResult([], []));

        var handler = CreateHandler();

        await handler.Handle(new UpdateEmployeeCommand(
            employeeId,
            null, null, null, null, null, null, null, null, null, null,
            professionalTypeId, null,
            [new ClinicAssignmentInput(clinicId, true, "Active")],
            null, null), CancellationToken.None);

        // HR → profesional: se sincronizan TODAS las clínicas activas.
        await _scopedAssignmentsClient.Received(1).GetAsync(userId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SinUserId_NoInvocaScopeSync()
    {
        var employeeId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var newClinicId = Guid.NewGuid();

        // Empleado sin UserId.
        var existing = BuildExistingEmployee(employeeId, userId: null, professionalId, Guid.NewGuid());
        var sameState = BuildExistingEmployee(employeeId, userId: null, professionalId, Guid.NewGuid());

        _employeeRepository.GetByIdAsync(employeeId, Arg.Any<CancellationToken>())
            .Returns(existing, sameState);

        var handler = CreateHandler();

        await handler.Handle(new UpdateEmployeeCommand(
            employeeId,
            null, null, null, null, null, null, null, null, null, null, null, null,
            [new ClinicAssignmentInput(Guid.NewGuid(), false, "Active"),
             new ClinicAssignmentInput(newClinicId, true, "Active")],
            null, null), CancellationToken.None);

        // Sin UserId → no se invoca scope sync.
        await _authRolesClient.DidNotReceive().GetRoleByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_HRSinProfessional_NoInvocaScopeSync()
    {
        var employeeId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();

        // Empleado HR sin Professional y sin UserId.
        var existing = BuildExistingEmployee(employeeId, userId: null, professionalId: null, clinicId);
        var sameState = BuildExistingEmployee(employeeId, userId: null, professionalId: null, clinicId);

        _employeeRepository.GetByIdAsync(employeeId, Arg.Any<CancellationToken>())
            .Returns(existing, sameState);

        var handler = CreateHandler();

        await handler.Handle(new UpdateEmployeeCommand(
            employeeId,
            null, null, null, null, null, null, null, null, null, null, null, null,
            [new ClinicAssignmentInput(clinicId, true, "Active")],
            null, null), CancellationToken.None);

        // HR sin Professional → no se invoca scope sync.
        await _authRolesClient.DidNotReceive().GetRoleByNameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
