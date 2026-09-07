using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Professionals;

/// <summary>Horarios semanales de atención de un profesional.</summary>
public record GetProfessionalSchedulesQuery(Guid ProfessionalId)
    : IRequest<IReadOnlyList<ProfessionalScheduleDto>>;

public sealed class GetProfessionalSchedulesQueryHandler(
    IEmployeeRepository repository
) : IRequestHandler<GetProfessionalSchedulesQuery, IReadOnlyList<ProfessionalScheduleDto>>
{
    public async Task<IReadOnlyList<ProfessionalScheduleDto>> Handle(
        GetProfessionalSchedulesQuery request,
        CancellationToken ct
    )
    {
        // Verificar que el profesional existe.
        var employee = await repository.GetByProfessionalIdAsync(request.ProfessionalId, ct);
        if (employee is null || employee.Professional is null)
            return [];

        var schedules = await repository.GetSchedulesByProfessionalIdAsync(
            request.ProfessionalId,
            ct
        );

        return schedules.Select(ProfessionalScheduleDto.FromEntity).ToList();
    }
}
