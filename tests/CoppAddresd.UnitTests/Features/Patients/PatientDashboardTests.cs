using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Cache;
using CoppAddresd.UnitTests.Cache;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Handler del dashboard general de pacientes: delega al repositorio con el
/// alcance resuelto por el backend (clínica + propio), normaliza el estado y el
/// rango de meses, y usa cache-aside con clave distinta por alcance/filtro.
/// </summary>
public class PatientDashboardTests
{
    private readonly IPatientDashboardRepository _repository =
        Substitute.For<IPatientDashboardRepository>();

    private static PatientDashboardDto SampleDashboard() =>
        new(new PatientStatsDto(120, 90, 8, 14), [], [], [], [], [], [], [], [], 0m);

    private void ArrangeRepository() =>
        _repository
            .GetDashboardAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(SampleDashboard());

    [Fact]
    public async Task Dashboard_PasaAlcanceEstadoNormalizadoYMeses()
    {
        ArrangeRepository();
        var clinicId = Guid.NewGuid();
        var professionalId = Guid.NewGuid();
        var handler = new GetPatientsDashboardQueryHandler(_repository, new NoCacheService());

        await handler.Handle(
            new GetPatientsDashboardQuery(clinicId, professionalId, "ca", Months: 6),
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .GetDashboardAsync(
                clinicId,
                professionalId,
                "CA",
                6,
                Arg.Is<DateTime>(d => d.Kind == DateTimeKind.Utc),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Dashboard_MesesInvalidos_CaenADoce()
    {
        ArrangeRepository();
        var handler = new GetPatientsDashboardQueryHandler(_repository, new NoCacheService());

        await handler.Handle(new GetPatientsDashboardQuery(Months: 3), CancellationToken.None);

        await _repository
            .Received(1)
            .GetDashboardAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                12,
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Dashboard_EstadoVacio_PasaNull()
    {
        ArrangeRepository();
        var handler = new GetPatientsDashboardQueryHandler(_repository, new NoCacheService());

        await handler.Handle(
            new GetPatientsDashboardQuery(StateCode: "  "),
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .GetDashboardAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Is<string?>(s => s == null),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Dashboard_DentroDeTtl_SegundaLlamadaEsHit()
    {
        ArrangeRepository();
        var cache = new FakeCacheService();
        var handler = new GetPatientsDashboardQueryHandler(_repository, cache);

        var first = await handler.Handle(new GetPatientsDashboardQuery(), CancellationToken.None);
        var second = await handler.Handle(new GetPatientsDashboardQuery(), CancellationToken.None);

        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);
        Assert.Equal(first, second);
        await _repository
            .Received(1)
            .GetDashboardAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<int>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Dashboard_AlcanceOFiltroDistinto_ClaveDistinta()
    {
        ArrangeRepository();
        var cache = new FakeCacheService();
        var handler = new GetPatientsDashboardQueryHandler(_repository, cache);
        var ownId = Guid.NewGuid();

        await handler.Handle(new GetPatientsDashboardQuery(), CancellationToken.None);
        await handler.Handle(
            new GetPatientsDashboardQuery(OwnProfessionalId: ownId),
            CancellationToken.None
        );
        await handler.Handle(
            new GetPatientsDashboardQuery(StateCode: "CA"),
            CancellationToken.None
        );
        await handler.Handle(new GetPatientsDashboardQuery(Months: 6), CancellationToken.None);

        Assert.Equal(4, cache.Set.Count);
        Assert.Equal(4, cache.Set.Distinct().Count());
        Assert.All(
            cache.Set,
            key =>
            {
                Assert.StartsWith("stats:patients-dashboard:", key);
                Assert.EndsWith(":v1", key);
            }
        );
    }
}
