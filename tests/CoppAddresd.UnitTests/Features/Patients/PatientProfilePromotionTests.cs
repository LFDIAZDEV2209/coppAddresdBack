using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.Features.Patients.Events;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CoppAddresd.UnitTests.Features.Patients;

/// <summary>
/// Promoción de estado al completar el onboarding self-service de la app:
/// un paciente registrado provisionalmente por el ERP (Pendiente) pasa a
/// Activo y se emite la métrica de cambio; Inactivo nunca se toca.
/// </summary>
public class PatientProfilePromotionTests
{
    private readonly IPatientRepository _repository = Substitute.For<IPatientRepository>();
    private readonly IPatientMetricsQueue _metricsQueue = Substitute.For<IPatientMetricsQueue>();
    private readonly ILogger<UpdateMyPatientProfileCommandHandler> _logger = Substitute.For<
        ILogger<UpdateMyPatientProfileCommandHandler>
    >();

    private static readonly Guid UserId = Guid.NewGuid();

    private static UpdateMyPatientProfileCommand Command() =>
        new(
            UserId,
            DateOfBirth: null,
            Email: null,
            Phone: "5551234567",
            EmergencyName: null,
            EmergencyRelationship: null,
            EmergencyPhone: null,
            EmergencyEmail: null,
            InsurerId: null,
            MemberId: null
        );

    private static PatientProfile Patient(string status) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            FirstName = "Mauricio",
            LastName = "Polo",
            DocumentNumber = "12345678",
            Status = status,
            ClinicId = Guid.NewGuid(),
        };

    [Fact]
    public async Task Pendiente_AlCompletarOnboarding_SePromueveAActivoYNotificaMetrica()
    {
        var patient = Patient("Pendiente");
        _repository.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(patient);
        var handler = new UpdateMyPatientProfileCommandHandler(
            _repository,
            _logger,
            _metricsQueue
        );

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Activo", patient.Status);
        await _repository.Received(1).UpdateAsync(patient, Arg.Any<CancellationToken>());
        await _metricsQueue
            .Received(1)
            .EnqueueAsync(
                Arg.Is<PatientStatusChangedMetricEvent>(e =>
                    e.PatientId == patient.Id
                    && e.ClinicId == patient.ClinicId
                    && e.OldStatus == "Pendiente"
                    && e.NewStatus == "Activo"
                )
            );
    }

    [Fact]
    public async Task Activo_AlCompletarOnboarding_NoNotificaMetrica()
    {
        var patient = Patient("Activo");
        _repository.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(patient);
        var handler = new UpdateMyPatientProfileCommandHandler(
            _repository,
            _logger,
            _metricsQueue
        );

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Activo", patient.Status);
        await _metricsQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!);
    }

    [Fact]
    public async Task Inactivo_AlCompletarOnboarding_NoSeReactiva()
    {
        var patient = Patient("Inactivo");
        _repository.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(patient);
        var handler = new UpdateMyPatientProfileCommandHandler(
            _repository,
            _logger,
            _metricsQueue
        );

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Inactivo", patient.Status);
        await _metricsQueue.DidNotReceiveWithAnyArgs().EnqueueAsync(default!);
    }

    [Fact]
    public async Task Pendiente_SinColaDeMetricas_SePromueveIgual()
    {
        var patient = Patient("Pendiente");
        _repository.GetByUserIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(patient);
        var handler = new UpdateMyPatientProfileCommandHandler(_repository, _logger);

        var result = await handler.Handle(Command(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Activo", patient.Status);
    }
}
