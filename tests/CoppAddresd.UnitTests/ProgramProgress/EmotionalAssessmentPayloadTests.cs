using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.UnitTests.ProgramProgress;

public sealed class EmotionalAssessmentPayloadTests
{
    private static CompleteTaskCommand Command(EmotionalAssessmentPayload? payload, TaskCode code = TaskCode.emocional) =>
        new(Guid.NewGuid(), new DateOnly(2026, 11, 3), code, "test-assessment", null, 4, "tiempo", null, Emotional: payload);

    [Fact]
    public void ConservaEstresMotivacionSuenoOrigenFechaYNotaEnRegistroClinico()
    {
        var payload = new EmotionalAssessmentPayload(3, 8, 427, "wearable", new DateTime(2026, 11, 3, 12, 0, 0, DateTimeKind.Utc), " Me siento mejor. ");
        var text = EmotionalAssessmentNotes.Format(payload)!;
        Assert.Contains("Estrés: 3/10.", text);
        Assert.Contains("Motivación: 8/10.", text);
        Assert.Contains("427 minutos (wearable)", text);
        Assert.Contains("2026-11-03T12:00:00.0000000Z", text);
        Assert.EndsWith("Me siento mejor.", text);
        Assert.True(new CompleteTaskCommandValidator().Validate(Command(payload)).IsValid);
    }

    [Fact]
    public void SinDatosNuevosConservaContratoAnteriorSinInventarLecturas()
    {
        Assert.Null(EmotionalAssessmentNotes.Format(null));
        Assert.Null(EmotionalAssessmentNotes.Format(new()));
        Assert.True(new CompleteTaskCommandValidator().Validate(Command(null)).IsValid);
    }

    [Theory]
    [InlineData(0, 5, 400)]
    [InlineData(11, 5, 400)]
    [InlineData(5, 11, 400)]
    [InlineData(5, 5, -1)]
    [InlineData(5, 5, 1441)]
    public void RechazaRangosInvalidos(int stress, int motivation, int sleep) =>
        Assert.False(new CompleteTaskCommandValidator().Validate(Command(new(stress, motivation, sleep, "manual"))).IsValid);

    [Fact]
    public void WearableExigeFechaYNoSeAceptaEnOtraTarea()
    {
        var validator = new CompleteTaskCommandValidator();
        Assert.False(validator.Validate(Command(new(SleepMinutes: 420, SleepSource: "wearable"))).IsValid);
        Assert.False(validator.Validate(Command(new(SleepMinutes: 420, SleepSource: "otra"))).IsValid);
        Assert.False(validator.Validate(Command(new(Notes: new string('a', 2001)))).IsValid);
        Assert.False(validator.Validate(Command(new(StressScore: 3), TaskCode.nutraceutico)).IsValid);
    }

    [Fact]
    public void SuenoDesconocido_NoEsCeroYNoConservaProcedenciaFalsa()
    {
        var validator = new CompleteTaskCommandValidator();
        Assert.True(validator.Validate(Command(new(SleepSource: "unknown", SleepQuality: "fair"))).IsValid);
        Assert.False(validator.Validate(Command(new(SleepMinutes: 0, SleepSource: "unknown"))).IsValid);
        Assert.False(validator.Validate(Command(new(SleepMinutes: 420, SleepSource: "manual", SleepDeviceId: "ring"))).IsValid);
        Assert.False(validator.Validate(Command(new(SleepMinutes: 420, SleepSource: "manual", SleepRecordedAt: DateTime.UtcNow))).IsValid);
        Assert.False(validator.Validate(Command(new(SleepQuality: "diagnostico"))).IsValid);
        Assert.False(validator.Validate(Command(new(SleepMinutes: 420, SleepSource: "manual", SleepNightDate: new DateOnly(2026, 11, 2)))).IsValid);
    }
}
