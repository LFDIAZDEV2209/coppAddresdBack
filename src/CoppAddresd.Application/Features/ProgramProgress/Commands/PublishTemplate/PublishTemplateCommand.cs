using CoppAddresd.Application.DTOs.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.PublishTemplate;

/// <summary>
/// Comando para publicar una plantilla (SPEC §7.6): incrementa la versión y
/// transiciona <c>Draft → Active</c> (fija <c>published_at</c>). Las semanas ya
/// activas conservan su snapshot (SPEC §4.5): la publicación afecta a las
/// próximas semanas, nunca reescribe las vigentes.
/// </summary>
public sealed record PublishTemplateCommand(
    Guid Id,
    Guid? ActorId = null) : IRequest<ProgramTemplateDto>;

/// <summary>Validación de input de <see cref="PublishTemplateCommand"/> (T-11).</summary>
public sealed class PublishTemplateCommandValidator : AbstractValidator<PublishTemplateCommand>
{
    public PublishTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El id de la plantilla es requerido.");
    }
}