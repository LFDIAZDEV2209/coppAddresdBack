using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask;
using CoppAddresd.Application.Features.ProgramProgress.Commands.CreateTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.EnrollPatient;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceWeekdayTasks;
using CoppAddresd.Application.Features.ProgramProgress.Queries.GetCalendar;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Tests de los validadores FluentValidation (T-11): forma del payload, sin
/// reglas de negocio. Cubren los casos del contrato: zona IANA inválida,
/// <c>moodScore</c> fuera de [1,5], emocional sin
/// <c>moodScore</c>, <c>clientRequestId</c> &gt; 64 y ventana del calendario
/// &gt; 92 días. Las fechas "futuras" en UTC son válidas a nivel validador
/// (B4: la autoridad del chequeo "due today" es el handler, zona del paciente).
/// </summary>
public class ProgramValidatorsTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    // ------------------------------------------------------------ CompleteTask

    [Fact]
    public void CompleteTask_Valido_EsValido()
    {
        var validator = new CompleteTaskCommandValidator();
        var command = new CompleteTaskCommand(
            Guid.NewGuid(), Today, TaskCode.podcast, "abc-123", null, null, null, null);

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CompleteTask_ClientRequestIdLargo_EsInvalido()
    {
        // T-11: clientRequestId ≤ 64.
        var validator = new CompleteTaskCommandValidator();
        var command = new CompleteTaskCommand(
            Guid.NewGuid(), Today, TaskCode.podcast, new string('x', 100), null, null, null, null);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CompleteTaskCommand.ClientRequestId));
    }

    [Fact]
    public void CompleteTask_FechaFuturaEnUTC_EsValidoEnElPipeline()
    {
        // B4: el validador YA NO rechaza fechas futuras en UTC — una fecha que
        // es "hoy" local para un paciente UTC+ es mañana en UTC. La autoridad
        // del chequeo "due today" es el handler, contra el hoy local del
        // paciente (SPEC §6.11). Aquí solo se valida la forma del payload.
        var validator = new CompleteTaskCommandValidator();
        var command = new CompleteTaskCommand(
            Guid.NewGuid(), Today.AddDays(1), TaskCode.podcast, null, null, null, null, null);

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void CompleteTask_MoodScoreFueraDeRango_EsInvalido(short moodScore)
    {
        var validator = new CompleteTaskCommandValidator();
        var command = new CompleteTaskCommand(
            Guid.NewGuid(), Today, TaskCode.emocional, null, null, moodScore, null, null);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CompleteTaskCommand.MoodScore));
    }

    [Fact]
    public void CompleteTask_EmocionalSinMoodScore_EsInvalido()
    {
        // La tarea emocional exige moodScore (1..5): el validador lo rechaza.
        var validator = new CompleteTaskCommandValidator();
        var command = new CompleteTaskCommand(
            Guid.NewGuid(), Today, TaskCode.emocional, null, null, MoodScore: null, null, null);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CompleteTaskCommand.MoodScore));
    }

    // ------------------------------------------------------------ EnrollPatient

    [Fact]
    public void EnrollPatient_ZonaIanaInvalida_EsInvalido()
    {
        var validator = new EnrollPatientCommandValidator();
        var command = new EnrollPatientCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Mars/Olympus", Today);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(EnrollPatientCommand.Timezone));
    }

    [Fact]
    public void EnrollPatient_ZonaIanaValida_EsValido()
    {
        var validator = new EnrollPatientCommandValidator();
        var command = new EnrollPatientCommand(
            Guid.NewGuid(), Guid.NewGuid(), "America/Bogota", Today);

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void EnrollPatient_StartLocalDateFuturaEnUTC_EsValidoEnElPipeline()
    {
        // B4: misma razón que CompleteTask — el validador no guarda contra
        // DateTime.UtcNow (rechazaría el hoy local de pacientes UTC+ y los
        // inicios futuros legítimos). El handler resuelve el default en zona
        // del paciente (SPEC §6.11).
        var validator = new EnrollPatientCommandValidator();
        var command = new EnrollPatientCommand(
            Guid.NewGuid(), Guid.NewGuid(), "America/Bogota", Today.AddDays(3));

        var result = validator.Validate(command);

        Assert.True(result.IsValid);
    }

    // ------------------------------------------------------------ Calendario

    [Fact]
    public void GetCalendar_VentanaMayorA92Dias_EsInvalido()
    {
        var validator = new GetCalendarQueryValidator();
        var command = new GetCalendarQuery(Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 5));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void GetCalendar_VentanaInvertida_EsInvalido()
    {
        var validator = new GetCalendarQueryValidator();
        var command = new GetCalendarQuery(Guid.NewGuid(), new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    // ------------------------------------------------------------ Plantillas

    [Fact]
    public void CreateTemplate_DuplicadoWeekdayTask_EsInvalido()
    {
        var validator = new CreateTemplateCommandValidator();
        var command = new CreateTemplateCommand(
            "tpl", "Plantilla", null, 83,
            [new WeeklyDayTemplateRequest(1, TaskCode.podcast, 80, 1),
             new WeeklyDayTemplateRequest(1, TaskCode.podcast, 90, 2)]);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void CreateTemplate_PuntosNegativos_EsInvalido()
    {
        var validator = new CreateTemplateCommandValidator();
        var command = new CreateTemplateCommand(
            "tpl", "Plantilla", null, 83,
            [new WeeklyDayTemplateRequest(1, TaskCode.podcast, -5, 1)]);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ReplaceWeekdayTasks_SinTareas_EsInvalido()
    {
        var validator = new ReplaceWeekdayTasksCommandValidator();
        var command = new ReplaceWeekdayTasksCommand(Guid.NewGuid(), []);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ReplaceWeekdayTasks_WeekdayFueraDeRango_EsInvalido()
    {
        var validator = new ReplaceWeekdayTasksCommandValidator();
        var command = new ReplaceWeekdayTasksCommand(
            Guid.NewGuid(), [new WeeklyDayTemplateRequest(8, TaskCode.podcast, 80, 1)]);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }
}