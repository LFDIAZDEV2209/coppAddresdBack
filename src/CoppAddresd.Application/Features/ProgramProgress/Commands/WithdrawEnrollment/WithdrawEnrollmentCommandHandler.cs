using CoppAddresd.Application.DTOs.ProgramProgress;
using CoppAddresd.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.WithdrawEnrollment;

/// <summary>
/// Retira la inscripción (terminal). El repositorio valida el estado (409 si
/// ya está retirada o completada) y registra <c>withdrawn_at</c>. El historial
/// (XP, racha, semanas) queda intacto para auditoría (SPEC §4.5).
/// </summary>
public sealed class WithdrawEnrollmentCommandHandler(
    IProgramRepository repository,
    ILogger<WithdrawEnrollmentCommandHandler> logger) : IRequestHandler<WithdrawEnrollmentCommand, ProgramEnrollmentDto>
{
    public async Task<ProgramEnrollmentDto> Handle(WithdrawEnrollmentCommand request, CancellationToken ct)
    {
        var enrollment = await repository.WithdrawAsync(request.EnrollmentId, request.ActorId, ct);

        var dto = await repository.GetEnrollmentAsync(enrollment.Id, ct)
            ?? throw new InvalidOperationException("No se pudo leer la inscripción retirada.");

        logger.LogInformation(
            "Program.WithdrawEnrollment: enrollment={EnrollmentId} motivo={Reason}",
            enrollment.Id, request.Reason);

        return dto;
    }
}