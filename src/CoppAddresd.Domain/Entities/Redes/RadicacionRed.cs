namespace CoppAddresd.Domain.Entities.Redes;

/// <summary>
/// Radicación de autorización solicitada por una red prestadora (nivel de
/// urgencias por defecto): diagnóstico CIE-10 + líneas CUPS/CUM con valores
/// editables + cotización adjunta opcional.
/// </summary>
public sealed class RadicacionRed
{
    public Guid Id { get; set; }

    /// <summary>Consecutivo público RAD-YYYY-#### generado por el servidor.</summary>
    public string Consecutivo { get; set; } = default!;

    public string Nit { get; set; } = default!;

    public string RazonSocial { get; set; } = default!;

    /// <summary>urgencias | consulta-externa | hospitalizacion.</summary>
    public string Nivel { get; set; } = "urgencias";

    public string DiagnosticoCie10 { get; set; } = default!;

    public string? DiagnosticoDescripcion { get; set; }

    /// <summary>alta | media | baja.</summary>
    public string Prioridad { get; set; } = "alta";

    public string? Observaciones { get; set; }

    public string? CotizacionNombreArchivo { get; set; }

    public string? CotizacionNumero { get; set; }

    public DateTime? CotizacionFecha { get; set; }

    public decimal? CotizacionMonto { get; set; }

    public decimal Total { get; set; }

    /// <summary>radicada | en-revision | aprobada | rechazada.</summary>
    public string Estado { get; set; } = "radicada";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<RadicacionRedLinea> Lineas { get; set; } = [];
}
