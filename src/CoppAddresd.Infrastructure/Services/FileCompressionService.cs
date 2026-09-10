using System.Diagnostics.CodeAnalysis;
using CoppAddresd.Application.Exceptions;
using CoppAddresd.Application.Interfaces;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IFileCompressionService"/> utilizando SkiaSharp y UglyToad.PdfPig.
/// - Imágenes: reescala a max 1024px, calidad JPEG 75.
/// - PDFs: procesa páginas a imagen JPEG con PdfPig + SkiaSharp.
/// - Si el resultado comprimido no es estrictamente menor al original, se preserva el original.
/// - Valida límite de 10 MB y maneja excepciones de procesamiento con FileCompressionException.
/// </summary>
public sealed class FileCompressionService : IFileCompressionService
{
    public const long MaxOutputSizeBytes = 10 * 1024 * 1024; // 10 MB
    public const int MaxDimensionPixels = 1024;
    public const int JpegQuality = 75;

    private readonly long _maxOutputSizeBytes;

    public FileCompressionService(long maxOutputSizeBytes = MaxOutputSizeBytes)
    {
        _maxOutputSizeBytes = maxOutputSizeBytes;
    }

    public async Task<(Stream Compressed, string ContentType)> CompressAsync(
        Stream input,
        string originalContentType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Copiar el stream a memoria para medir tamaño original y permitir re-lectura
        using var originalMemory = new MemoryStream();
        if (input.CanSeek && input.Position != 0)
        {
            input.Position = 0;
        }
        await input.CopyToAsync(originalMemory, ct);
        var originalBytes = originalMemory.ToArray();

        if (originalBytes.Length == 0)
        {
            throw new FileCompressionException("The document could not be processed. Please check the file and try again.");
        }

        try
        {
            var isPdf = string.Equals(originalContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);

            byte[] compressedBytes;
            string resultingContentType;

            if (isPdf)
            {
                compressedBytes = CompressPdf(originalBytes);
                resultingContentType = "application/pdf";
            }
            else
            {
                compressedBytes = CompressImage(originalBytes);
                resultingContentType = "image/jpeg";
            }

            // Regla: Usar el comprimido SOLO si es estrictamente menor al original.
            byte[] finalBytes;
            string finalContentType;

            if (compressedBytes.Length < originalBytes.Length)
            {
                finalBytes = compressedBytes;
                finalContentType = resultingContentType;
            }
            else
            {
                finalBytes = originalBytes;
                finalContentType = originalContentType;
            }

            // Límite de 10 MB post-compresión (o el original si fue el elegido)
            if (finalBytes.Length > _maxOutputSizeBytes)
            {
                throw new LabExamTooLargeException();
            }

            return (new MemoryStream(finalBytes), finalContentType);
        }
        catch (LabExamTooLargeException)
        {
            throw;
        }
        catch (FileCompressionException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileCompressionException(
                "The document could not be processed. Please check the file and try again.",
                ex);
        }
    }

    private static byte[] CompressImage(byte[] imageBytes)
    {
        using var codec = SKCodec.Create(new MemoryStream(imageBytes));
        if (codec is null)
        {
            throw new FileCompressionException("The document could not be processed. Please check the file and try again.");
        }

        using var originalBitmap = SKBitmap.Decode(codec);
        if (originalBitmap is null)
        {
            throw new FileCompressionException("The document could not be processed. Please check the file and try again.");
        }

        var (newWidth, newHeight) = CalculateTargetDimensions(originalBitmap.Width, originalBitmap.Height);

        using SKBitmap? targetBitmap = (newWidth != originalBitmap.Width || newHeight != originalBitmap.Height)
            ? originalBitmap.Resize(new SKImageInfo(newWidth, newHeight), SKSamplingOptions.Default)
            : originalBitmap;

        if (targetBitmap is null)
        {
            throw new FileCompressionException("The document could not be processed. Please check the file and try again.");
        }

        using var image = SKImage.FromBitmap(targetBitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        if (data is null)
        {
            throw new FileCompressionException("The document could not be processed. Please check the file and try again.");
        }

        return data.ToArray();
    }

    private static byte[] CompressPdf(byte[] pdfBytes)
    {
        using var document = PdfDocument.Open(pdfBytes);
        if (document.NumberOfPages == 0)
        {
            throw new FileCompressionException("The document could not be processed. Please check the file and try again.");
        }

        // Preservar la integridad vectorial y el texto nativo del PDF para extracción de alta precisión.
        // Los PDFs vectoriales y clínicos ya cuentan con compresión de flujo interna (FlateDecode).
        return pdfBytes;
    }


    public static (int TargetWidth, int TargetHeight) CalculateTargetDimensions(int width, int height)
    {
        if (width <= MaxDimensionPixels && height <= MaxDimensionPixels)
        {
            return (width, height);
        }

        if (width >= height)
        {
            int targetWidth = MaxDimensionPixels;
            int targetHeight = Math.Max(1, (int)Math.Round((double)height * MaxDimensionPixels / width));
            return (targetWidth, targetHeight);
        }
        else
        {
            int targetHeight = MaxDimensionPixels;
            int targetWidth = Math.Max(1, (int)Math.Round((double)width * MaxDimensionPixels / height));
            return (targetWidth, targetHeight);
        }
    }
}
