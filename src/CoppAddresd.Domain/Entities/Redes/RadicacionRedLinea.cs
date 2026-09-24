namespace CoppAddresd.Domain.Entities.Redes;

/// <summary>
/// Línea CUPS o CUM de una radicación de autorización de red. Los valores
/// son editables por el profesional (cantidad/valor) y el servidor recalcula
/// el total de la radicación.
/// </summary>
public sealed class RadicacionRedLinea
{
    public Guid Id { get; set; }

    public Guid RadicacionId { get; set; }

    /// <summary>CUPS | CUM.</summary>
    public string Tipo { get; set; } = "CUPS";

    public string Codigo { get; set; } = default!;

    public string? Descripcion { get; set; }

    public int Cantidad { get; set; } = 1;

    public decimal ValorUnitario { get; set; }
}
