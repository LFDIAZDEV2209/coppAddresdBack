using CoppAddresd.Application.Common;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Application.Features.FoodAi;

/// <summary>
/// Resultado de la validación de una imagen entrante.
/// </summary>
public record ImageValidationResult(bool IsValid, string? ErrorCode = null, string? ErrorMessage = null);

/// <summary>
/// Validación de imágenes de comida: extensión, MIME, tamaño máximo, archivo
/// vacío y firma mágica (detección de archivos corruptos o renombrados).
/// Reglas puras, sin dependencias de infraestructura — unit testeable.
/// </summary>
public sealed class ImageFileValidator
{
    public const string CodeInvalidImage = "INVALID_IMAGE";
    public const string CodeImageTooLarge = "IMAGE_TOO_LARGE";
    public const string CodeEmptyFile = "EMPTY_FILE";
    public const string CodeCorruptFile = "CORRUPT_FILE";

    private static readonly string[] DefaultAllowedContentTypes =
        ["image/jpeg", "image/png", "image/webp"];

    private static readonly string[] DefaultAllowedExtensions =
        [".jpg", ".jpeg", ".png", ".webp"];

    private const int MagicBytesCount = 12;

    private readonly long _maxSizeBytes;
    private readonly IReadOnlyList<string> _allowedContentTypes;
    private readonly IReadOnlyList<string> _allowedExtensions;

    public ImageFileValidator(IOptions<FoodAiSettings> settings)
    {
        _maxSizeBytes = settings.Value.MaxImageSizeBytes;
        _allowedContentTypes = settings.Value.AllowedContentTypes.Count > 0
            ? settings.Value.AllowedContentTypes
            : DefaultAllowedContentTypes;
        _allowedExtensions = settings.Value.AllowedExtensions.Count > 0
            ? settings.Value.AllowedExtensions
            : DefaultAllowedExtensions;
    }

    public ImageValidationResult Validate(
        Stream image,
        string fileName,
        string contentType,
        long length)
    {
        if (length <= 0)
        {
            return new ImageValidationResult(false, CodeEmptyFile, "El archivo está vacío.");
        }

        if (length > _maxSizeBytes)
        {
            return new ImageValidationResult(
                false, CodeImageTooLarge, $"La imagen supera el tamaño máximo de {_maxSizeBytes / 1024 / 1024} MB.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!_allowedExtensions.Contains(extension))
        {
            return new ImageValidationResult(
                false, CodeInvalidImage, $"Extensión no permitida: {extension}.");
        }

        var normalizedContentType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        if (!_allowedContentTypes.Contains(normalizedContentType))
        {
            return new ImageValidationResult(
                false, CodeInvalidImage, $"Tipo de contenido no permitido: {contentType}.");
        }

        if (!HasValidMagicBytes(image, normalizedContentType))
        {
            return new ImageValidationResult(
                false, CodeCorruptFile, "El archivo no es una imagen válida.");
        }

        return new ImageValidationResult(true);
    }

    /// <summary>
    /// Verifica la firma mágica del archivo contra el content type declarado.
    /// Detecta archivos corruptos o renombrados (p. ej. .exe → .jpg).
    /// Restaura la posición del stream después de leer.
    /// </summary>
    private static bool HasValidMagicBytes(Stream image, string contentType)
    {
        var originalPosition = image.Position;
        try
        {
            var buffer = new byte[MagicBytesCount];
            var read = image.Read(buffer, 0, MagicBytesCount);
            if (read < 8)
            {
                return false;
            }

            return contentType switch
            {
                "image/jpeg" => buffer[0] == 0xFF && buffer[1] == 0xD8 && buffer[2] == 0xFF,
                "image/png" => buffer[0] == 0x89 && buffer[1] == 0x50 && buffer[2] == 0x4E && buffer[3] == 0x47,
                "image/webp" => buffer[0] == 0x52 && buffer[1] == 0x49 && buffer[2] == 0x46 && buffer[3] == 0x46
                    && buffer[8] == 0x57 && buffer[9] == 0x45 && buffer[10] == 0x42 && buffer[11] == 0x50,
                _ => false,
            };
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            image.Position = originalPosition;
        }
    }
}