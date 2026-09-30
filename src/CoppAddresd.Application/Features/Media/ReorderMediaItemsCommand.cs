using CoppAddresd.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.Media;

/// <summary>Entrada de reordenamiento del catálogo: id del medio y su nueva posición.</summary>
public sealed record ReorderMediaItemEntry(Guid Id, int SortOrder);

/// <summary>
/// Reordenamiento en lote de <c>SortOrder</c> (REQ-PCA-03): una sola escritura
/// atómica del catálogo. Los ids inexistentes se ignoran (el front pudo leer
/// un catálogo que cambió concurrentemente). Devuelve la cantidad de filas
/// actualizadas.
/// </summary>
public sealed record ReorderMediaItemsCommand(IReadOnlyList<ReorderMediaItemEntry> Items)
    : IRequest<ReorderMediaItemsResult>;

/// <summary>Resultado del reordenamiento en lote.</summary>
public sealed record ReorderMediaItemsResult(int UpdatedCount);

/// <summary>Validación de input de <see cref="ReorderMediaItemsCommand"/>.</summary>
public sealed class ReorderMediaItemsCommandValidator : AbstractValidator<ReorderMediaItemsCommand>
{
    public ReorderMediaItemsCommandValidator()
    {
        RuleFor(x => x.Items)
            .NotNull()
            .WithMessage("La lista de reordenamiento es requerida.")
            .Must(items => items is { Count: > 0 })
            .WithMessage("Debe enviarse al menos una entrada de reordenamiento.")
            .Must(items =>
                items == null || items.Select(i => i.Id).Distinct().Count() == items.Count
            )
            .WithMessage("No puede repetirse el mismo medio en la lista de reordenamiento.");

        RuleForEach(x => x.Items)
            .ChildRules(entry =>
                entry
                    .RuleFor(e => e.Id)
                    .NotEmpty()
                    .WithMessage("Cada entrada requiere el id del medio.")
            );
    }
}

public sealed class ReorderMediaItemsCommandHandler(
    IMediaItemRepository repository,
    ILogger<ReorderMediaItemsCommandHandler> logger
) : IRequestHandler<ReorderMediaItemsCommand, ReorderMediaItemsResult>
{
    public async Task<ReorderMediaItemsResult> Handle(
        ReorderMediaItemsCommand request,
        CancellationToken ct
    )
    {
        var updated = await repository.ReorderAsync(request.Items, ct);

        logger.LogInformation(
            "Media reordered: {Requested} entradas, {Updated} filas actualizadas",
            request.Items.Count,
            updated
        );

        return new ReorderMediaItemsResult(updated);
    }
}
