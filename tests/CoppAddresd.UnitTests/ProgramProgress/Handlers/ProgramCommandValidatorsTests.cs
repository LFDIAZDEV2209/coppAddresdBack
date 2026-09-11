using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ArchiveTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.DecideAdaptation;
using CoppAddresd.Application.Features.ProgramProgress.Commands.LogNutrition;
using CoppAddresd.Application.Features.ProgramProgress.Commands.PauseEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.PublishTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ResumeEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.WithdrawEnrollment;
using CoppAddresd.Domain.Enums.ProgramProgress;

namespace CoppAddresd.UnitTests.ProgramProgress.Handlers;

/// <summary>
/// Tests de los validadores restantes de comandos (T-11): ids requeridos,
/// longitud de notas y decisión de adaptación válida. Complementan
/// <see cref="ProgramValidatorsTests"/> para cubrir todos los comandos.
/// </summary>
public class ProgramCommandValidatorsTests
{
    [Fact]
    public void PauseEnrollment_EnrollmentIdVacio_EsInvalido()
    {
        var validator = new PauseEnrollmentCommandValidator();
        var command = new PauseEnrollmentCommand(Guid.Empty, "motivo");

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void PauseEnrollment_NotaLarga_EsInvalido()
    {
        var validator = new PauseEnrollmentCommandValidator();
        var command = new PauseEnrollmentCommand(Guid.NewGuid(), new string('x', 501));

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void PauseEnrollment_Valido_EsValido()
    {
        var validator = new PauseEnrollmentCommandValidator();
        var command = new PauseEnrollmentCommand(Guid.NewGuid(), "vacaciones");

        Assert.True(validator.Validate(command).IsValid);
    }

    [Fact]
    public void ResumeEnrollment_EnrollmentIdVacio_EsInvalido()
    {
        var validator = new ResumeEnrollmentCommandValidator();

        Assert.False(validator.Validate(new ResumeEnrollmentCommand(Guid.Empty)).IsValid);
    }

    [Fact]
    public void WithdrawEnrollment_EnrollmentIdVacio_EsInvalido()
    {
        var validator = new WithdrawEnrollmentCommandValidator();

        Assert.False(validator.Validate(new WithdrawEnrollmentCommand(Guid.Empty)).IsValid);
    }

    [Fact]
    public void DecideAdaptation_DecisionInvalida_EsInvalido()
    {
        var validator = new DecideAdaptationCommandValidator();
        var command = new DecideAdaptationCommand(Guid.NewGuid(), (AdaptationDecisionAction)99);

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void DecideAdaptation_Valido_EsValido()
    {
        var validator = new DecideAdaptationCommandValidator();
        var command = new DecideAdaptationCommand(
            Guid.NewGuid(),
            AdaptationDecisionAction.Approve,
            "ok"
        );

        Assert.True(validator.Validate(command).IsValid);
    }

    [Fact]
    public void PublishTemplate_IdVacio_EsInvalido()
    {
        var validator = new PublishTemplateCommandValidator();

        Assert.False(validator.Validate(new PublishTemplateCommand(Guid.Empty)).IsValid);
    }

    [Fact]
    public void ArchiveTemplate_IdVacio_EsInvalido()
    {
        var validator = new ArchiveTemplateCommandValidator();

        Assert.False(validator.Validate(new ArchiveTemplateCommand(Guid.Empty)).IsValid);
    }

    [Fact]
    public void ReplaceEnrollmentWeekTasks_Validations_Work()
    {
        var validator =
            new CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceEnrollmentWeekTasks.ReplaceEnrollmentWeekTasksCommandValidator();

        // 1. EnrollmentId vacío -> Inválido
        var emptyId =
            new CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceEnrollmentWeekTasks.ReplaceEnrollmentWeekTasksCommand(
                Guid.Empty,
                1,
                [new WeeklyDayTemplateRequest(1, TaskCode.podcast, 80, 1)]
            );
        Assert.False(validator.Validate(emptyId).IsValid);

        // 2. WeekNumber < 1 -> Inválido
        var invalidWeek =
            new CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceEnrollmentWeekTasks.ReplaceEnrollmentWeekTasksCommand(
                Guid.NewGuid(),
                0,
                [new WeeklyDayTemplateRequest(1, TaskCode.podcast, 80, 1)]
            );
        Assert.False(validator.Validate(invalidWeek).IsValid);

        // 3. Tareas nulas o vacías -> Inválido
        var emptyTasks =
            new CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceEnrollmentWeekTasks.ReplaceEnrollmentWeekTasksCommand(
                Guid.NewGuid(),
                1,
                []
            );
        Assert.False(validator.Validate(emptyTasks).IsValid);

        // 4. Tareas duplicadas en mismo día -> Inválido
        var duplicateTasks =
            new CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceEnrollmentWeekTasks.ReplaceEnrollmentWeekTasksCommand(
                Guid.NewGuid(),
                1,
                [
                    new WeeklyDayTemplateRequest(1, TaskCode.podcast, 80, 1),
                    new WeeklyDayTemplateRequest(1, TaskCode.podcast, 80, 2),
                ]
            );
        Assert.False(validator.Validate(duplicateTasks).IsValid);

        // 5. Tareas válidas -> Válido
        var valid =
            new CoppAddresd.Application.Features.ProgramProgress.Commands.ReplaceEnrollmentWeekTasks.ReplaceEnrollmentWeekTasksCommand(
                Guid.NewGuid(),
                1,
                [
                    new WeeklyDayTemplateRequest(1, TaskCode.podcast, 80, 1),
                    new WeeklyDayTemplateRequest(1, TaskCode.vitals, 120, 2),
                ]
            );
        Assert.True(validator.Validate(valid).IsValid);
    }

    [Fact]
    public void UpdateNutritionIntake_PatientIdVacio_EsInvalido()
    {
        var validator = new UpdateNutritionIntakeCommandValidator();
        var command = new UpdateNutritionIntakeCommand(Guid.Empty, MealCode.alm);

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void UpdateNutritionIntake_MealCodeInvalido_EsInvalido()
    {
        var validator = new UpdateNutritionIntakeCommandValidator();
        var command = new UpdateNutritionIntakeCommand(Guid.NewGuid(), (MealCode)99);

        Assert.False(validator.Validate(command).IsValid);
    }

    [Fact]
    public void UpdateNutritionIntake_Valido_EsValido()
    {
        var validator = new UpdateNutritionIntakeCommandValidator();
        var command = new UpdateNutritionIntakeCommand(Guid.NewGuid(), MealCode.alm);

        Assert.True(validator.Validate(command).IsValid);
    }
}
