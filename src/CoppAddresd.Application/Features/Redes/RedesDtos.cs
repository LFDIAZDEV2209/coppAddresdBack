using CoppAddresd.Domain.Entities.Redes;

namespace CoppAddresd.Application.Features.Redes;

/// <summary>DTOs del módulo Acceso a Redes (radicaciones + facturación RIPS).</summary>

public sealed record RedPrestadoraDto(
    Guid Id,
    string Nit,
    string RazonSocial,
    string? Direccion,
    string? Ciudad,
    string? Departamento,
    string? Telefono,
    string? Email,
    string? CodigoPrestador,
    string Habilitacion,
    string Naturaleza);

public sealed record CupLineDto(
    Guid Id,
    string Tipo,
    string Codigo,
    string? Descripcion,
    int Cantidad,
    decimal ValorUnitario);

public sealed record CotizacionDto(
    string NombreArchivo,
    string Numero,
    string Fecha,
    decimal Monto);

public sealed record RadicacionDto(
    Guid Id,
    string Consecutivo,
    string CreatedAt,
    string Nit,
    string RazonSocial,
    string Nivel,
    string DiagnosticoCie10,
    string? DiagnosticoDescripcion,
    string Prioridad,
    string? Observaciones,
    IReadOnlyList<CupLineDto> Cups,
    CotizacionDto? Cotizacion,
    decimal Total,
    string Estado);

public sealed record CupLineInput(
    string Tipo,
    string Codigo,
    string? Descripcion,
    int Cantidad,
    decimal ValorUnitario);

public sealed record CotizacionInput(
    string NombreArchivo,
    string Numero,
    string Fecha,
    decimal Monto);

public sealed record RadicacionInput(
    string Nit,
    string Nivel,
    string DiagnosticoCie10,
    string? DiagnosticoDescripcion,
    string Prioridad,
    string? Observaciones,
    IReadOnlyList<CupLineInput> Cups,
    CotizacionInput? Cotizacion);

public sealed record RipsArchivoDto(
    Guid Id,
    string Tipo,
    string Nombre,
    int Registros,
    string? Tamano);

public sealed record FacturaDto(
    Guid Id,
    string Cuv,
    string FacturaNumero,
    string PrestadorRazonSocial,
    string PrestadorNit,
    string FechaRadicacion,
    decimal ValorTotal,
    decimal ValorNeto,
    string? UsuarioNombre,
    string? Cobertura,
    IReadOnlyDictionary<string, int> Registros,
    IReadOnlyList<RipsArchivoDto> Archivos,
    string Estado);

public sealed record RipsArchivoInput(
    string Tipo,
    string Nombre,
    int Registros,
    string? Tamano);

public sealed record FacturaInput(
    string Cuv,
    string FacturaNumero,
    string PrestadorNit,
    string PrestadorRazonSocial,
    string? FechaRadicacion,
    decimal ValorTotal,
    decimal ValorCopago,
    decimal ValorCuotaModeradora,
    decimal ValorNeto,
    string UsuarioTipo,
    string? UsuarioDocumento,
    string? UsuarioNombre,
    string? NumeroContrato,
    string? ModalidadContrato,
    string? Cobertura,
    string? PeriodoAtencion,
    IReadOnlyDictionary<string, int> Registros,
    IReadOnlyList<RipsArchivoInput> Archivos);

/// <summary>Respuesta de la validación de un CUV (autocompleto de la factura).</summary>
public sealed record CuvValidationDto(
    string Cuv,
    string PrestadorNit,
    string PrestadorRazonSocial,
    string FacturaNumero,
    string FechaExpedicion,
    decimal ValorTotal,
    decimal ValorCopago,
    decimal ValorCuotaModeradora,
    decimal ValorNeto,
    string UsuarioTipo,
    string UsuarioDocumento,
    string UsuarioNombre,
    string NumeroContrato,
    string ModalidadContrato,
    string Cobertura,
    string PeriodoAtencion);

/* ------------------------------ Mapeadores ------------------------------ */

public static class RedesMapper
{
    public static RedPrestadoraDto ToDto(RedPrestadora x) => new(
        x.Id, x.Nit, x.RazonSocial, x.Direccion, x.Ciudad, x.Departamento,
        x.Telefono, x.Email, x.CodigoPrestador, x.Habilitacion, x.Naturaleza);

    public static RadicacionDto ToDto(RadicacionRed x) => new(
        x.Id,
        x.Consecutivo,
        x.CreatedAt.ToString("yyyy-MM-dd"),
        x.Nit,
        x.RazonSocial,
        x.Nivel,
        x.DiagnosticoCie10,
        x.DiagnosticoDescripcion,
        x.Prioridad,
        x.Observaciones,
        x.Lineas
            .OrderBy(l => l.Codigo)
            .Select(l => new CupLineDto(l.Id, l.Tipo, l.Codigo, l.Descripcion, l.Cantidad, l.ValorUnitario))
            .ToList(),
        x.CotizacionNumero is null
            ? null
            : new CotizacionDto(
                x.CotizacionNombreArchivo ?? "",
                x.CotizacionNumero,
                x.CotizacionFecha?.ToString("yyyy-MM-dd") ?? "",
                x.CotizacionMonto ?? 0),
        x.Total,
        x.Estado);

    public static FacturaDto ToDto(FacturaRed x) => new(
        x.Id,
        x.Cuv,
        x.FacturaNumero,
        x.PrestadorRazonSocial,
        x.PrestadorNit,
        x.FechaRadicacion.ToString("yyyy-MM-dd"),
        x.ValorTotal,
        x.ValorNeto,
        x.UsuarioNombre,
        x.Cobertura,
        FacturaRedRegistros.Parse(x.RegistrosJson),
        x.Archivos
            .OrderBy(a => a.Tipo)
            .Select(a => new RipsArchivoDto(a.Id, a.Tipo, a.Nombre, a.Registros, a.Tamano))
            .ToList(),
        x.Estado);
}

/// <summary>
/// Mapa tipo RIPS → registros reportados en la factura. Serializado a JSONB
/// con las 10 claves del estándar para que el frontend siempre reciba el mapa
/// completo (valores en 0 por defecto).
/// </summary>
public static class FacturaRedRegistros
{
    public static readonly string[] TiposRips =
        ["AF", "US", "AP", "AH", "AN", "AC", "AU", "AM", "AT", "FA"];

    private static readonly string[] ClavesFactura =
        ["AF", "US", "AP", "AH", "AN", "AC", "AU", "AM", "AT", "FA"];

    public static Dictionary<string, int> Parse(string? json)
    {
        var map = ClavesFactura.ToDictionary(k => k, _ => 0);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.TryGetInt32(out var value) && map.ContainsKey(prop.Name))
                {
                    map[prop.Name] = value;
                }
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // JSONB corrupto o vacío: mapa en ceros (fail-safe de lectura).
        }
        return map;
    }

    public static string ToJson(IReadOnlyDictionary<string, int> registros)
    {
        var map = ClavesFactura.ToDictionary(
            k => k,
            k => registros.TryGetValue(k, out var v) ? Math.Max(0, v) : 0);
        return System.Text.Json.JsonSerializer.Serialize(map);
    }
}
