using CoppAddresd.Application.Features.Redes;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.Redes;
using CoppAddresd.Domain.Exceptions;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Redes;

/// <summary>
/// Tests unitarios del módulo Acceso a Redes: handlers de radicaciones,
/// facturación RIPS y validación de CUV (fakes del repositorio con
/// NSubstitute, patrón PatientScopingTests).
/// </summary>
public sealed class RedesHandlersTests
{
    private readonly IRedesRepository _repository = Substitute.For<IRedesRepository>();

    private static readonly RedPrestadora RedHabilitada = new()
    {
        Nit = "900123456-1",
        RazonSocial = "Clínica Santa Bárbara S.A.S.",
        Habilitacion = "habilitado",
    };

    private static RadicacionInput RadicacionValida(
        string nit = "900123456-1",
        IReadOnlyList<CupLineInput>? cups = null) => new(
        Nit: nit,
        Nivel: "urgencias",
        DiagnosticoCie10: "S72.0",
        DiagnosticoDescripcion: "Fractura del cuello del fémur",
        Prioridad: "alta",
        Observaciones: null,
        Cups: cups ?? new List<CupLineInput>
        {
            new("CUPS", "870201", "Hemograma", 1, 62_000),
            new("CUM", "I-302", "Cloruro de sodio", 2, 18_000),
        },
        Cotizacion: null);

    private static FacturaInput FacturaInput(
        string cuv = "CUV-2026-00048291",
        IReadOnlyDictionary<string, int>? registros = null,
        IReadOnlyList<RipsArchivoInput>? archivos = null) => new(
        Cuv: cuv,
        FacturaNumero: "FV-12481",
        PrestadorNit: "900123456-1",
        PrestadorRazonSocial: "Clínica Santa Bárbara S.A.S.",
        FechaRadicacion: "2026-09-24",
        ValorTotal: 100_000,
        ValorCopago: 4_000,
        ValorCuotaModeradora: 1_500,
        ValorNeto: 94_500,
        UsuarioTipo: "cotizante",
        UsuarioDocumento: "1.038.442.117",
        UsuarioNombre: "Mariana Restrepo Vélez",
        NumeroContrato: "CTO-100",
        ModalidadContrato: "Evento",
        Cobertura: "Urgencias y hospitalización",
        PeriodoAtencion: "2026-08-20 a 2026-09-10",
        Registros: registros ?? new Dictionary<string, int> { ["AP"] = 6, ["AU"] = 2, ["AM"] = 4 },
        Archivos: archivos ?? new List<RipsArchivoInput>
        {
            new("AF", "AF-900123456-1-12481.txt", 12, "2.1 KB"),
            new("US", "US-900123456-1-12481.txt", 5, "0.9 KB"),
        });

    /* ----------------------------- Radicaciones ----------------------------- */

