using CoppAddresd.Application.Features.ProgramProgress.DTOs.Erp;
using CoppAddresd.Application.Features.ProgramProgress.Queries.Erp;
using CoppAddresd.Application.Interfaces;
using NSubstitute;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

public sealed class BiometriaExportTests
{
    [Fact]
    public async Task Export_IncluyeTodasLasPaginasYConservaFiltros()
    {
        var repository = Substitute.For<IProgramRepository>();
        var city = Guid.NewGuid();
        var query = new StreamBiometriaPatientsExportQuery(
            "Luis", "M", "sobrepeso", "normal", "alto", "Mejorando", city, "AL");
        using var cancellation = new CancellationTokenSource();
        var ct = cancellation.Token;
        var rows = Enumerable.Range(0, 205).Select(i => new BiometriaPatientListItemDto(
            Guid.NewGuid(), $"Luis {i:D3}", "M", null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, 0, "Mejorando")).ToList();
        var pages = new List<int>();
        repository.ListBiometriaPatientsAsync("Luis", "M", "sobrepeso", "normal", "alto",
            "Mejorando", city, "AL", Arg.Any<int>(), 100, ct).Returns(call =>
            {
                var page = call.ArgAt<int>(8);
                pages.Add(page);
                return ((IReadOnlyList<BiometriaPatientListItemDto>)rows.Skip((page - 1) * 100)
                    .Take(100).ToList(), rows.Count);
            });
        var handler = new StreamBiometriaPatientsExportQueryHandler(repository);
        var actual = new List<BiometriaPatientListItemDto>();
        await foreach (var row in handler.Handle(query, ct)) actual.Add(row);

        Assert.Equal(rows, actual);
        Assert.Equal(new[] { 1, 2, 3 }, pages);
    }

    [Fact]
    public async Task Export_Cancelado_NoConsultaRepositorio()
    {
        var repository = Substitute.For<IProgramRepository>();
        var handler = new StreamBiometriaPatientsExportQueryHandler(repository);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var row in handler.Handle(new(), cancellation.Token)) { }
        });
        Assert.Empty(repository.ReceivedCalls());
    }
}
