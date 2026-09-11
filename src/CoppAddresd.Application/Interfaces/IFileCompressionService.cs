namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Comprime archivos de examen (imágenes o PDF) antes del almacenamiento y del
/// envío al AI Service. Límites: 20 MB raw / 10 MB post-compresión.
/// </summary>
public interface IFileCompressionService
{
    /// <summary>
    /// Devuelve un stream comprimido y el content-type resultante.
    /// PDFs se procesan/renderizan o reempaquetan a imágenes/JPEG (PdfPig + SkiaSharp).
    /// Imágenes se reescalan y re-encodifican (SkiaSharp, quality 75, max 1024 px).
    /// Si el archivo comprimido es mayor o igual al original, se preserva el original.
    /// Lanza <see cref="Exceptions.LabExamTooLargeException"/> si el resultado supera 10 MB.
    /// Lanza <see cref="Exceptions.FileCompressionException"/> si el archivo está corrupto o no se puede procesar.
    /// </summary>
    Task<(Stream Compressed, string ContentType)> CompressAsync(
        Stream input,
        string originalContentType,
        CancellationToken ct = default);
}
