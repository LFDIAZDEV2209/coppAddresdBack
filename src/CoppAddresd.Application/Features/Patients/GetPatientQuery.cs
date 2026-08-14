using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>Obtiene el agregado completo de un paciente por Id.</summary>
public record GetPatientQuery(Guid Id) : IRequest<PatientDto?>;

public sealed class GetPatientQueryHandler(
    IPatientRepository repository) : IRequestHandler<GetPatientQuery, PatientDto?>
{
    public async Task<PatientDto?> Handle(GetPatientQuery request, CancellationToken ct)
    {
        var patient = await repository.GetByIdAsync(request.Id, ct);
        return patient is null ? null : PatientDto.FromEntity(patient);
    }
}