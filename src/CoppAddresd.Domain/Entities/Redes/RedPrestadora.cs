namespace CoppAddresd.Domain.Entities.Redes;

/// <summary>
/// Red prestadora de servicios de salud (IPS) del acceso a redes del ERP.
/// Catálogo de referencia: las radicaciones y facturas RIPS la referencian
/// por NIT, igual que los catálogos clínicos del módulo de pacientes.
/// </summary>
public sealed class RedPrestadora
{
    public Guid Id { get; set; }

    public string Nit { get; set; } = default!;

    public string RazonSocial { get; set; } = default!;

    public string? Direccion { get; set; }

    public string? Ciudad { get; set; }

    public string? Departamento { get; set; }

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public string? CodigoPrestador { get; set; }

    /// <summary>habilitado | no-habilitado (estado de habilitación REPS).</summary>
    public string Habilitacion { get; set; } = "habilitado";

    /// <summary>privada | publica | mixta.</summary>
    public string Naturaleza { get; set; } = "privada";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
