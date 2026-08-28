using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.UnitTests.Cache;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Comportamiento de caché del endpoint /patients/stats (SPEC infra/cache):
/// segunda llamada dentro del TTL es hit (no re-agrega contra PostgreSQL) y
/// alcances distintos (admin global vs profesional propio) nunca comparten
/// clave — el scoping lo resuelve el backend.
/// </summary>
public class PatientStatsCacheTests
{
    private readonly IPatientRepository _repository = Substitute.For<IPatientRepository>();

    private static PatientStatsDto SampleStats(int total = 120) =>
        new(Total: total, Active: 90, NewThisMonth: 8, WithoutProfessional: 14);

    [Fact]
    public async Task Stats_DentroDeTtl_SegundaLlamadaEsHit()
    {
        var cache = new FakeCacheService();
        _repository
            .GetStatsAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(SampleStats());
        var handler = new GetPatientsStatsQueryHandler(_repository, cache);

        var first = await handler.Handle(new GetPatientsStatsQuery(), CancellationToken.None);
        var second = await handler.Handle(new GetPatientsStatsQuery(), CancellationToken.None);

        Assert.Equal(1, cache.Misses);
        Assert.Equal(1, cache.Hits);
        Assert.Equal(first, second);
        await _repository
            .Received(1)
            .GetStatsAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Stats_DistintoAlcance_ClaveDistinta()
    {
        var cache = new FakeCacheService();
        _repository
            .GetStatsAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(SampleStats());
        var handler = new GetPatientsStatsQueryHandler(_repository, cache);
        var ownId = Guid.NewGuid();

        await handler.Handle(new GetPatientsStatsQuery(), CancellationToken.None);
        await handler.Handle(
            new GetPatientsStatsQuery(OwnProfessionalId: ownId),
            CancellationToken.None
        );

        Assert.Equal(2, cache.Set.Count);
        Assert.Equal(2, cache.Set.Distinct().Count());
        await _repository
            .Received(2)
            .GetStatsAsync(
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Stats_LaClaveLlevaVersion_NamespaceYHash()
    {
        var cache = new FakeCacheService();
        var handler = new GetPatientsStatsQueryHandler(_repository, cache);

        await handler.Handle(new GetPatientsStatsQuery(), CancellationToken.None);

        var key = Assert.Single(cache.Set);
        Assert.StartsWith("stats:patients:", key);
        Assert.EndsWith(":v1", key);
        Assert.NotEqual("stats:patients:v1", key); // el hash de alcance está presente
    }
}
