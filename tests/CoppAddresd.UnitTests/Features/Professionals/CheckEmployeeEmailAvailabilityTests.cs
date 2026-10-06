using CoppAddresd.Application.Features.Professionals;
using CoppAddresd.Application.Interfaces;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Professionals;

/// <summary>
/// Preflight del correo del alta de personal: el wizard consulta la
/// disponibilidad por organización para avisar en el propio campo.
/// </summary>
public class CheckEmployeeEmailAvailabilityTests
{
    private readonly IEmployeeRepository _repository = Substitute.For<IEmployeeRepository>();
    private readonly Guid _organizationId = Guid.NewGuid();

    private CheckEmployeeEmailAvailabilityQueryHandler CreateHandler() => new(_repository);

    [Fact]
    public async Task Handle_CorreoExistente_DevuelveNoDisponible()
    {
        _repository
            .EmailExistsInOrganizationAsync(
                _organizationId,
                "ana@mediquer.com",
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(true);

        var result = await CreateHandler()
            .Handle(
                new CheckEmployeeEmailAvailabilityQuery(_organizationId, "ana@mediquer.com"),
                CancellationToken.None
            );

        Assert.False(result.Available);
    }

    [Fact]
    public async Task Handle_CorreoLibre_DevuelveDisponible()
    {
        _repository
            .EmailExistsInOrganizationAsync(
                _organizationId,
                "nueva@mediquer.com",
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        var result = await CreateHandler()
            .Handle(
                new CheckEmployeeEmailAvailabilityQuery(_organizationId, "nueva@mediquer.com"),
                CancellationToken.None
            );

        Assert.True(result.Available);
    }

    [Fact]
    public async Task Handle_NormalizaElCorreoAntesDeConsultar()
    {
        _repository
            .EmailExistsInOrganizationAsync(
                _organizationId,
                "ana@mediquer.com",
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(false);

        await CreateHandler()
            .Handle(
                new CheckEmployeeEmailAvailabilityQuery(_organizationId, "  Ana@Mediquer.com "),
                CancellationToken.None
            );

        await _repository
            .Received(1)
            .EmailExistsInOrganizationAsync(
                _organizationId,
                "ana@mediquer.com",
                Arg.Any<Guid?>(),
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
            .EmailExistsInOrganizationAsync(
                Arg.Any<Guid>(),
                Arg.Any<string>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            );
    }
}
