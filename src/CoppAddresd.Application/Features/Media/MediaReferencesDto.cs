using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.Media;

/// <summary>Referencia de un medio en una fila de plantilla (WeeklyDayTemplate).</summary>
public sealed record MediaTemplateReferenceDto(
    Guid TemplateId,
    string TemplateName,
    short Weekday,
    int Points
);

/// <summary>Referencia de un medio en el snapshot de una semana de paciente.</summary>
public sealed record MediaEnrollmentReferenceDto(
    Guid EnrollmentId,
    string? PatientName,
    int WeekNumber,
    short Weekday,
    bool IsFrozen
);

/// <summary>
/// "Dónde se usa" un medio (REQ-PCA-07): plantillas y semanas de pacientes
/// que lo referencian. <see cref="TotalReferences"/> es el total consolidado
/// (plantillas + semanas); el guardia de eliminación lo usa para decidir el
/// 409 Conflict.
/// </summary>
public sealed record MediaReferencesDto(
    Guid MediaId,
    int TotalReferences,
    IReadOnlyList<MediaTemplateReferenceDto> TemplateReferences,
    IReadOnlyList<MediaEnrollmentReferenceDto> EnrollmentReferences
)
{
    public bool HasReferences => TotalReferences > 0;
}

/// <summary>Obtiene las referencias activas de un medio. Devuelve null si el medio no existe.</summary>
public record GetMediaReferencesQuery(Guid MediaId) : IRequest<MediaReferencesDto?>;

public sealed class GetMediaReferencesQueryHandler(IMediaItemRepository repository)
    : IRequestHandler<GetMediaReferencesQuery, MediaReferencesDto?>
{
    public async Task<MediaReferencesDto?> Handle(
        GetMediaReferencesQuery request,
        CancellationToken ct
    ) => await repository.GetReferencesAsync(request.MediaId, ct);
}
