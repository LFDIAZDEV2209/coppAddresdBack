namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Código CPT (Current Procedural Terminology, EE. UU.) del catálogo de
/// referencia del módulo de pacientes. La descripción es propiedad del
/// código: las órdenes de procedimientos de los profesionales solo referencian
/// el código, igual que los diagnósticos con ICD-10.
/// </summary>
public sealed class CptCode
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
