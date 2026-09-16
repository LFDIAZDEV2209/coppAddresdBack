using CoppAddresd.Application.Features.HealthTests.Notifications;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.UnitTests.Features.HealthTests;

public sealed class HealthTestTemplateRendererTests
{
    private readonly HealthTestTemplateRenderer _renderer = new();

    [Fact]
    public void ExtractPlaceholders_devuelve_claves_ordenadas_y_unicas()
    {
        var keys = _renderer.ExtractPlaceholders("Hola [Paciente], tu [valor] y de nuevo [paciente].");

        Assert.Equal(["paciente", "valor"], keys);
    }

    [Fact]
    public void ExtractPlaceholders_ignora_llaves_antiguas()
    {
        Assert.Empty(_renderer.ExtractPlaceholders("Hola {paciente}."));
    }

    [Fact]
    public void ExtractPlaceholders_devuelve_vacio_cuando_no_hay_plantilla()
    {
        Assert.Empty(_renderer.ExtractPlaceholders(null));
        Assert.Empty(_renderer.ExtractPlaceholders("   "));
    }

    [Fact]
    public void Render_reemplaza_placeholders_conocidos()
    {
        var context = new HealthTestNotificationRenderContext(
            PatientName: "Ana Pérez",
            IndicatorName: "ORP",
            Value: "4.2",
            Severity: HealthTestSeverity.high,
            Date: new DateTime(2026, 9, 16, 0, 0, 0, DateTimeKind.Utc)
        );

        var body = _renderer.Render("[paciente]: [indicador] [valor] ([severidad]) el [fecha]", context);

        Assert.Equal("Ana Pérez: ORP 4.2 (alta) el 16/09/2026", body);
    }

    [Fact]
    public void Render_conserva_placeholders_desconocidos_y_vacia_los_nulos()
    {
        var context = new HealthTestNotificationRenderContext(PatientName: "Ana Pérez");

        var body = _renderer.Render("[paciente] | [valor] | [desconocido]", context);

        Assert.Equal("Ana Pérez |  | [desconocido]", body);
    }

    [Theory]
    [InlineData(HealthTestSeverity.low, "baja")]
    [InlineData(HealthTestSeverity.moderate, "media")]
    [InlineData(HealthTestSeverity.high, "alta")]
    [InlineData(HealthTestSeverity.critical, "crítica")]
    public void SeverityLabel_traduce_la_severidad(HealthTestSeverity severity, string expected)
    {
        Assert.Equal(expected, HealthTestTemplateRenderer.SeverityLabel(severity));
    }
}
