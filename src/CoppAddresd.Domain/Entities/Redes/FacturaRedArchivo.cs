namespace CoppAddresd.Domain.Entities.Redes;

/// <summary>
/// Archivo RIPS de una factura de red (AF, US, AP, AH, AN, AC, AU, AM, AT, FA
/// o la factura en PDF): nombre del archivo, número de registros y tamaño.
/// </summary>
public sealed class FacturaRedArchivo
{
    public Guid Id { get; set; }

    public Guid FacturaId { get; set; }

    public string Tipo { get; set; } = "AF";

    public string Nombre { get; set; } = default!;

    public int Registros { get; set; }

    public string? Tamano { get; set; }
}
