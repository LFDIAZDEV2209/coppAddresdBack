namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Documento del repositorio clínico (Fase 5). Persiste SOLO metadata; el
/// binario vive en el storage de objetos (<c>IObjectStorageService</c>) bajo
/// <c>StorageKey</c>.
/// El vínculo con el paciente (y opcionalmente profesional/encounter) es
/// nullable por extensibilidad: un documento puede nacer de un evento clínico
/// (Fase 6), de un profesional o del directorio sin perder el modelo.
/// El versionado crea filas hijas (<c>ParentDocumentId</c>) con
/// <c>Version</c> incremental; la fila raíz es la identidad inmutable del
/// documento y las versiones son append-only (trazabilidad PHI).
/// </summary>
public sealed class Document
{
    public Guid Id { get; set; }

    public Guid? PatientId { get; set; }

    public Guid? ProfessionalId { get; set; }

    /// <summary>Clínica a la que pertenece el documento (regla anti-tenancy).</summary>
    public Guid? ClinicId { get; set; }

    public Guid? LocationId { get; set; }

    /// <summary>Evento clínico asociado (Fase 6). Columna preparada sin FK aún.</summary>
    public Guid? EncounterId { get; set; }

    public Guid DocumentTypeId { get; set; }

    public string Title { get; set; } = default!;

    public string? Description { get; set; }

    public string StorageKey { get; set; } = default!;

    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    public int Version { get; set; } = 1;

    public Guid? ParentDocumentId { get; set; }

    /// <summary>Estado del documento (Ready, Archived). Eliminación = DeletedAt.</summary>
    public string Status { get; set; } = "Ready";

    /// <summary>Usuario que subió el archivo (auth.users, FK por SQL).</summary>
    public Guid? UploadedBy { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }

    public Guid? DeletedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }

    public PatientProfile? Patient { get; set; }

    public Professional? Professional { get; set; }

    public Clinic? Clinic { get; set; }

    public Location? Location { get; set; }

    public ClinicalDocumentType DocumentType { get; set; } = default!;

    public Document? Parent { get; set; }

    public ICollection<Document> Versions { get; set; } = [];
}