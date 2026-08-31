using CoppAddresd.Application.Features.ProgramProgress.Commands.ArchiveTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.DecideAdaptation;
using CoppAddresd.Application.Features.ProgramProgress.Commands.PauseEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.PublishTemplate;
using CoppAddresd.Application.Features.ProgramProgress.Commands.ResumeEnrollment;
using CoppAddresd.Application.Features.ProgramProgress.Commands.WithdrawEnrollment;
using CoppAddresd.Application.DTOs.ProgramProgress;

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
        var command = new DecideAdaptationCommand(Guid.NewGuid(), AdaptationDecisionAction.Approve, "ok");

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
}