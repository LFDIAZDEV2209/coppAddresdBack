using CoppAddresd.WhatsApp.Messaging;

namespace CoppAddresd.WhatsApp.Tests;

/// <summary>
/// Pruebas del normalizador de formato para WhatsApp: negrita con asteriscos
/// simples, sin separadores Markdown que el canal muestra literales y
/// encabezados convertidos a negrita.
/// </summary>
public sealed class MessagePolicyTests
{
    [Theory]
    [InlineData("**Nutrición**", "*Nutrición*")]
    [InlineData("Lo que **no** puedo", "Lo que *no* puedo")]
    [InlineData("Texto sin markdown", "Texto sin markdown")]
    [InlineData("", "")]
    public void ConvierteNegritaDobleASimple(string input, string expected) =>
        Assert.Equal(expected, MessagePolicy.NormalizeForWhatsApp(input));

    [Theory]
    [InlineData("---\r\nSiguiente sección", "\nSiguiente sección")]
    [InlineData("***\r\nTexto", "\nTexto")]
    [InlineData("___\r\nTexto", "\nTexto")]
    public void EliminaSeparadoresHorizontales(string input, string expected) =>
        Assert.Equal(expected, MessagePolicy.NormalizeForWhatsApp(input));

    [Theory]
    [InlineData("### Nutrición", "*Nutrición*")]
    [InlineData("## Tu plan de salud", "*Tu plan de salud*")]
    public void ConvierteEncabezadosANegrita(string input, string expected) =>
        Assert.Equal(expected, MessagePolicy.NormalizeForWhatsApp(input));

    [Fact]
    public void NoAlternaAsteriscosSimplesYaValidos() =>
        Assert.Equal(
            "*Nutrición* con detalle",
            MessagePolicy.NormalizeForWhatsApp("*Nutrición* con detalle")
        );

    [Fact]
    public void PreservaSaltosDeLineaYBloques() =>
        Assert.Equal(
            "Saludo\n\n*Item uno*\nDetalle\n\n*Item dos*",
            MessagePolicy.NormalizeForWhatsApp(
                "Saludo\r\n\r\n**Item uno**\r\nDetalle\r\n\r\n**Item dos**"
            )
        );
}