    [Fact]
    public async Task CreateRadicacion_NitDesconocido_LanzaRegla()
    {
        _repository.GetByNitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RedPrestadora?)null);
        var handler = new CreateRadicacionCommandHandler(_repository);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => handler.Handle(
                new CreateRadicacionCommand(RadicacionValida(nit: "999999999-9")), default));
    }

    [Fact]
    public async Task CreateRadicacion_SinLineas_LanzaRegla()
    {
        _repository.GetByNitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(RedHabilitada);
        var handler = new CreateRadicacionCommandHandler(_repository);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => handler.Handle(
                new CreateRadicacionCommand(
                    RadicacionValida(cups: new List<CupLineInput>())), default));
    }

    [Fact]
    public async Task CreateRadicacion_PrioridadInvalida_LanzaRegla()
    {
        _repository.GetByNitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(RedHabilitada);
        var handler = new CreateRadicacionCommandHandler(_repository);

        var input = new RadicacionInput(
            "900123456-1", "urgencias", "S72.0", null, "urgente", null,
            new List<CupLineInput> { new("CUPS", "870201", "Hemograma", 1, 62_000) },
            null);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => handler.Handle(new CreateRadicacionCommand(input), default));
    }

    [Fact]
    public async Task CreateRadicacion_Valida_GeneraConsecutivoTotalYEstado()
    {
        _repository.GetByNitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(RedHabilitada);
        _repository.NextRadicacionNumberAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(184);

        var handler = new CreateRadicacionCommandHandler(_repository);
        var dto = await handler.Handle(
            new CreateRadicacionCommand(RadicacionValida()), default);

        Assert.Multiple(
            () => Assert.Matches(@"^RAD-\d{4}-0184$", dto.Consecutivo),
            () => Assert.Equal(62_000 + 2 * 18_000, dto.Total),
            () => Assert.Equal("radicada", dto.Estado),
            () => Assert.Equal(2, dto.Cups.Count),
            () => Assert.Equal("900123456-1", dto.Nit));

        await _repository.Received(1).AddRadicacionAsync(
            Arg.Is<RadicacionRed>(r => r.Estado == "radicada"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateRadicacion_ConCotizacion_AdjuntaMetadatos()
    {
        _repository.GetByNitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(RedHabilitada);
        _repository.NextRadicacionNumberAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var handler = new CreateRadicacionCommandHandler(_repository);
        var input = new RadicacionInput(
            "900123456-1", "urgencias", "S06.0", null, "alta", null,
            new List<CupLineInput> { new("CUPS", "870404", "TAC de cráneo", 1, 780_000) },
            new CotizacionInput("COTIZACION-URGENCIAS.pdf", "COT-2026-0912", "2026-09-12", 1_482_500));

        var dto = await handler.Handle(new CreateRadicacionCommand(input), default);

        Assert.NotNull(dto.Cotizacion);
        Assert.Equal("COT-2026-0912", dto.Cotizacion!.Numero);
        Assert.Equal("2026-09-12", dto.Cotizacion.Fecha);
        Assert.Equal(780_000, dto.Total);
    }

    /* ------------------------------- Facturas ------------------------------- */

    [Fact]
    public async Task CreateFactura_CuvCorto_LanzaRegla()
    {
        var handler = new CreateFacturaCommandHandler(_repository);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => handler.Handle(new CreateFacturaCommand(FacturaInput(cuv: "CUV-1")), default));
    }

    [Fact]
    public async Task CreateFactura_CuvDuplicado_LanzaRegla()
    {
        _repository.ExistsFacturaCuvAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var handler = new CreateFacturaCommandHandler(_repository);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => handler.Handle(new CreateFacturaCommand(FacturaInput()), default));
    }

    [Fact]
    public async Task CreateFactura_SinArchivos_LanzaRegla()
    {
        _repository.ExistsFacturaCuvAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var handler = new CreateFacturaCommandHandler(_repository);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => handler.Handle(
                new CreateFacturaCommand(
                    FacturaInput(archivos: new List<RipsArchivoInput>())), default));
    }

    [Fact]
    public async Task CreateFactura_TipoArchivoInvalido_LanzaRegla()
    {
        _repository.ExistsFacturaCuvAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var handler = new CreateFacturaCommandHandler(_repository);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => handler.Handle(
                new CreateFacturaCommand(FacturaInput(
                    archivos: new List<RipsArchivoInput> { new("XX", "XX.txt", 1, "1 KB") })),
                default));
    }
    [Fact]
    public async Task CreateFactura_Valida_NormalizaRegistrosYEstado()
    {
        _repository.ExistsFacturaCuvAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var handler = new CreateFacturaCommandHandler(_repository);

        var dto = await handler.Handle(new CreateFacturaCommand(FacturaInput(
            registros: new Dictionary<string, int>
            {
                ["AP"] = 6,
                ["AU"] = 2,
                ["AM"] = 4,
                ["ZZ"] = 99, // tipo desconocido: se ignora
            })), default);

        Assert.Multiple(
            () => Assert.Equal("cargada", dto.Estado),
            () => Assert.Equal(6, dto.Registros["AP"]),
            () => Assert.Equal(2, dto.Registros["AU"]),
            () => Assert.Equal(4, dto.Registros["AM"]),
            // Las 10 claves del estándar siempre presentes.
            () => Assert.Equal(10, dto.Registros.Count),
            () => Assert.Equal(0, dto.Registros["AF"]));
    }

    /* --------------------------------- CUV --------------------------------- */

    [Fact]
    public void ValidarCuv_MismoCuv_DevuelveMismosDatos()
    {
        var validator = new DeterministicCuvValidator();
        var prestadores = new List<RedPrestadoraDto>
        {
            new(Guid.NewGuid(), "900123456-1", "Clínica Santa Bárbara S.A.S.",
                null, null, null, null, null, null, "habilitado", "privada"),
            new(Guid.NewGuid(), "860012345-6", "Clínica del Country",
                null, null, null, null, null, null, "habilitado", "privada"),
        };

        var a = validator.Validate("CUV-2026-00048291", prestadores);
        var b = validator.Validate("cuv-2026-00048291", prestadores); // case-insensitive

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(a!.FacturaNumero, b!.FacturaNumero);
        Assert.Equal(a.PrestadorNit, b.PrestadorNit);
        Assert.Equal(a.ValorTotal, b.ValorTotal);
    }

    [Fact]
    public void ValidarCuv_Corto_SinPrestadores_DevuelveNull()
    {
        var validator = new DeterministicCuvValidator();
        var prestadores = new List<RedPrestadoraDto>
        {
            new(Guid.NewGuid(), "900123456-1", "X",
                null, null, null, null, null, null, "habilitado", "privada"),
        };

        Assert.Null(validator.Validate("CUV", prestadores)); // < 6 caracteres
        Assert.Null(validator.Validate("CUV12345", new List<RedPrestadoraDto>())); // sin catálogo
    }

    [Fact]
    public async Task ValidarCuvHandler_ResuelvePrestadorDelCatalogo()
    {
        _repository.ListRedesHabilitadasAsync(Arg.Any<CancellationToken>())
            .Returns(new List<RedPrestadora> { RedHabilitada });
        var handler = new ValidarCuvCommandHandler(_repository, new DeterministicCuvValidator());

        var dto = await handler.Handle(new ValidarCuvCommand("CUV-2026-00048291"), default);

        Assert.NotNull(dto);
        Assert.Equal("900123456-1", dto!.PrestadorNit);
        Assert.Equal("900123456-1", dto.PrestadorNit);
    }
}
