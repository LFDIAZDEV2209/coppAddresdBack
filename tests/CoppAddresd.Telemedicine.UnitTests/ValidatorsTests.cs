using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Enums;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Validadores FluentValidation de los comandos del microservicio.
/// </summary>
public class ValidatorsTests
{
    [Fact]
    public void CreateRequest_MotivoVacio_Invalido()
    {
        var validator = new CreateTelemedicineRequestCommandValidator();
        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId,
            TestData.Org,
            TestData.SpecialtyId,
            null,
            TestData.Clinic,
            TestData.LocationId,
            null,
            "",
            TestData.UserId,
            ErpMode: true
        );

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }

    [Fact]
    public void CreateRequest_Valido_EsValido()
    {
        var validator = new CreateTelemedicineRequestCommandValidator();
        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId,
            TestData.Org,
            TestData.SpecialtyId,
            null,
            TestData.Clinic,
            TestData.LocationId,
            null,
            "Dolor abdominal",
            TestData.UserId,
            ErpMode: true
        );

        Assert.True(validator.Validate(command).IsValid);
    }

    [Fact]
    public void ScheduleAppointment_SinIdsObligatorios_Invalido()
    {
        var validator = new ScheduleAppointmentCommandValidator();
        var command = new ScheduleAppointmentCommand(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            null,
            null,
            DateTimeOffset.UtcNow.AddDays(1),
            null,
            Guid.Empty
        );

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "PatientId");
        Assert.Contains(result.Errors, e => e.PropertyName == "ProfessionalId");
        Assert.Contains(result.Errors, e => e.PropertyName == "SpecialtyId");
        Assert.Contains(result.Errors, e => e.PropertyName == "CreatedBy");
    }

    [Fact]
    public void CancelAppointment_SinRazon_Invalido()
    {
        var validator = new CancelAppointmentCommandValidator();
        var command = new CancelAppointmentCommand(
            Guid.NewGuid(),
            "",
            CancelledBy.Professional,
            TestData.UserId
        );

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void ReviewRequest_RejectSinMotivo_Invalido()
    {
        var validator = new ReviewTelemedicineRequestCommandValidator();
        var command = new ReviewTelemedicineRequestCommand(
            Guid.NewGuid(),
            RequestDecision.Rejected,
            "",
            TestData.UserId,
            HasAdminView: true
        );

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }

    [Fact]
    public void ReviewRequest_ApproveSinMotivo_EsValido()
    {
        var validator = new ReviewTelemedicineRequestCommandValidator();
        var command = new ReviewTelemedicineRequestCommand(
            Guid.NewGuid(),
            RequestDecision.Approved,
            null,
            TestData.UserId,
            HasAdminView: true
        );

        Assert.True(validator.Validate(command).IsValid);
    }

    [Fact]
    public void RescheduleAppointment_SinNuevoInicio_Invalido()
    {
        var validator = new RescheduleAppointmentCommandValidator();
        var command = new RescheduleAppointmentCommand(
            Guid.NewGuid(),
            default,
            null,
            null,
            RescheduleRequestedBy.Patient,
            TestData.UserId
        );

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void RescheduleAppointment_RazonLarga_Invalido()
    {
        var validator = new RescheduleAppointmentCommandValidator();
        var command = new RescheduleAppointmentCommand(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1),
            null,
            new string('x', 2001),
            RescheduleRequestedBy.Patient,
            TestData.UserId
        );

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void JoinSession_SinUsuario_Invalido()
    {
        var validator = new JoinSessionCommandValidator();
        var command = new JoinSessionCommand(Guid.NewGuid(), Guid.Empty, false);

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void StartSession_Valido_EsValido()
    {
        var validator = new StartSessionCommandValidator();
        var command = new StartSessionCommand(Guid.NewGuid(), TestData.UserId, false);

        Assert.True(validator.Validate(command).IsValid);
    }

    [Fact]
    public void EndSession_RazonLarga_Invalido()
    {
        var validator = new EndSessionCommandValidator();
        var command = new EndSessionCommand(
            Guid.NewGuid(),
            new string('x', 501),
            TestData.UserId,
            false
        );

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void Backfill_RangoInvertido_Invalido()
    {
        var validator = new BackfillMetricsCommandValidator();
        var now = DateTimeOffset.UtcNow;
        var command = new BackfillMetricsCommand(now, now.AddDays(-5), null);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Backfill_RangoValido_EsValido()
    {
        var validator = new BackfillMetricsCommandValidator();
        var now = DateTimeOffset.UtcNow;
        var command = new BackfillMetricsCommand(now.AddDays(-30), now, TestData.Clinic);

        Assert.True(validator.Validate(command).IsValid);
    }

    [Fact]
    public void Backfill_SinRango_EsValido()
    {
        var validator = new BackfillMetricsCommandValidator();
        var command = new BackfillMetricsCommand(null, null, null, DryRun: true);

        Assert.True(validator.Validate(command).IsValid);
    }

    [Fact]
    public void Backfill_RangoFuturo_EsValido()
    {
        // La carga inicial debe poder cubrir citas ya programadas a futuro;
        // los lectores prefieren el pre-agregado cuando existe.
        var validator = new BackfillMetricsCommandValidator();
        var now = DateTimeOffset.UtcNow;
        var command = new BackfillMetricsCommand(now.AddDays(-30), now.AddDays(30), null);

        Assert.True(validator.Validate(command).IsValid);
    }
}
