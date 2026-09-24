using CoppAddresd.Application.DTOs.Ai;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.Chat;

/// <summary>
/// Comando de retroalimentación del chat IA (Fase 9). El <c>UserId</c>
/// proviene del JWT en el controller, nunca del body del cliente.
/// </summary>
public record SendChatFeedbackCommand(
    string ExecutionId,
    string ThreadId,
    int Rating,
    string? Comment,
    string? UserId
) : IRequest<ChatFeedbackResponseDto>;

/// <summary>
/// Validador del feedback: execution/thread requeridos, rating 1-5 y
/// comentario acotado (el ai-service admite máx. 2000).
/// </summary>
public sealed class SendChatFeedbackCommandValidator : AbstractValidator<SendChatFeedbackCommand>
{
    public SendChatFeedbackCommandValidator()
    {
        RuleFor(x => x.ExecutionId).NotEmpty().WithMessage("ExecutionId es requerido.");
        RuleFor(x => x.ThreadId).NotEmpty().WithMessage("ThreadId es requerido.");
        RuleFor(x => x.Rating).InclusiveBetween(1, 5).WithMessage("Rating debe estar entre 1 y 5.");
        RuleFor(x => x.Comment)
            .MaximumLength(2000)
            .WithMessage("Comment no debe superar 2000 caracteres.");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("Usuario no identificado.");
    }
}
