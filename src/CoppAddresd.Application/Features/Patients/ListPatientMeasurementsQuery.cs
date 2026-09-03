using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Patients;

/// <summary>
/// Lista plana de mediciones clínicas de un paciente para el panel ERP. El
/// controlador ya resolvió el alcance y la accesibilidad del paciente
/// (CanAccessPatientAsync consulta el paciente), por lo que este query es thin
/// y NO re-verifica existencia (mismo patrón que <see cref="GetPatientQuery"/>):
/// sin mediciones devuelve lista vacía → 200 [].
/// </summary>
public record ListPatientMeasurementsQuery(Guid PatientId)
    : IRequest<IReadOnlyList<PatientMeasurementDto>>;

public sealed class ListPatientMeasurementsQueryHandler(
    IClinicalMeasurementRepository repository
) : IRequestHandler<ListPatientMeasurementsQuery, IReadOnlyList<PatientMeasurementDto>>
{
    public async Task<IReadOnlyList<PatientMeasurementDto>> Handle(
        ListPatientMeasurementsQuery request,
        CancellationToken ct
    ) => await repository.ListForErpAsync(request.PatientId, ct);
}