using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>Elimina un paciente del directorio.</summary>
public record DeletePatientCommand(Guid Id) : IRequest<bool>;

public sealed class DeletePatientCommandHandler(
    IPatientRepository repository) : IRequestHandler<DeletePatientCommand, bool>
{
    public async Task<bool> Handle(DeletePatientCommand request, CancellationToken ct)
    {
        var patient = await repository.GetByIdAsync(request.Id, ct);
        if (patient is null)
            return false;

        await repository.DeleteAsync(patient, ct);
        return true;
    }
}