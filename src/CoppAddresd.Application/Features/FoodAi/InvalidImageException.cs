namespace CoppAddresd.Application.Features.FoodAi;

/// <summary>
/// Imagen inválida según las reglas de validación de ingestión
/// (<see cref="ImageFileValidator"/>). El código identifica el motivo para
/// respuestas de error consistentes (INVALID_IMAGE, IMAGE_TOO_LARGE, ...).
/// </summary>
public sealed class InvalidImageException : Exception
{
    public InvalidImageException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Código de error estable para el cliente (p. ej. INVALID_IMAGE).</summary>
    public string Code { get; }
}