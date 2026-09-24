namespace CoppAddresd.Domain.Entities.Redes;

/// <summary>
/// Factura de una red prestadora con su cargue RIPS. Los datos de la factura
/// llegan autocompletados desde la validación del CUV (código único de
/// validación de la plataforma de facturación) y son editables antes de
/// radicar; los archivos RIPS se registran por tipo (AF/US/AP/...).
/// </summary>
public sealed class FacturaRed
{
    public Guid Id { get; set; }

    /// <summary>Código Único de Validación reportado en la factura.</summary>
    public string Cuv { get; set; } = default!;

    public string FacturaNumero { get; set; } = default!;

    public string PrestadorNit { get; set; } = default!;

    public string PrestadorRazonSocial { get; set; } = default!;

    public DateTime FechaRadicacion { get; set; }

    public decimal ValorTotal { get; set; }

    public decimal ValorCopago { get; set; }

    public decimal ValorCuotaModeradora { get; set; }

    public decimal ValorNeto { get; set; }

    /// <summary>cotizante | beneficiario.</summary>
    public string UsuarioTipo { get; set; } = "cotizante";

    public string? UsuarioDocumento { get; set; }

    public string? UsuarioNombre { get; set; }

    public string? NumeroContrato { get; set; }

    public string? ModalidadContrato { get; set; }

    public string? Cobertura { get; set; }

    public string? PeriodoAtencion { get; set; }

    /// <summary>cargada | en-validacion | validada | rechazada.</summary>
    public string Estado { get; set; } = "cargada";

    /// <summary>Mapa tipo RIPS → registros reportados (JSONB, 10 claves).</summary>
    public string RegistrosJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<FacturaRedArchivo> Archivos { get; set; } = [];
}
