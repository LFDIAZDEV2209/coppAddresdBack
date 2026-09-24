using CoppAddresd.Api.Authorization;
using CoppAddresd.Api.Constants;
using CoppAddresd.Application.Features.Redes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoppAddresd.Api.Controllers;

/// <summary>
/// Acceso a Redes: las redes prestadoras radican autorizaciones y cargan
/// sus facturas/RIPS. Endpoints espejo del módulo frontend `features/redes`.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/redes")]
public sealed class RedesController(IMediator mediator) : ControllerBase
{
    /// <summary>Busca la red por NIT (autocompleto del formulario de radicación).</summary>
    [HttpGet("redes")]
    [RequirePermission(PermissionCodes.RedesView)]
    public async Task<ActionResult<IReadOnlyList<RedPrestadoraDto>>> SearchRedes(
        [FromQuery] string? nit = null,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new SearchRedesPrestadorasQuery(nit), ct));

    /// <summary>Historial de radicaciones de autorizaciones (filtros: estado y búsqueda).</summary>
    [HttpGet("radicaciones")]
    [RequirePermission(PermissionCodes.RedesView)]
    public async Task<ActionResult<IReadOnlyList<RadicacionDto>>> ListRadicaciones(
        [FromQuery] string? estado = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListRadicacionesQuery(estado, search), ct));

    /// <summary>Rada una autorización: consecutivo server-side, total recalculado, estado "radicada".</summary>
    [HttpPost("radicaciones")]
    [RequirePermission(PermissionCodes.RedesManage)]
    public async Task<ActionResult<RadicacionDto>> CreateRadicacion(
        [FromBody] CreateRadicacionBody body,
        CancellationToken ct)
        => Ok(await mediator.Send(new CreateRadicacionCommand(body.ToInput()), ct));

    /// <summary>Historial de facturaciones y RIPS cargados por la red (búsqueda por CUV/factura/prestador).</summary>
    [HttpGet("facturas")]
    [RequirePermission(PermissionCodes.RedesView)]
    public async Task<ActionResult<IReadOnlyList<FacturaDto>>> ListFacturas(
        [FromQuery] string? search = null,
        CancellationToken ct = default)
        => Ok(await mediator.Send(new ListFacturasQuery(search), ct));

    /// <summary>Rada la factura RIPS: CUV autocompleta los datos (editables) y se registran los archivos.</summary>
    [HttpPost("facturas")]
    [RequirePermission(PermissionCodes.RedesManage)]
    public async Task<ActionResult<FacturaDto>> CreateFactura(
        [FromBody] CreateFacturaBody body,
        CancellationToken ct)
        => Ok(await mediator.Send(new CreateFacturaCommand(body.ToInput()), ct));

    /// <summary>
    /// Valida un CUV y devuelve los datos autocompletados de la factura.
    /// 404 cuando el código no es válido.
    /// </summary>
    [HttpPost("cuv/validar")]
    [RequirePermission(PermissionCodes.RedesView)]
    public async Task<ActionResult<CuvValidationDto>> ValidarCuv(
        [FromBody] ValidarCuvBody body,
        CancellationToken ct)
    {
        var result = await mediator.Send(new ValidarCuvCommand(body.Cuv), ct);
        return result is null ? NotFound() : Ok(result);
    }

    public sealed record ValidarCuvBody(string Cuv);

    public sealed record RadicacionBody(
        string Nit,
        string Nivel,
        string DiagnosticoCie10,
        string? DiagnosticoDescripcion,
        string Prioridad,
        string? Observaciones,
        IReadOnlyList<CupLineBody> Cups,
        CotizacionBody? Cotizacion);

    public sealed record CupLineBody(
        string Tipo,
        string Codigo,
        string? Descripcion,
        int Cantidad,
        decimal ValorUnitario);

    public sealed record CotizacionBody(
        string NombreArchivo,
        string Numero,
        string Fecha,
        decimal Monto);

    public sealed record CreateRadicacionBody(
        string Nit,
        string Nivel,
        string DiagnosticoCie10,
        string? DiagnosticoDescripcion,
        string Prioridad,
        string? Observaciones,
        IReadOnlyList<CupLineBody> Cups,
        CotizacionBody? Cotizacion)
    {
        public RadicacionInput ToInput() => new(
            Nit, Nivel, DiagnosticoCie10, DiagnosticoDescripcion, Prioridad,
            Observaciones,
            Cups.Select(c => new CupLineInput(c.Tipo, c.Codigo, c.Descripcion, c.Cantidad, c.ValorUnitario)).ToList(),
            Cotizacion is null
                ? null
                : new CotizacionInput(Cotizacion.NombreArchivo, Cotizacion.Numero, Cotizacion.Fecha, Cotizacion.Monto));
    }

    public sealed record ArchivoBody(string Tipo, string Nombre, int Registros, string? Tamano);

    public sealed record CreateFacturaBody(
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
        IReadOnlyList<ArchivoBody> Archivos)
    {
        public FacturaInput ToInput() => new(
            Cuv, FacturaNumero, PrestadorNit, PrestadorRazonSocial, FechaRadicacion,
            ValorTotal, ValorCopago, ValorCuotaModeradora, ValorNeto, UsuarioTipo,
            UsuarioDocumento, UsuarioNombre, NumeroContrato, ModalidadContrato,
            Cobertura, PeriodoAtencion, Registros,
            Archivos.Select(a => new RipsArchivoInput(a.Tipo, a.Nombre, a.Registros, a.Tamano)).ToList());
    }
}
