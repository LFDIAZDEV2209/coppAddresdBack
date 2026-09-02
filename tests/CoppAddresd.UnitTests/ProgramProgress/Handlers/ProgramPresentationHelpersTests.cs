using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Tests de los helpers de presentación del módulo (catálogo de tareas y
/// escalera de niveles): espejo del mock móvil, sin lógica clínica (SPEC
/// §6.15). Los ejercita el repositorio en producción; aquí se cubren de forma
/// directa para los tests de handlers.
/// </summary>
public class ProgramPresentationHelpersTests
{
    [Theory]
    [InlineData(TaskCode.podcast)]
    [InlineData(TaskCode.vitals)]
    [InlineData(TaskCode.nut)]
    [InlineData(TaskCode.ejercicio)]
    [InlineData(TaskCode.nutraceutico)]
    [InlineData(TaskCode.emocional)]
    public void ProgramTaskCatalog_LasSeisTareas_TienenTituloYSub (TaskCode code)
    {
        var (title, shortText) = ProgramTaskCatalog.For(code);

        Assert.False(string.IsNullOrWhiteSpace(title));
        Assert.False(string.IsNullOrWhiteSpace(shortText));
    }

    [Fact]
    public void XpLevels_BalanceEnEscalon_DevuelveNivelYSiguiente()
    {
        var (level, nextLevelAt) = XpLevels.ForBalance(1620);

        Assert.Equal("Constante", level);
        Assert.Equal(3000, nextLevelAt);
    }

    [Fact]
    public void XpLevels_BalanceEnTope_DevuelveNivelMaximoSinSiguiente()
    {
        var (level, nextLevelAt) = XpLevels.ForBalance(50_000);

        Assert.Equal("Maestro", level);
        Assert.Equal(99999, nextLevelAt);
    }

    [Fact]
    public void XpLevels_BalanceCero_DevuelvePrimerNivel()
    {
        var (level, nextLevelAt) = XpLevels.ForBalance(0);

        Assert.Equal("Explorador", level);
        Assert.Equal(500, nextLevelAt);
    }
}