using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>
/// Elimina lógicamente (soft-delete) el perfil de un empleado: desaparece del
/// directorio y su correo queda libre para un alta nueva. El perfil y su
/// historial se conservan y la cuenta de Auth no se toca (puede servir a
/// otros perfiles, p. ej. un paciente). La eliminación definitiva de la
/// cuenta se gestiona desde el módulo de Usuarios.
/// </summary>
public record DeleteEmployeeCommand(Guid Id) : IRequest<bool>;

public sealed class DeleteEmployeeCommandHandler(IEmployeeRepository repository)
    : IRequestHandler<DeleteEmployeeCommand, bool>
{
    public async Task<bool> Handle(DeleteEmployeeCommand request, CancellationToken ct)
    {
        // GetByIdAsync excluye los ya eliminados: un segundo DELETE responde 404.
        var employee = await repository.GetByIdAsync(request.Id, ct);
        if (employee is null)
        {
            return false;
        }

        return await repository.SoftDeleteAsync(request.Id, ct);
    }
}
