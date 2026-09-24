using CoppAddresd.Application.Features.Measurements.Queries.GetMyMetricsHistory;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.MetricsHistory;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetMetricsHistory;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Measurements;

/// <summary>
/// Pruebas del historial abierto <c>GET /api/v1/me/metrics-history</c>
/// (Fase 7, móvil): sin inscripción devuelve la serie (nunca 404
/// <c>NO_ACTIVE_ENROLLMENT</c>), códigos desconocidos → 400
/// <c>METRICS_UNKNOWN</c>, días fuera de [7, 365] → 400, y el endpoint de
/// programa <c>GET /api/v1/program/me/metrics-history</c> SIGUE devolviendo
/// 404 sin inscripción (contrato intacto, sin modificarlo).
/// </summary>
public sealed class GetMyMetricsHistoryTests
{
    private readonly IPatientRepository _patients = Substitute.For<IPatientRepository>();
    private readonly IMetricsHistoryRepository _repository =
        Substitute.For<IMetricsHistoryRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    private GetMyMetricsHistoryQueryHandler BuildHandler() =>
        new(
            _patients,
            _repository,
            _cache,
            Substitute.For<ILogger<GetMyMetricsHistoryQueryHandler>>()
        );

    /// <summary>Caché mockeado que ejecuta la factory (miss siempre).</summary>
    private void WireCachePassthrough()
    {
        _cache
            .GetOrCreateAsync(
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<Func<CancellationToken, Task<MetricsHistoryCacheDto>>>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(callInfo =>
                callInfo
                    .Arg<Func<CancellationToken, Task<MetricsHistoryCacheDto>>>()
                    .Invoke(callInfo.Arg<CancellationToken>())
            );
    }

    private static PatientProfile BuildPatient() =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "Ruiz",
            Status = "Activo",
        };

    /// <summary>Contexto abierto con una medición de peso hoy (ventana UTC).</summary>
    private static MetricsHistoryContext BuildWeightContext()
    {
        var metricId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return new MetricsHistoryContext(
            HeightCm: null,
            WindowEnd: today,
            Metrics: [new MetricsHistoryMetricRow(metricId, "weight", "kg")],
            Measurements:
            [
                new MetricsHistoryMeasurementRow(
                    metricId,
                    today,
                    70.5m,
                    "kg",
                    DateTime.UtcNow,
                    Guid.NewGuid()
                ),
            ],
            Ranges: [],
            Baselines: []
        );
    }

    [Fact]
    public async Task Handle_SinInscripcion_DevuelveSerieSin404()
    {
        // Arrange: paciente con perfil pero SIN inscripción (el handler abierto
        // nunca consulta la inscripción).
        var patient = BuildPatient();
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _repository
            .GetOpenMetricsHistoryContextAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(BuildWeightContext());
        WireCachePassthrough();

        // Act.
        var result = await BuildHandler()
            .Handle(
                new GetMyMetricsHistoryQuery(Guid.NewGuid(), ["weight"], 7),
                CancellationToken.None
            );

        // Assert: serie con datos, sin 404.
        var metric = Assert.Single(result.Metrics);
        Assert.Equal("weight", metric.Code);
        Assert.NotEmpty(metric.Points);
        await _repository
            .DidNotReceiveWithAnyArgs()
            .HasActiveEnrollmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CodigoDesconocido_LanzaMetricsUnknown()
    {
        // Arrange: catálogo abierto solo conoce "weight".
        var patient = BuildPatient();
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _repository
            .GetOpenMetricsHistoryContextAsync(patient.Id, Arg.Any<CancellationToken>())
            .Returns(BuildWeightContext());
        WireCachePassthrough();

        // Act + Assert: 400 con el mismo formato que el endpoint de programa.
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            BuildHandler()
                .Handle(
                    new GetMyMetricsHistoryQuery(Guid.NewGuid(), ["no_existe"], 7),
                    CancellationToken.None
                )
        );
        Assert.Contains("METRICS_UNKNOWN", ex.Errors.First().ErrorMessage);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(366)]
    public async Task Handle_DiasFueraDeRango_LanzaValidation(int days)
    {
        // Arrange: paciente válido (la validación de días ocurre primero).
        _patients
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(BuildPatient());

        // Act + Assert: 400 en el borde inferior y superior.
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            BuildHandler()
                .Handle(
                    new GetMyMetricsHistoryQuery(Guid.NewGuid(), ["weight"], days),
                    CancellationToken.None
                )
        );
        Assert.Equal("days", ex.Errors.First().PropertyName);
    }

    [Fact]
    public async Task Programa_SinInscripcion_SigueSiendo404()
    {
        // El contrato del endpoint de programa NO cambia: sin inscripción
        // activa sigue devolviendo 404 NO_ACTIVE_ENROLLMENT (verificación sin
        // modificar GetMetricsHistoryQuery ni ProgramController).
        var repository = Substitute.For<IMetricsHistoryRepository>();
        repository
            .HasActiveEnrollmentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);
        var handler = new GetMetricsHistoryQueryHandler(
            repository,
            Substitute.For<ICacheService>(),
            Substitute.For<ILogger<GetMetricsHistoryQueryHandler>>()
        );

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(
                new GetMetricsHistoryQuery(Guid.NewGuid(), ["weight"], 180),
                CancellationToken.None
            )
        );
        Assert.Contains("NO_ACTIVE_ENROLLMENT", ex.Message);
    }
}
