using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.CompleteTask;

/// <summary>
/// Comando para completar una tarea del programa (SPEC §6.2 y §7.2). El
/// <c>clientRequestId</c> es la clave de reintento del móvil (retries
/// idempotentes): el repositorio garantiza XP exactamente una vez.
/// </summary>
public sealed record CompleteTaskCommand(
    Guid EnrollmentId,
    DateOnly LocalDate,
    TaskCode TaskCode,
    string? ClientRequestId,
    DateTime? ClientCompletedAt,
    short? MoodScore,
    string? Barriers,
    string? ContentFingerprint,
    Guid? ActorId = null) : IRequest<CompleteTaskResponseDto>;

/// <summary>
/// Validación de input de <see cref="CompleteTaskCommand"/> (T-11). Reglas de
/// negocio (semana activa, tarea programada, estado de la inscripción, clave
/// reutilizada) viven en el repositorio; aquí solo forma del payload. El
/// chequeo "due today" NO se hace aquí: una guardia contra
/// <c>DateTime.UtcNow</c> rechazaría fechas legítimas de pacientes en zonas
/// UTC+ (hoy local = mañana en UTC). El handler es la autoridad y valida
/// contra el "hoy" del paciente en su zona IANA (SPEC §6.11).
/// </summary>
public sealed class CompleteTaskCommandValidator : AbstractValidator<CompleteTaskCommand>
{
    public CompleteTaskCommandValidator()
    {
        RuleFor(x => x.EnrollmentId)
            .NotEmpty()
            .WithMessage("El enrollmentId es requerido.");

        RuleFor(x => x.LocalDate)
            .NotEmpty()
            .WithMessage("La fecha local es requerida.");

        RuleFor(x => x.TaskCode)
            .IsInEnum()
            .WithMessage("Código de tarea inválido.");

        RuleFor(x => x.ClientRequestId)
            .MaximumLength(64)
            .WithMessage("clientRequestId no puede superar 64 caracteres.");

        RuleFor(x => x.MoodScore)
            .InclusiveBetween((short)1, (short)5)
            .When(x => x.MoodScore.HasValue)
            .WithMessage("moodScore debe estar entre 1 y 5.");

        RuleFor(x => x.Barriers)
            .MaximumLength(40)
            .WithMessage("barriers no puede superar 40 caracteres.");

        RuleFor(x => x.ContentFingerprint)
            .MaximumLength(64)
            .WithMessage("contentFingerprint no puede superar 64 caracteres.");

        // La tarea emocional exige un moodScore real (1..5): el cliente debe
        // enviarlo y el handler lo rechaza si falta (SPEC §3.10, §6.2).
        RuleFor(x => x.MoodScore)
            .NotNull()
            .WithMessage("La tarea emocional requiere moodScore (1..5).")
            .When(x => x.TaskCode == TaskCode.emocional);
    }
}