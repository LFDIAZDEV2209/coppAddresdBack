using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Documents;

/// <summary>
/// Eliminación lógica del documento completo (raíz + todas sus versiones) con
/// actor. Devuelve <c>false</c> si el documento no existe.
/// </summary>
public record DeleteDocumentCommand(Guid Id, Guid? DeletedBy) : IRequest<bool>;

public sealed class DeleteDocumentCommandHandler(
    IDocumentRepository repository) : IRequestHandler<DeleteDocumentCommand, bool>
{
    public async Task<bool> Handle(DeleteDocumentCommand request, CancellationToken ct)
    {
        return await repository.SoftDeleteFamilyAsync(request.Id, request.DeletedBy, ct);
    }
}