using CoppAddresd.Application.DTOs.ProgramProgress;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.ArchiveTemplate;

/// <summary>
/// Comando para archivar una plantilla (SPEC §7.6): transiciona a
/// <c>Archived</c> (inactiva, conserva historia). Idempotente: archivar una ya
/// archivada devuelve el estado actual sin error.
/// </summary>
public sealed record ArchiveTemplateCommand(
    Guid Id,
    Guid? ActorId = null) : IRequest<ProgramTemplateDto>;

/// <summary>Validación de input de <see cref="ArchiveTemplateCommand"/> (T-11).</summary>
public sealed class ArchiveTemplateCommandValidator : AbstractValidator<ArchiveTemplateCommand>
{
    public ArchiveTemplateCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El id de la plantilla es requerido.");
    }
}