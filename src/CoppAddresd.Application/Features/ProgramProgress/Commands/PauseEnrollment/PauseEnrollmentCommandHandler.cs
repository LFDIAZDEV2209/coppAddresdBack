using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.PauseEnrollment;

/// <summary>
/// Pausa la inscripción (Active → Paused). El repositorio valida el estado
/// (409 <c>INVALID_ENROLLMENT_STATE</c> si no está activa) y registra
/// <c>paused_at</c>/<c>updated_by</c>. El motivo se conserva en el log (la
/// tabla no tiene columna de razón en MVP).
/// </summary>
public sealed class PauseEnrollmentCommandHandler(
    IProgramRepository repository,
    ILogger<PauseEnrollmentCommandHandler> logger) : IRequestHandler<PauseEnrollmentCommand, ProgramEnrollmentDto>
{
    public async Task<ProgramEnrollmentDto> Handle(PauseEnrollmentCommand request, CancellationToken ct)
    {
        var enrollment = await repository.PauseAsync(request.EnrollmentId, request.ActorId, ct);

        var dto = await repository.GetEnrollmentAsync(enrollment.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la inscripción pausada.");

        logger.LogInformation(
            "Program.PauseEnrollment: enrollment={EnrollmentId} motivo={Reason}",
            enrollment.Id, request.Reason);

        return dto;
    }
}