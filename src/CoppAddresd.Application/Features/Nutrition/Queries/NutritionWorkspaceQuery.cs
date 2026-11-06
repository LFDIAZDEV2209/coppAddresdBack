using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.Nutrition.Queries;

public sealed record NutritionGenerationStatus(Guid ReservationId, Guid BatteryAssignmentId, string State, string? Reason,
    int Attempts, bool Fallback, Guid? PrescriptionId, DateTime UpdatedAt);
public sealed record NutritionWorkspaceView(Guid PatientId, string PatientName, string? PatientDocument, string Timezone,
    DateOnly Today, NutritionMeasuredProfile Profile, IReadOnlyList<string> MissingData, NutritionGenerationStatus? Generation,
    NutritionView? ToReview, NutritionView? CurrentPublished, NutritionHistoryPage History);
public sealed record NutritionWorkspaceQuery(Guid PatientId, NutritionActor Actor, int HistoryPage = 1,
    int HistoryPageSize = 20, Guid? PrescriptionId = null) : IRequest<NutritionWorkspaceView>;

public sealed class NutritionWorkspaceQueryHandler(INutritionCaseStore access, INutritionWorkspaceRepository workspace,
    INutritionPrescriptionRepository prescriptions, NutritionCases cases, TimeProvider clock)
    : IRequestHandler<NutritionWorkspaceQuery, NutritionWorkspaceView>
{
    public async Task<NutritionWorkspaceView> Handle(NutritionWorkspaceQuery request, CancellationToken ct)
    {
        if (request.Actor.UserId == Guid.Empty || !request.Actor.CanManage && !request.Actor.CanApprove) throw new ForbiddenException("Se requiere permiso de Nutrición.");
        if (!await access.CanAccessAsync(request.PatientId, request.Actor, ct)) throw new NotFoundException("Paciente no encontrado.");
        if (request.HistoryPage < 1 || request.HistoryPageSize is < 1 or > 100 || (long)(request.HistoryPage - 1) * request.HistoryPageSize > int.MaxValue)
            throw new ValidationException("Paginación de historial inválida.");
        var identity = await workspace.IdentityAsync(request.PatientId, ct) ?? throw new NotFoundException("Paciente no encontrado.");
        var context = await access.GetContextAsync(request.PatientId, ct) ?? throw new NotFoundException("Paciente no encontrado.");
        var today = NutritionStateMachine.Today(clock.GetUtcNow(), identity.Timezone);
        var profile = await cases.ReadMeasuredProfileAsync(request.PatientId, context, ct);
        var id = request.PrescriptionId ?? await workspace.LatestPrescriptionIdAsync(request.PatientId, ct);
        var review = id is { } key ? await prescriptions.GetByIdAsync(key, ct) : null;
        if (request.PrescriptionId is not null && review?.PatientId != request.PatientId) throw new NotFoundException("Prescripción no encontrada.");
        var current = await prescriptions.GetCurrentPublishedAsync(request.PatientId, today, ct);
        NutritionView? reviewView = review is null ? null : await cases.ReadViewAsync(review, context, ct);
        var currentView = current is null ? null : current.Id == review?.Id ? reviewView : await cases.ReadViewAsync(current, context, ct);
        var generation = await workspace.GenerationAsync(request.PatientId, ct);
        return new(request.PatientId, identity.PatientName, identity.Document, identity.Timezone, today, profile, profile.MissingData,
            Status(generation), reviewView, currentView, await workspace.HistoryAsync(request.PatientId, request.HistoryPage, request.HistoryPageSize, ct));
    }

    public static NutritionGenerationStatus? Status(NutritionDraftReservation? r) => r is null ? null
        : new(r.Id, r.BatteryAssignmentId, r.State, r.Reason, r.Attempts, r.Fallback, r.PrescriptionId, r.UpdatedAt);
}
