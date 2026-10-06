using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Professionals;

/// <summary>
/// Preflight del correo del alta de personal: el wizard consulta la
/// disponibilidad por organización y el contexto de los perfiles/cuenta que ya
/// usan el correo para mostrar la tarjeta de perfil existente.
/// </summary>
public class CheckEmployeeEmailAvailabilityTests
{
    private readonly IEmployeeRepository _repository = Substitute.For<IEmployeeRepository>();
    private readonly IPatientRepository _patients = Substitute.For<IPatientRepository>();
    private readonly IAuthUsersLookupClient _usersLookup = Substitute.For<IAuthUsersLookupClient>();
    private readonly Guid _organizationId = Guid.NewGuid();

    private CheckEmployeeEmailAvailabilityQueryHandler CreateHandler() =>
        new(_repository, _patients, _usersLookup);

    [Fact]
    public async Task Handle_CorreoExistente_DevuelveNoDisponibleConPerfil()
    {
        var employeeId = Guid.NewGuid();
        _repository
            .GetByEmailAsync(_organizationId, "ana@mediquer.com", Arg.Any<CancellationToken>())
            .Returns(
                new Employee
                {
                    Id = employeeId,
                    FirstName = "Ana",
                    LastName = "Pérez",
                    Status = "Invited",
                    UserId = null,
                }
            );

        var result = await CreateHandler()
            .Handle(
                new CheckEmployeeEmailAvailabilityQuery(_organizationId, "ana@mediquer.com"),
                CancellationToken.None
            );

        Assert.False(result.Available);
        Assert.NotNull(result.Employee);
        Assert.Equal(employeeId, result.Employee.Id);
        Assert.Equal("Ana", result.Employee.FirstName);
        Assert.False(result.Employee.HasAccount);
    }

    [Fact]
    public async Task Handle_CorreoLibre_DevuelveDisponibleSinContexto()
    {
        var result = await CreateHandler()
            .Handle(
                new CheckEmployeeEmailAvailabilityQuery(_organizationId, "nueva@mediquer.com"),
                CancellationToken.None
            );

        Assert.True(result.Available);
        Assert.Null(result.Employee);
        Assert.Null(result.Patient);
    }

    [Fact]
    public async Task Handle_NormalizaElCorreoAntesDeConsultar()
    {
        await CreateHandler()
            .Handle(
                new CheckEmployeeEmailAvailabilityQuery(_organizationId, "  Ana@Mediquer.com "),
                CancellationToken.None
            );

        await _repository
            .Received(1)
            .GetByEmailAsync(
                _organizationId,
                "ana@mediquer.com",
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Handle_EntradaIncompleta_DevuelveDisponibleSinConsultar()
    {
        var result = await CreateHandler()
            .Handle(
                new CheckEmployeeEmailAvailabilityQuery(Guid.Empty, "   "),
                CancellationToken.None
            );

        Assert.True(result.Available);
        await _repository
            .DidNotReceive()
            .GetByEmailAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _patients
            .DidNotReceive()
            .GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _usersLookup
            .DidNotReceive()
            .LookupByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CorreoDePacienteYCuenta_DevuelveContextoCompleto()
    {
        var patientId = Guid.NewGuid();
        _patients
            .GetByEmailAsync("mario@mediquer.com", Arg.Any<CancellationToken>())
            .Returns(
                new PatientProfile
                {
                    Id = patientId,
                    FirstName = "Mario",
                    LastName = "Polo",
                    Status = "Activo",
                    UserId = Guid.NewGuid(),
                }
            );
        _usersLookup
            .LookupByEmailAsync("mario@mediquer.com", Arg.Any<CancellationToken>())
            .Returns(new AccountLookupResult(true, true, true));

        var result = await CreateHandler()
            .Handle(
                new CheckEmployeeEmailAvailabilityQuery(_organizationId, "mario@mediquer.com"),
                CancellationToken.None
            );

        // El paciente no bloquea el alta de empleado (coexistencia de perfiles).
        Assert.True(result.Available);
        Assert.NotNull(result.Patient);
        Assert.Equal(patientId, result.Patient.Id);
        Assert.True(result.Patient.HasAccount);
        Assert.NotNull(result.Account);
        Assert.True(result.Account.Exists);
        Assert.True(result.Account.HasPassword);
    }

    [Fact]
    public async Task Handle_AuthNoResponde_OmiteLaCuentaSinFallar()
    {
        _usersLookup
            .LookupByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((AccountLookupResult?)null);

        var result = await CreateHandler()
            .Handle(
                new CheckEmployeeEmailAvailabilityQuery(_organizationId, "nueva@mediquer.com"),
                CancellationToken.None
            );

        Assert.True(result.Available);
        Assert.Null(result.Account);
    }
}
