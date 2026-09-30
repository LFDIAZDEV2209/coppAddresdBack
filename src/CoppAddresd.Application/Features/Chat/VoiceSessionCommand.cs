using CoppAddresd.Application.DTOs.Ai;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.Chat;

/// <summary>
/// Comando de apertura de sesión de voz (ElevenLabs Agents). El backend
/// valida identidad/estado/rate limit/auditoría y el ai-service emite el
/// signed URL temporal. El <c>UserId</c> proviene del JWT en el controller,
/// nunca del body del cliente.
/// </summary>
public record VoiceSessionCommand(string UserId, string? PatientId, string? ThreadId)
    : IRequest<VoiceSessionResponseDto>;

/// <summary>
/// Validador de la sesión de voz: usuario requerido y thread acotado. Los
/// ids internos son cadenas cortas; cualquier payload raro se rechaza aquí.
/// </summary>
public sealed class VoiceSessionCommandValidator : AbstractValidator<VoiceSessionCommand>
{
    public VoiceSessionCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Usuario no identificado.");
        RuleFor(x => x.ThreadId)
            .MaximumLength(200)
            .WithMessage("ThreadId no debe superar 200 caracteres.");
        RuleFor(x => x.PatientId)
            .MaximumLength(64)
            .WithMessage("PatientId no debe superar 64 caracteres.");
    }
}
