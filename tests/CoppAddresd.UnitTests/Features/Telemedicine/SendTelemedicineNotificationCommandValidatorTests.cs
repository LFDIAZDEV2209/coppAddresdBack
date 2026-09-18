using CoppAddresd.Application.Features.Telemedicine;
using FluentValidation;

namespace CoppAddresd.UnitTests.Features.Telemedicine;

/// <summary>
/// Tests de validación del DTO del endpoint interno de notificaciones de
/// Telemedicina (F2): payload válido, campos obligatorios y whitelist de
/// canales (Push/Sms, case-insensitive). El pipeline de MediatR convierte los
/// errores en 400.
/// </summary>
public sealed class SendTelemedicineNotificationCommandValidatorTests
{
    private readonly SendTelemedicineNotificationCommandValidator _validator = new();

    private static SendTelemedicineNotificationCommand Command(
        Guid? userId = null,
        string title = "Recordatorio de tu cita",
        string body = "Tu consulta es mañana a las 10:00.",
        IReadOnlyList<string>? channels = null,
        string? dedupeKey = null)
        => new(
            userId ?? Guid.NewGuid(),
            title,
            body,
            channels ?? ["Push", "Sms"],
            new Dictionary<string, string> { ["appointmentId"] = Guid.NewGuid().ToString() },
            dedupeKey);

    [Fact]
    public void Payload_valido_no_tiene_errores()
    {
        var result = _validator.Validate(Command());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UserId_vacio_falla()
    {
        var result = _validator.Validate(Command(userId: Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendTelemedicineNotificationCommand.UserId));
    }

    [Fact]
    public void Titulo_vacio_falla()
    {
        var result = _validator.Validate(Command(title: "   "));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendTelemedicineNotificationCommand.Title));
    }

    [Fact]
    public void Body_vacio_falla()
    {
        var result = _validator.Validate(Command(body: string.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendTelemedicineNotificationCommand.Body));
    }

    [Fact]
    public void Sin_canales_falla()
    {
        var result = _validator.Validate(Command(channels: []));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendTelemedicineNotificationCommand.Channels));
    }

    [Theory]
    [InlineData("Email")]
    [InlineData("Whatsapp")]
    [InlineData("")]
    public void Canal_desconocido_falla(string channel)
    {
        var result = _validator.Validate(Command(channels: [channel]));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendTelemedicineNotificationCommand.Channels));
    }

    [Fact]
    public void Canales_case_insensitive_son_validos()
    {
        var result = _validator.Validate(Command(channels: ["push", "SMS", " Push "]));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Titulo_demasiado_largo_falla()
    {
        var result = _validator.Validate(
            Command(title: new string('a', SendTelemedicineNotificationCommandValidator.MaxTitleLength + 1)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void DedupeKey_demasiado_larga_falla()
    {
        var result = _validator.Validate(
            Command(dedupeKey: new string('k', SendTelemedicineNotificationCommandValidator.MaxDedupeKeyLength + 1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(SendTelemedicineNotificationCommand.DedupeKey));
    }
}
