using CoppAddresd.Application.Common;
using Microsoft.Extensions.Options;
using CoppAddresd.Application.Features.FoodAi;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del validador de imágenes de comida: reglas puras (extensión, MIME,
/// tamaño, vacío, firma mágica) sin dependencias de infraestructura.
/// </summary>
public class ImageFileValidatorTests
{
    private const string Png1x1Base64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private static readonly byte[] Png1x1 = Convert.FromBase64String(Png1x1Base64);

    private static ImageFileValidator BuildValidator(long maxSizeBytes = 10 * 1024 * 1024)
        => new(Options.Create(new FoodAiSettings { MaxImageSizeBytes = maxSizeBytes }));

    private static Stream StreamFrom(byte[] bytes) => new MemoryStream(bytes);

    [Fact]
    public void Validate_png_valido_pasa()
    {
        var validator = BuildValidator();

        var result = validator.Validate(
            StreamFrom(Png1x1), "comida.png", "image/png", Png1x1.Length);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_jpeg_valido_pasa()
    {
        var jpeg = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        var validator = BuildValidator();

        var result = validator.Validate(StreamFrom(jpeg), "plato.jpg", "image/jpeg", jpeg.Length);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_webp_valido_pasa()
    {
        var webp = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        var validator = BuildValidator();

        var result = validator.Validate(StreamFrom(webp), "arepa.webp", "image/webp", webp.Length);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_archivo_vacio_rechaza_con_EMPTY_FILE()
    {
        var validator = BuildValidator();

        var result = validator.Validate(StreamFrom([]), "vacio.png", "image/png", 0);

        Assert.False(result.IsValid);
        Assert.Equal(ImageFileValidator.CodeEmptyFile, result.ErrorCode);
    }

    [Fact]
    public void Validate_tamano_superior_al_maximo_rechaza_con_IMAGE_TOO_LARGE()
    {
        var validator = BuildValidator(maxSizeBytes: 100);

        var result = validator.Validate(StreamFrom(Png1x1), "grande.png", "image/png", 10_000);

        Assert.False(result.IsValid);
        Assert.Equal(ImageFileValidator.CodeImageTooLarge, result.ErrorCode);
    }

    [Fact]
    public void Validate_extension_no_permitida_rechaza()
    {
        var validator = BuildValidator();

        var result = validator.Validate(StreamFrom(Png1x1), "comida.exe", "image/png", Png1x1.Length);

        Assert.False(result.IsValid);
        Assert.Equal(ImageFileValidator.CodeInvalidImage, result.ErrorCode);
    }

    [Fact]
    public void Validate_mime_no_permitido_rechaza()
    {
        var validator = BuildValidator();

        var result = validator.Validate(
            StreamFrom(Png1x1), "comida.png", "application/pdf", Png1x1.Length);

        Assert.False(result.IsValid);
        Assert.Equal(ImageFileValidator.CodeInvalidImage, result.ErrorCode);
    }

    [Fact]
    public void Validate_mime_con_parametros_se_normaliza()
    {
        var validator = BuildValidator();

        var result = validator.Validate(
            StreamFrom(Png1x1), "comida.png", "image/png; charset=binary", Png1x1.Length);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_firma_magica_inconsistente_rechaza_como_corrupto()
    {
        var validator = BuildValidator();
        var texto = System.Text.Encoding.UTF8.GetBytes("esto no es una imagen real");

        var result = validator.Validate(StreamFrom(texto), "falso.png", "image/png", texto.Length);

        Assert.False(result.IsValid);
        Assert.Equal(ImageFileValidator.CodeCorruptFile, result.ErrorCode);
    }

    [Fact]
    public void Validate_firma_magica_mantiene_posicion_del_stream()
    {
        var validator = BuildValidator();
        var stream = StreamFrom(Png1x1);

        validator.Validate(stream, "comida.png", "image/png", Png1x1.Length);

        Assert.Equal(0, stream.Position);
    }
}