using CoppAddresd.Application.Features.Nutrition.Engine;
using CoppAddresd.Domain.Entities;
using MediatR;

namespace CoppAddresd.Application.Features.Nutrition.Cases;

public sealed record NutritionActor(Guid UserId, Guid? ProfessionalId, bool ViewAll, bool CanManage, bool CanApprove);
public sealed record NutritionCaseContext(DateOnly? BirthDate, string? Sex, string Timezone, bool TestsComplete, Guid? BatteryId);
public enum NutritionOperation { Generate, Save, ApplyDay, CreateVersion, Submit, Approve, Return, Regenerate, Publish }
public sealed record NutritionGenerateInput(NutritionPatient? Manual = null, DateOnly? BirthDate = null, bool PreserveConfirmedEdits = true);
public sealed record NutritionItemEdit(string FoodItemId, double Grams, string? Notes = null);
public sealed record NutritionMealEdit(int DayNumber, string MealType, string? MenuName, IReadOnlyList<NutritionItemEdit> Items,
    string? Notes = null, int? WaterMl = null);
public sealed record NutritionEdit(NutritionTargets? Targets = null, IReadOnlyList<NutritionMealEdit>? Meals = null,
    string? Instructions = null, string? InstructionsEn = null, bool ConfirmManualEdits = false, int? SourceDay = null);
public sealed record NutritionCaseCommand(NutritionOperation Operation, Guid PatientId, Guid? Id, int? Revision,
    NutritionActor Actor, NutritionGenerateInput? Generate = null, NutritionEdit? Edit = null,
    string? Comment = null, bool CriticalConfirmed = false, DateOnly? StartDate = null,
    Guid? CorrelationId = null, Guid? BatteryAssignmentId = null) : IRequest<NutritionView>;
public sealed record GetNutritionCaseQuery(Guid PatientId, Guid Id, NutritionActor Actor) : IRequest<NutritionView>;
public sealed record ListNutritionCasesQuery(Guid PatientId, NutritionActor Actor, int Page = 1, int PageSize = 20) : IRequest<IReadOnlyList<NutritionView>>;
public sealed record NutritionHistoryQuery(Guid PatientId, Guid Id, NutritionActor Actor) : IRequest<IReadOnlyList<NutritionPrescriptionEvent>>;
public sealed record NutritionView(Guid Id, Guid PatientId, Guid CycleId, int Version, int Revision,
    string State, bool Provisional, bool IsPublished, bool MenusOutdated, bool ManualEditsConfirmed,
    DateOnly? StartDate, DateOnly? EndDate, NutritionTargets Targets,
    string? Instructions, string? InstructionsEn, string NarrativeSource, IReadOnlyList<string> MissingData,
    IReadOnlyList<NutritionAlert> Alerts, IReadOnlyList<NutritionMealEdit> Meals, bool CanRegenerate,
    Guid CreatedBy, Guid? ApprovedBy, DateTime? ApprovedAt, string Disclaimer = NutritionRecommendationNotice.Text);

public sealed record DraftText(string Es, string En);
public sealed record DraftFood(string FoodId, double Grams);
public sealed record DraftMenu(string Key, string Name, IReadOnlyList<DraftFood> Items);
public sealed record DraftTargets(double K, double C, double P, double F, double Fi, int WaterMl);
public sealed record DraftMeasuredField(object? Value, string? Unit, string Source, string? SourceCode, DateTime? ObservedAt);
public sealed record DraftAppliedRule(string Code, string Severity, string Message, bool Technical);
public sealed record DraftClinicalProfile(string EngineVersion, IReadOnlyDictionary<string, DraftMeasuredField> Fields,
    IReadOnlyList<DraftAppliedRule> AppliedRules, IReadOnlyList<string> MissingData);
public sealed record NutritionDraftRequest(DraftTargets Targets, IReadOnlyList<string> CatalogIds, IReadOnlyList<DraftMenu> Menus,
    DraftClinicalProfile? ClinicalProfile = null)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public Guid? SubjectPatientId { get; init; }
}
public sealed record NutritionDraftResponse(DraftText Narratives, DraftText Instructions, IReadOnlyList<DraftMenu> Menus, string Source);
