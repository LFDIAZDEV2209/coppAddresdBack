using CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using NSubstitute;

namespace CoppAddresd.UnitTests.Measurements;

/// <summary>
/// Pruebas del query self-service <c>GetMyMeasurements</c> (Fase 7, móvil):
/// resolución del paciente por JWT, validación de frontera (PageSize y
/// códigos contra el catálogo activo) y contrato mínimo del DTO.
/// El repositorio paginado se mockea: el SQL con cursor vive en la siguiente
/// tarea (aquí solo se verifica la delegación).
/// </summary>
public sealed class GetMyMeasurementsTests
{
    private readonly IPatientRepository _patients = Substitute.For<IPatientRepository>();
    private readonly IClinicalMeasurementRepository _catalog =
        Substitute.For<IClinicalMeasurementRepository>();
    private readonly IPatientMeasurementRepository _measurements =
        Substitute.For<IPatientMeasurementRepository>();

    private GetMyMeasurementsQueryHandler BuildHandler() => new(_patients, _catalog, _measurements);

    private static PatientProfile BuildPatient(Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            FirstName = "Ana",
            LastName = "Ruiz",
            Status = "Activo",
        };

    private static MeasurementMetric BuildMetric(string code) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            DefaultUnitId = Guid.NewGuid(),
            Category = "vital",
            IsActive = true,
        };

    [Fact]
    public async Task Handle_UsuarioSinPerfil_LanzaNotFound()
    {
        // Arrange: el JWT no tiene perfil vinculado en patient_profiles.
        _patients
            .GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PatientProfile?)null);

        var handler = BuildHandler();

        // Act + Assert: 404 sin revelar otros perfiles.
        var query = new GetMyMeasurementsQuery(Guid.NewGuid());
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(query, CancellationToken.None)
        );

        // No debe tocar catálogo ni SQL paginado tras el 404.
        await _catalog
            .DidNotReceiveWithAnyArgs()
            .GetActiveMetricsWithUnitsAsync(Arg.Any<CancellationToken>());
        await _measurements
            .DidNotReceiveWithAnyArgs()
            .GetPagedAsync(
                Arg.Any<Guid>(),
                Arg.Any<string[]?>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Handle_CodigoInvalido_LanzaValidation()
    {
        // Arrange: paciente existe, catálogo solo conoce "weight".
        var patient = BuildPatient();
        _patients.GetByUserIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(patient);
        _catalog
            .GetActiveMetricsWithUnitsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { BuildMetric("weight") });

        var handler = BuildHandler();

        // Act + Assert: código desconocido → 400.
        var query = new GetMyMeasurementsQuery(
            patient.UserId ?? Guid.NewGuid(),
            MetricCodes: ["no_existe"]
        );
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(query, CancellationToken.None)
        );
        Assert.Contains("METRICS_UNKNOWN", ex.Errors.First().ErrorMessage);

        // No debe llegar al SQL paginado con un filtro inválido.
        await _measurements
            .DidNotReceiveWithAnyArgs()
            .GetPagedAsync(
                Arg.Any<Guid>(),
                Arg.Any<string[]?>(),
                Arg.Any<int>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Handle_PageSizeCero_LanzaValidation()
    {
        // Arrange: PageSize 0 falla en frontera, sin I/O.
        var handler = BuildHandler();

        // Act + Assert: 400.
        var query = new GetMyMeasurementsQuery(Guid.NewGuid(), PageSize: 0);
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(query, CancellationToken.None)
        );
        Assert.Equal("pageSize", ex.Errors.First().PropertyName);
    }

    [Fact]
    public async Task Handle_PageSizeMayor100_LanzaValidation()
    {
        // Arrange: PageSize 101 falla en frontera, sin I/O.
        var handler = BuildHandler();

        // Act + Assert: 400.
        var query = new GetMyMeasurementsQuery(Guid.NewGuid(), PageSize: 101);
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            handler.Handle(query, CancellationToken.None)
        );
        Assert.Equal("pageSize", ex.Errors.First().PropertyName);
    }

    [Fact]
    public async Task Handle_FlujoFeliz_DelegaAlRepositorioConPacienteResuelto()
    {
        // Arrange: paciente y catálogo válidos, página mockeada.
        var patient = BuildPatient();
        var userId = Guid.NewGuid();
        _patients.GetByUserIdAsync(userId, Arg.Any<CancellationToken>()).Returns(patient);
        _catalog
            .GetActiveMetricsWithUnitsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { BuildMetric("weight"), BuildMetric("bmi") });

        var expected = new CursorPagedResult<MeasurementItemDto>(
            [
                new MeasurementItemDto(
                    Guid.NewGuid(),
                    "weight",
                    "Peso",
                    70.5m,
                    "kg",
                    "kg",
                    DateTimeOffset.UtcNow,
                    "device"
                ),
            ],
            NextCursor: null,
            HasNextPage: false
        );
        _measurements
            .GetPagedAsync(
                patient.Id,
                Arg.Any<string[]?>(),
                20,
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(expected);

        var handler = BuildHandler();

        // Act: filtro con duplicado case-insensitive se normaliza.
        var result = await handler.Handle(
            new GetMyMeasurementsQuery(userId, PageSize: 20, MetricCodes: ["weight", "WEIGHT"]),
            CancellationToken.None
        );

        // Assert: devuelve la página del repositorio y normaliza el filtro.
        Assert.Same(expected, result);
        await _measurements
            .Received(1)
            .GetPagedAsync(
                patient.Id,
                Arg.Is<string[]?>(codes =>
                    codes != null && codes.Length == 1 && codes[0] == "weight"
                ),
                20,
                null,
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public void Dto_NoExponeCamposSensibles()
    {
        // El contrato móvil reducido no debe incluir notas clínicas, createdBy
        // ni encounterId (minimización de PHI): solo los 8 campos del spec.
        var names = typeof(MeasurementItemDto)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        var esperados = new[]
        {
            "Id",
            "MetricCode",
            "MetricName",
            "Value",
            "UnitCode",
            "UnitSymbol",
            "ObservedAt",
            "Source",
        };

        Assert.Equal(esperados.Length, names.Count);
        foreach (var campo in esperados)
        {
            Assert.Contains(campo, names);
        }

        Assert.DoesNotContain("Notes", names);
        Assert.DoesNotContain("CreatedBy", names);
        Assert.DoesNotContain("EncounterId", names);
        Assert.DoesNotContain("BatchId", names);
        Assert.DoesNotContain("SourceKey", names);
    }
}
