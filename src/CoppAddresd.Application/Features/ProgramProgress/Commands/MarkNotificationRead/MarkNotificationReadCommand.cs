using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.MarkNotificationRead;

/// <summary>
/// Marca una notificación gamificada del paciente como leída (SPEC §20, D):
/// <c>read_at = now</c>. El <c>patientId</c> SIEMPRE llega resuelto de la
/// identidad del JWT por la capa API (nunca del body); una notificación que no
/// pertenece al paciente devuelve 404 (anti-IDOR AC-11), sin distinguir si
/// existe.
/// </summary>
public sealed record MarkNotificationReadCommand(Guid NotificationId, Guid PatientId) : IRequest<bool>;

/// <summary>Validación de forma del payload (SPEC §20, D): ids requeridos.</summary>
public sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(x => x.NotificationId)
            .NotEmpty()
            .WithMessage("El id de la notificación es requerido.");

        RuleFor(x => x.PatientId)
            .NotEmpty()
            .WithMessage("El patientId es requerido.");
    }
}

public sealed class MarkNotificationReadCommandHandler(
    INotificationLogRepository repository) : IRequestHandler<MarkNotificationReadCommand, bool>
{
    public async Task<bool> Handle(MarkNotificationReadCommand request, CancellationToken ct)
        => await repository.MarkReadAsync(request.NotificationId, request.PatientId, ct);
}