namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Tipo de documento clínico (catálogo, schema <c>app</c>). Pertenece a una
/// <see cref="DocumentCategory"/> y define las extensiones de archivo
/// permitidas (opcional) para validar la subida. No confundir con
/// <c>DocumentType</c> (documentos de identidad del paciente).
/// </summary>
public sealed class ClinicalDocumentType
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Extensiones permitidas en minúsculas sin punto (ej. pdf, png, jpg). Vacío = sin restricción.</summary>
    public List<string> AllowedExtensions { get; set; } = [];

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DocumentCategory Category { get; set; } = default!;
}