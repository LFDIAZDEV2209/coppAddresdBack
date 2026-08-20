namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Categoría de documentos clínicos (catálogo). Agrupa los tipos de documento
/// (ej. categoría "Laboratorio" → tipos "CBC", "Panel lipídico"...). Catálogo
/// extensible: agregar filas nunca requiere cambios de código.
/// </summary>
public sealed class DocumentCategory
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<ClinicalDocumentType> Types { get; set; } = [];
}