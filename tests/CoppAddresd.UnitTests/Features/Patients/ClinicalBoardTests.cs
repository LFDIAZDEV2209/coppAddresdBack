using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Handler del tablero clínico: normaliza paginación y filtros (riesgo y
/// seguimiento desconocidos se descartan), delega el alcance al repositorio y
/// calcula la paginación del contrato.
/// </summary>
public class ClinicalBoardTests
{
    private readonly IPatientDashboardRepository _repository =
        Substitute.For<IPatientDashboardRepository>();

    private static ClinicalBoardItemDto SampleItem() =>
        new(
            Guid.NewGuid(),
            "MRN-1",
            "Ana",
            "Martínez",
            "1000000003",
            "high",
            DateTime.UtcNow.AddDays(-3),
            "Historia clínica",
            1,
            "high",
            DateTime.UtcNow.AddDays(5),
            ClinicalBoardFollowUp.OnTrack
        );

    [Fact]
    public async Task Board_ClampeaPaginacionYNormalizaFiltros()
    {
        var clinicId = Guid.NewGuid();
        _repository
            .GetClinicalBoardAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new[] { SampleItem() }, 41));

        var handler = new GetClinicalBoardQueryHandler(_repository);

        var result = await handler.Handle(
            new GetClinicalBoardQuery(
                Page: 2,
                PageSize: 500,
                Search: "  ana  ",
                Risk: "HIGH",
                HasAlerts: true,
                FollowUp: "VENCIDO",
                ClinicId: clinicId
            ),
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .GetClinicalBoardAsync(
                2,
                100,
                "ana",
                "high",
                true,
                "vencido",
                clinicId,
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );

        Assert.Equal(41, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(100, result.PageSize);
        Assert.Equal(1, result.TotalPages);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task Board_FiltrosDesconocidos_SeDescartan()
    {
        _repository
            .GetClinicalBoardAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((Array.Empty<ClinicalBoardItemDto>(), 0));

        var handler = new GetClinicalBoardQueryHandler(_repository);

        var result = await handler.Handle(
            new GetClinicalBoardQuery(Risk: "inventado", FollowUp: "otro"),
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .GetClinicalBoardAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Is<string?>(s => s == null),
                Arg.Any<bool?>(),
                Arg.Is<string?>(s => s == null),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );

        Assert.Equal(0, result.Total);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task Board_CalculaTotalPages()
    {
        _repository
            .GetClinicalBoardAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((new[] { SampleItem() }, 45));

        var handler = new GetClinicalBoardQueryHandler(_repository);

        var result = await handler.Handle(
            new GetClinicalBoardQuery(Page: 3, PageSize: 20),
            CancellationToken.None
        );

        Assert.Equal(3, result.TotalPages);
    }
}
