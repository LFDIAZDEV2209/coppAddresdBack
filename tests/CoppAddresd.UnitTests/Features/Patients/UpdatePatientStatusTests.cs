using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Features.Patients.Events;
using CoppAddresd.Application.Interfaces;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Toggle de estado del paciente: actualiza solo status (nunca el agregado),
/// es idempotente con el mismo estado, notifica la pre-agregación CQRS y
/// devuelve null cuando el paciente no existe. El validador rechaza cualquier
/// estado fuera de Activo/Inactivo (Pendiente incluido).
/// </summary>
public class UpdatePatientStatusTests
{
    private readonly IPatientRepository _repository = Substitute.For<IPatientRepository>();
    private readonly IPatientMetricsQueue _metricsQueue = Substitute.For<IPatientMetricsQueue>();

    [Fact]
    public async Task Status_Valido_ActualizaYNotificaMetrica()
    {
        var patientId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        _repository
            .GetStatusSnapshotAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(new PatientStatusSnapshot("Activo", clinicId));
        var handler = new UpdatePatientStatusCommandHandler(_repository, _metricsQueue);

        var result = await handler.Handle(
            new UpdatePatientStatusCommand(patientId, "Inactivo", Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal("Inactivo", result!.Status);
        await _repository
            .Received(1)
            .UpdateStatusAsync(
                patientId,
                "Inactivo",
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>()
            );
        await _metricsQueue
            .Received(1)
            .EnqueueAsync(
                Arg.Is<PatientStatusChangedMetricEvent>(e =>
                    e.PatientId == patientId
                    && e.ClinicId == clinicId
                    && e.OldStatus == "Activo"
                    && e.NewStatus == "Inactivo"
                )
            );
    }

    [Fact]
    public async Task Status_MismoEstado_EsNoOp()
    {
        var patientId = Guid.NewGuid();
        _repository
            .GetStatusSnapshotAsync(patientId, Arg.Any<CancellationToken>())
            .Returns(new PatientStatusSnapshot("Activo", null));
        var handler = new UpdatePatientStatusCommandHandler(_repository, _metricsQueue);

        var result = await handler.Handle(
            new UpdatePatientStatusCommand(patientId, "Activo", Guid.NewGuid()),
            CancellationToken.None
        );

        Assert.Equal("Activo", result!.Status);
        await _repository
            .DidNotReceiveWithAnyArgs()
            .UpdateStatusAsync(default, default!, default, default);
        await _metricsQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!);
    }

    [Fact]
    public async Task Status_PacienteInexistente_DevuelveNull()
    {
        _repository
            .GetStatusSnapshotAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((PatientStatusSnapshot?)null);
        var handler = new UpdatePatientStatusCommandHandler(_repository, _metricsQueue);

        var result = await handler.Handle(
            new UpdatePatientStatusCommand(Guid.NewGuid(), "Inactivo", null),
            CancellationToken.None
        );

        Assert.Null(result);
        await _repository
            .DidNotReceiveWithAnyArgs()
            .UpdateStatusAsync(default, default!, default, default);
    }

    [Theory]
    [InlineData("Pendiente")]
    [InlineData("Activo ")]
    [InlineData("activo")]
    [InlineData("")]
    public void Validator_RechazaEstadosNoPermitidos(string status)
    {
        var validator = new UpdatePatientStatusCommandValidator();

        var result = validator.Validate(
            new UpdatePatientStatusCommand(Guid.NewGuid(), status, null)
        );

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("Activo")]
    [InlineData("Inactivo")]
    public void Validator_AceptaEstadosPermitidos(string status)
    {
        var validator = new UpdatePatientStatusCommandValidator();

        var result = validator.Validate(
            new UpdatePatientStatusCommand(Guid.NewGuid(), status, null)
        );

        Assert.True(result.IsValid);
    }
}
