using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.ResumeEnrollment;

/// <summary>
/// Reanuda la inscripción (Paused → Active). El repositorio valida el estado
/// (409 <c>INVALID_ENROLLMENT_STATE</c> si no está pausada) y limpia
/// <c>paused_at</c>.
/// </summary>
public sealed class ResumeEnrollmentCommandHandler(
    IProgramRepository repository,
    ILogger<ResumeEnrollmentCommandHandler> logger) : IRequestHandler<ResumeEnrollmentCommand, ProgramEnrollmentDto>
{
    public async Task<ProgramEnrollmentDto> Handle(ResumeEnrollmentCommand request, CancellationToken ct)
    {
        var enrollment = await repository.ResumeAsync(request.EnrollmentId, request.ActorId, ct);

        var dto = await repository.GetEnrollmentAsync(enrollment.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la inscripción reanudada.");

        logger.LogInformation(
            "Program.ResumeEnrollment: enrollment={EnrollmentId}",
            enrollment.Id);

        return dto;
    }
}