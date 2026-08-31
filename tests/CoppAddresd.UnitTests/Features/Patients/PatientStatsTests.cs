using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Cache;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Pruebas del query de estadísticas del directorio: el handler delega al
/// repositorio con la misma frontera de datos que el listado (clínica activa
/// + alcance propio) y calcula el inicio del mes actual en UTC. Con
/// NoCacheService (Provider=None) el factory corre en cada llamada.
/// </summary>
public class PatientStatsTests
{
    private readonly IPatientRepository _repository = Substitute.For<IPatientRepository>();

    [Fact]
    public async Task Stats_ConClinicaYAlcancePropio_PasaAmbosAlRepositorio()
    {
        var clinicId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();

        var handler = new GetPatientsStatsQueryHandler(_repository, new NoCacheService());

        await handler.Handle(
            new GetPatientsStatsQuery(ClinicId: clinicId, OwnProfessionalId: professionalId),
            CancellationToken.None
        );

        var now = DateTime.UtcNow;
        await _repository
            .Received(1)
            .GetStatsAsync(
                clinicId,
                professionalId,
                Arg.Is<DateTime>(d =>
                    d.Kind == DateTimeKind.Utc
                    && d.Day == 1
                    && d.Year == now.Year
                    && d.Month == now.Month
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Stats_SinAlcancePropio_PasaNull()
    {
        var clinicId = Guid.NewGuid();

        var handler = new GetPatientsStatsQueryHandler(_repository, new NoCacheService());

        await handler.Handle(new GetPatientsStatsQuery(ClinicId: clinicId), CancellationToken.None);

        await _repository
            .Received(1)
            .GetStatsAsync(
                clinicId,
                Arg.Is<Guid?>(v => v == null),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Stats_DevuelveLosValoresDelRepositorio()
    {
        var expected = new PatientStatsDto(
            Total: 120,
            Active: 90,
            NewThisMonth: 8,
            WithoutProfessional: 14
        );

        _repository
            .GetStatsAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(expected);

        var handler = new GetPatientsStatsQueryHandler(_repository, new NoCacheService());

        var result = await handler.Handle(new GetPatientsStatsQuery(), CancellationToken.None);

        Assert.Equal(expected, result);
    }
}
