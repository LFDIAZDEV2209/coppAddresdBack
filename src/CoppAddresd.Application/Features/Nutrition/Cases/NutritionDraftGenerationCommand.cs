using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using MediatR;

namespace CoppAddresd.Application.Features.Nutrition.Cases;

public sealed record NutritionDraftGenerationResult(Guid ReservationId, string State, Guid? PrescriptionId);
public sealed record NutritionDraftGenerationCommand(Guid PatientId, NutritionActor Actor) : IRequest<NutritionDraftGenerationResult>;

/// <summary>Recuperación autorizada: reserva trabajo durable sin ejecutar generación en la petición.</summary>
public sealed class NutritionDraftGenerationCommandHandler(INutritionCaseStore access,
    IProgramAssignmentRepository assessments, INutritionDraftReservationStore reservations, TimeProvider clock)
    : IRequestHandler<NutritionDraftGenerationCommand, NutritionDraftGenerationResult>
{
    public async Task<NutritionDraftGenerationResult> Handle(NutritionDraftGenerationCommand request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.Actor.UserId == Guid.Empty || !request.Actor.CanManage)
            throw new ForbiddenException("Se requiere permiso Nutrition.Manage.");
        if (!await access.CanAccessAsync(request.PatientId, request.Actor, ct))
            throw new NotFoundException("Paciente no encontrado.");
        var context = await access.GetContextAsync(request.PatientId, ct);
        if (context?.BatteryId is not { } batteryId ||
            await assessments.FindCompletedInitialBatteryAsync(request.PatientId, batteryId, ct) is null)
            throw new UnprocessableEntityException("El paciente no tiene una batería inicial completa y correlacionable para generar el borrador.");
        var reservation = await reservations.RequestAsync(request.PatientId, batteryId, clock.GetUtcNow().UtcDateTime, ct);
        return new(reservation.Id, reservation.State, reservation.PrescriptionId);
    }
}
