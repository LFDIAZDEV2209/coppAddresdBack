using CoppAddresd.Application.Features.Redes;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.Redes;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.Redes;

/* ============================ Redes prestadoras ============================ */

/// <summary>Busca redes prestadoras por NIT (catálogo erp.redes_prestadoras).</summary>
public record SearchRedesPrestadorasQuery(string? Nit)
    : IRequest<IReadOnlyList<RedPrestadoraDto>>;

public sealed class SearchRedesPrestadorasQueryHandler(IRedesRepository repository)
    : IRequestHandler<SearchRedesPrestadorasQuery, IReadOnlyList<RedPrestadoraDto>>
{
    public async Task<IReadOnlyList<RedPrestadoraDto>> Handle(
        SearchRedesPrestadorasQuery request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.Nit))
        {
            var hit = await repository.GetByNitAsync(request.Nit, ct);
            if (hit is null) return [];
            return [RedesMapper.ToDto(hit!)];
        }
        var redes = await repository.ListRedesHabilitadasAsync(ct);
        return redes.Select(x => RedesMapper.ToDto(x)).ToList();
    }
}

/* ============================== Radicaciones ============================== */

/// <summary>Lista el historial de radicaciones de autorizaciones (opcionalmente por estado/búsqueda).</summary>
public record ListRadicacionesQuery(string? Estado, string? Search)
    : IRequest<IReadOnlyList<RadicacionDto>>;

public sealed class ListRadicacionesQueryHandler(IRedesRepository repository)
    : IRequestHandler<ListRadicacionesQuery, IReadOnlyList<RadicacionDto>>
{
    public async Task<IReadOnlyList<RadicacionDto>> Handle(
        ListRadicacionesQuery request, CancellationToken ct)
    {
        var rows = await repository.ListRadicacionesAsync(request.Estado, request.Search, ct: ct);
        return rows.Select(x => RedesMapper.ToDto(x)).ToList();
    }
}

/// <summary>
/// Radica una autorización de una red prestadora: consecutivo RAD-YYYY-####
/// generado por el servidor, total recalculado server-side y estado inicial
/// "radicada". Exige que el NIT exista en el catálogo de redes.
/// </summary>
public record CreateRadicacionCommand(RadicacionInput Input)
    : IRequest<RadicacionDto>;

public sealed class CreateRadicacionCommandHandler(IRedesRepository repository)
    : IRequestHandler<CreateRadicacionCommand, RadicacionDto>
{
    private static readonly string[] NivelesValidos = ["urgencias", "consulta-externa", "hospitalizacion"];
    private static readonly string[] PrioridadesValidas = ["alta", "media", "baja"];
    private static readonly string[] TiposValidos = ["CUPS", "CUM"];

    public async Task<RadicacionDto> Handle(CreateRadicacionCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (!NivelesValidos.Contains(input.Nivel))
        {
            throw new BusinessRuleViolationException(
                $"Nivel de atención inválido: '{input.Nivel}'. Valores: urgencias, consulta-externa, hospitalizacion.");
        }
        if (!PrioridadesValidas.Contains(input.Prioridad))
        {
            throw new BusinessRuleViolationException(
                $"Prioridad inválida: '{input.Prioridad}'. Valores: alta, media, baja.");
        }
        if (input.Cups.Count == 0)
        {
            throw new BusinessRuleViolationException(
                "La radicación requiere al menos una línea CUPS o CUM.");
        }
        if (input.Cups.Any(c => !TiposValidos.Contains(c.Tipo)))
        {
            throw new BusinessRuleViolationException("Tipo de línea inválido: use CUPS o CUM.");
        }
        if (input.Cups.Any(c => c.Cantidad < 1))
        {
            throw new BusinessRuleViolationException("La cantidad de cada línea debe ser mayor o igual a 1.");
        }
        if (input.Cups.Any(c => c.ValorUnitario < 0))
        {
            throw new BusinessRuleViolationException("El valor unitario no puede ser negativo.");
        }

        var red = await repository.GetByNitAsync(input.Nit, ct)
            ?? throw new BusinessRuleViolationException(
                $"No existe una red prestadora con NIT '{input.Nit}'.");

        var year = DateTime.UtcNow.Year;
        var next = await repository.NextRadicacionNumberAsync(year, ct);
        var radicacion = new RadicacionRed
        {
            Consecutivo = $"RAD-{year}-{next:D4}",
            Nit = red.Nit,
            RazonSocial = red.RazonSocial,
            Nivel = input.Nivel,
            DiagnosticoCie10 = input.DiagnosticoCie10.Trim().ToUpperInvariant(),
            DiagnosticoDescripcion = input.DiagnosticoDescripcion?.Trim(),
            Prioridad = input.Prioridad,
            Observaciones = input.Observaciones?.Trim(),
            CotizacionNombreArchivo = input.Cotizacion?.NombreArchivo,
            CotizacionNumero = input.Cotizacion?.Numero,
            CotizacionFecha = ParseFecha(input.Cotizacion?.Fecha),
            CotizacionMonto = input.Cotizacion?.Monto,
            Total = input.Cups.Sum(c => c.Cantidad * c.ValorUnitario),
            Estado = "radicada",
            Lineas = input.Cups
                .Select(c => new RadicacionRedLinea
                {
                    Tipo = c.Tipo,
                    Codigo = c.Codigo.Trim(),
                    Descripcion = c.Descripcion?.Trim(),
                    Cantidad = c.Cantidad,
                    ValorUnitario = c.ValorUnitario,
                })
                .ToList(),
        };

        await repository.AddRadicacionAsync(radicacion, ct);
        return RedesMapper.ToDto(radicacion);
    }

    private static DateTime? ParseFecha(string? value)
        => DateTime.TryParse(value, out var date) ? date : null;
}

