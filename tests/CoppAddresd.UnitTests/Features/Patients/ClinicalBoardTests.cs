using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Interfaces;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Handler del tablero clínico: normaliza paginación y filtros (riesgo,
/// seguimiento, estado del paciente y ubicación; los desconocidos se
/// descartan), delega el alcance al repositorio, propaga el resumen de
/// tarjetas y calcula la paginación del contrato.
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
            "E11",
            "Diabetes mellitus tipo 2",
            "US-CA",
            "California",
            "Seguros Vida",
            ["Dra. López"],
            "Activo",
            "high",
            DateTime.UtcNow.AddDays(-3),
            "Historia clínica",
            1,
            "high",
            DateTime.UtcNow.AddDays(5),
            ClinicalBoardFollowUp.OnTrack
        );

    private static ClinicalBoardSummaryDto SampleSummary() => new(10, 3, 2, 3, 2, 4, 5, 3, 2);

    private void SetupBoard(
        IReadOnlyList<ClinicalBoardItemDto>? items = null,
        int total = 0,
        ClinicalBoardSummaryDto? summary = null
    )
    {
        _repository
            .GetClinicalBoardAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            )
            .Returns((items ?? [], total, summary ?? SampleSummary()));
    }

    [Fact]
    public async Task Board_ClampeaPaginacionYNormalizaFiltros()
    {
        var clinicId = Guid.NewGuid();
        SetupBoard(items: new[] { SampleItem() }, total: 41);

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
                null,
                null,
                null,
                clinicId,
                null,
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
        SetupBoard();

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
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
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
        SetupBoard(items: new[] { SampleItem() }, total: 45);

        var handler = new GetClinicalBoardQueryHandler(_repository);

        var result = await handler.Handle(
            new GetClinicalBoardQuery(Page: 3, PageSize: 20),
            CancellationToken.None
        );

        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task Board_AceptaRiesgoCriticoNormalizado()
    {
        SetupBoard();

        var handler = new GetClinicalBoardQueryHandler(_repository);

        await handler.Handle(new GetClinicalBoardQuery(Risk: "CRITICAL"), CancellationToken.None);

        await _repository
            .Received(1)
            .GetClinicalBoardAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Is<string?>(s => s == "critical"),
                Arg.Any<bool?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Board_NormalizaFiltrosDeDirectorio()
    {
        var insurerId = Guid.NewGuid();
        SetupBoard();

        var handler = new GetClinicalBoardQueryHandler(_repository);

        await handler.Handle(
            new GetClinicalBoardQuery(
                Status: "  Activo  ",
                InsurerId: insurerId,
                StateCode: " us-ca "
            ),
            CancellationToken.None
        );

        await _repository
            .Received(1)
            .GetClinicalBoardAsync(
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<string?>(),
                "Activo",
                insurerId,
                "US-CA",
                Arg.Any<Guid?>(),
                Arg.Any<Guid?>(),
                Arg.Any<DateTime>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Board_PropagaResumenDeTarjetas()
    {
        SetupBoard(summary: new ClinicalBoardSummaryDto(12, 2, 3, 4, 3, 5, 6, 4, 2));

        var handler = new GetClinicalBoardQueryHandler(_repository);

        var result = await handler.Handle(new GetClinicalBoardQuery(), CancellationToken.None);

        Assert.Equal(12, result.Summary.Total);
        Assert.Equal(2, result.Summary.WithoutEvaluation);
        Assert.Equal(3, result.Summary.RiskHigh);
        Assert.Equal(4, result.Summary.RiskModerate);
        Assert.Equal(3, result.Summary.RiskLow);
        Assert.Equal(5, result.Summary.WithActiveAlerts);
        Assert.Equal(6, result.Summary.FollowUpOnTrack);
        Assert.Equal(4, result.Summary.FollowUpOverdue);
        Assert.Equal(2, result.Summary.FollowUpUnassigned);
    }
}
