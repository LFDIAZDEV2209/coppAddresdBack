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
            TestData.PatientId, TestData.Org, TestData.SpecialtyId, null, TestData.Clinic, TestData.LocationId,
            null, "", TestData.UserId);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Reason");
    }

    [Fact]
    public void CreateRequest_Valido_EsValido()
    {
        var validator = new CreateTelemedicineRequestCommandValidator();
        var command = new CreateTelemedicineRequestCommand(
            TestData.PatientId, TestData.Org, TestData.SpecialtyId, null, TestData.Clinic, TestData.LocationId,
            null, "Dolor abdominal", TestData.UserId);

        Assert.True(validator.Validate(command).IsValid);
    }

    [Fact]
    public void ScheduleAppointment_SinIdsObligatorios_Invalido()
    {
        var validator = new ScheduleTelemedicineAppointmentCommandValidator();
        var command = new ScheduleTelemedicineAppointmentCommand(
            Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, null, null,
            DateTimeOffset.UtcNow.AddDays(1), null, Guid.Empty);

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
        var validator = new CancelTelemedicineAppointmentCommandValidator();
        var command = new CancelTelemedicineAppointmentCommand(Guid.NewGuid(), "", CancelledBy.Professional, TestData.UserId);

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void RescheduleAppointment_SinNuevoInicio_Invalido()
    {
        var validator = new RescheduleTelemedicineAppointmentCommandValidator();
        var command = new RescheduleTelemedicineAppointmentCommand(
            Guid.NewGuid(), default, null, null, RescheduleRequestedBy.Patient, TestData.UserId);

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void RescheduleAppointment_RazonLarga_Invalido()
    {
        var validator = new RescheduleTelemedicineAppointmentCommandValidator();
        var command = new RescheduleTelemedicineAppointmentCommand(
            Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), null, new string('x', 2001),
            RescheduleRequestedBy.Patient, TestData.UserId);

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
        var command = new EndSessionCommand(Guid.NewGuid(), new string('x', 501), TestData.UserId, false);

        Assert.False(validator.Validate(command).IsValid);
    }
}