/* ============================ Facturación RIPS ============================ */

/// <summary>Lista el historial de facturaciones y RIPS cargados por la red.</summary>
public record ListFacturasQuery(string? Search)
    : IRequest<IReadOnlyList<FacturaDto>>;

public sealed class ListFacturasQueryHandler(IRedesRepository repository)
    : IRequestHandler<ListFacturasQuery, IReadOnlyList<FacturaDto>>
{
    public async Task<IReadOnlyList<FacturaDto>> Handle(
        ListFacturasQuery request, CancellationToken ct)
    {
        var rows = await repository.ListFacturasAsync(request.Search, ct: ct);
        return rows.Select(x => RedesMapper.ToDto(x)).ToList();
    }
}

/// <summary>
/// Rada una factura RIPS de la red: el CUV validado autocompleta los datos
/// (editables) y el servidor registra los archivos RIPS. Rechaza CUVs ya
/// radicados (idempotencia de cargue).
/// </summary>
public record CreateFacturaCommand(FacturaInput Input)
    : IRequest<FacturaDto>;

public sealed class CreateFacturaCommandHandler(IRedesRepository repository)
    : IRequestHandler<CreateFacturaCommand, FacturaDto>
{
    private static readonly string[] TiposArchivoValidos = FacturaRedRegistros.TiposRips;

    public async Task<FacturaDto> Handle(CreateFacturaCommand request, CancellationToken ct)
    {
        var input = request.Input;

        if (input.Cuv.Trim().Length < 6)
        {
            throw new BusinessRuleViolationException(
                "CUV inválido: debe tener al menos 6 caracteres.");
        }
        if (await repository.ExistsFacturaCuvAsync(input.Cuv.Trim(), ct))
        {
            throw new BusinessRuleViolationException(
                $"El CUV '{input.Cuv.Trim()}' ya fue radicado.");
        }
        if (input.Archivos.Count == 0)
        {
            throw new BusinessRuleViolationException(
                "El cargue requiere al menos un archivo RIPS o la factura.");
        }
        if (input.Archivos.Any(a => !TiposArchivoValidos.Contains(a.Tipo)))
        {
            throw new BusinessRuleViolationException(
                "Tipo de archivo RIPS inválido. Válidos: " + string.Join(", ", TiposArchivoValidos) + ".");
        }

        var factura = new FacturaRed
        {
            Cuv = input.Cuv.Trim(),
            FacturaNumero = input.FacturaNumero.Trim(),
            PrestadorNit = input.PrestadorNit.Trim(),
            PrestadorRazonSocial = input.PrestadorRazonSocial.Trim(),
            FechaRadicacion = ParseFecha(input.FechaRadicacion) ?? DateTime.UtcNow.Date,
            ValorTotal = input.ValorTotal,
            ValorCopago = input.ValorCopago,
            ValorCuotaModeradora = input.ValorCuotaModeradora,
            ValorNeto = input.ValorNeto,
            UsuarioTipo = input.UsuarioTipo == "beneficiario" ? "beneficiario" : "cotizante",
            UsuarioDocumento = input.UsuarioDocumento?.Trim(),
            UsuarioNombre = input.UsuarioNombre?.Trim(),
            NumeroContrato = input.NumeroContrato?.Trim(),
            ModalidadContrato = input.ModalidadContrato?.Trim(),
            Cobertura = input.Cobertura?.Trim(),
            PeriodoAtencion = input.PeriodoAtencion?.Trim(),
            Estado = "cargada",
            RegistrosJson = FacturaRedRegistros.ToJson(input.Registros),
            Archivos = input.Archivos
                .Select(a => new FacturaRedArchivo
                {
                    Tipo = a.Tipo,
                    Nombre = a.Nombre.Trim(),
                    Registros = Math.Max(0, a.Registros),
                    Tamano = a.Tamano,
                })
                .ToList(),
        };

        await repository.AddFacturaAsync(factura, ct);
        return RedesMapper.ToDto(factura);
    }

    private static DateTime? ParseFecha(string? value)
        => DateTime.TryParse(value, out var date) ? date.ToUniversalTime().Date : null;
}

/* ============================== Validar CUV =============================== */

/// <summary>
/// Valida un CUV para autocompletar los datos de la factura del prestador.
/// Devuelve la validación o null cuando el código no es válido (el endpoint
/// responde 404 para que el frontend muestre el error).
/// </summary>
public record ValidarCuvCommand(string Cuv)
    : IRequest<CuvValidationDto?>;

public sealed class ValidarCuvCommandHandler(
    IRedesRepository repository,
    ICuvValidator validator)
    : IRequestHandler<ValidarCuvCommand, CuvValidationDto?>
{
    public async Task<CuvValidationDto?> Handle(ValidarCuvCommand request, CancellationToken ct)
    {
        var prestadores = await repository.ListRedesHabilitadasAsync(ct);
        return validator.Validate(
            request.Cuv, prestadores.Select(x => RedesMapper.ToDto(x)).ToList());
    }
}
