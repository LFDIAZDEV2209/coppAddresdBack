using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Elimina un paciente del directorio (soft delete: marca <c>deleted_at</c> y
/// conserva la trazabilidad PHI; nunca borrado físico).
/// </summary>
public record DeletePatientCommand(Guid Id, Guid? DeletedBy) : IRequest<bool>;

public sealed class DeletePatientCommandHandler(
    IPatientRepository repository) : IRequestHandler<DeletePatientCommand, bool>
{
    public async Task<bool> Handle(DeletePatientCommand request, CancellationToken ct)
    {
        var patient = await repository.GetByIdAsync(request.Id, ct);
        if (patient is null)
            return false;

        patient.UpdatedBy = request.DeletedBy;
        await repository.SoftDeleteAsync(patient, ct);
        return true;
    }
}