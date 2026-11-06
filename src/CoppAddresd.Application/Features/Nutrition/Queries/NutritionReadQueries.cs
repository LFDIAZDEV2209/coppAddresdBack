using System.Text.Json;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace CoppAddresd.Application.Features.Nutrition.Queries;

public sealed record NutritionInboxPrescription(Guid Id, int Version, int Revision, string State,
    bool Provisional, double? K, DateOnly? StartDate, DateOnly? EndDate, bool IsPublished, int CriticalAlertCount);
public sealed record NutritionInboxItem(Guid PatientId, string PatientName, string State, NutritionInboxPrescription? Prescription,
    string? GenerationState = null, bool GenerationFallback = false, string? GenerationReason = null, DateTime? GenerationUpdatedAt = null);
public sealed record NutritionInboxPage(IReadOnlyList<NutritionInboxItem> Items, int Total, int Page, int PageSize);
public sealed record NutritionInboxQuery(NutritionActor Actor, int Page = 1, int PageSize = 20,
    string? State = null, string? Search = null) : IRequest<NutritionInboxPage>;

internal static class NutritionReadAccess
{
    public static void Ensure(NutritionActor actor, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (actor.UserId == Guid.Empty || !actor.CanManage && !actor.CanApprove)
            throw new ForbiddenException("Se requiere permiso de Nutrición.");
    }
}

public sealed class NutritionInboxQueryHandler(INutritionInboxRepository repository, TimeProvider clock)
    : IRequestHandler<NutritionInboxQuery, NutritionInboxPage>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<NutritionInboxPage> Handle(NutritionInboxQuery request, CancellationToken ct)
    {
        NutritionReadAccess.Ensure(request.Actor, ct);
        if (request.Page < 1 || request.PageSize is < 1 or > 100)
            throw new ValidationException("page debe ser positivo y pageSize debe estar entre 1 y 100.");
        if (request.State is not null and not ("sin_plan" or "borrador_ia" or "en_aprobacion" or "aprobado" or "devuelto" or "por_renovar"))
            throw new ValidationException("Estado nutricional inválido.");
        if (request.Search?.Length > 200) throw new ValidationException("search admite hasta 200 caracteres.");
        var result = await repository.ListAsync(request.Actor, request.Page, request.PageSize, request.State,
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(), clock.GetUtcNow().UtcDateTime, ct);
        var items = result.Items.Select(row =>
        {
            ct.ThrowIfCancellationRequested();
            // Las críticas clínicas no necesitan cargar comidas; los avisos técnicos nunca son críticos.
            var prescription = row.PrescriptionId is { } id ? new NutritionInboxPrescription(id,
                row.Version!.Value, row.Revision!.Value, row.State, row.Provisional, row.K,
                row.StartDate, row.EndDate, row.IsPublished,
                NutritionEngine.ClinicalFlags(JsonSerializer.Deserialize<NutritionPatient>(row.ClinicalProfileJson!, Json)!,
                    JsonSerializer.Deserialize<NutritionTargets>(row.TargetsJson!, Json)!).Count(x => x.S == "crit")) : null;
            return new NutritionInboxItem(row.PatientId, row.PatientName, row.State, prescription,
                row.GenerationState, row.GenerationFallback, row.GenerationReason, row.GenerationUpdatedAt);
        }).ToArray();
        return new(items, result.Total, request.Page, request.PageSize);
    }
}
