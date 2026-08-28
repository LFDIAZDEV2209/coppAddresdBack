using CoppAddresd.Application.DTOs.ProgramProgress;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.GetProgramContent;

/// <summary>
/// Query para obtener el contenido de nutrición y ejercicio configurado por
/// semana para una inscripción (T-77). Consumido por el ERP para configurar
/// contenido por semana sin hacer cálculos de fecha.
/// </summary>
public sealed record GetProgramContentQuery(Guid EnrollmentId) : IRequest<ProgramContentResponse?>;
