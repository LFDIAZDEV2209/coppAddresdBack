using CoppAddresd.Auth.Constants;

namespace CoppAddresd.Auth.Seeders;

/// <summary>Traducción de ARQUITECTURA §10.1 a capacidades; no concede alcance global.</summary>
public static class WellbeingRolePermissions
{
    public static IReadOnlyList<string> ManagedCodes { get; } =
    [
        "Program.Profile360View", "Program.Edit", "Program.Enroll", "Program.Adapt",
        "Program.View", "Program.AiView", "Program.AiManage",
        PermissionCodes.MediaView, PermissionCodes.MediaCreate, PermissionCodes.MediaEdit,
        PermissionCodes.MediaPublish, PermissionCodes.MediaArchive,
        PermissionCodes.WellnessView, PermissionCodes.WellnessManage,
        "Nutrition.View", PermissionCodes.NutritionManage, PermissionCodes.NutritionApprove,
        PermissionCodes.NutritionPlansApprove, "Nutrition.RecordsView", "Nutrition.PhotosView",
        "Nutrition.NotesView", "Nutrition.NotesManage", "Nutrition.ReviewManage",
    ];

    public static IReadOnlyList<string> ForRole(string role) => role switch
    {
        "Admin" => ManagedCodes.Where(c => c != "Nutrition.NotesManage").ToArray(),
        "ClinicalDirector" =>
        [
            "Program.Profile360View", "Program.Edit", "Program.Enroll", "Program.Adapt",
            "Program.AiView", "Program.AiManage",
            PermissionCodes.MediaView, PermissionCodes.MediaCreate, PermissionCodes.MediaEdit,
            PermissionCodes.MediaPublish, PermissionCodes.MediaArchive,
            PermissionCodes.WellnessView, PermissionCodes.WellnessManage,
            "Nutrition.View", PermissionCodes.NutritionManage, PermissionCodes.NutritionApprove,
            PermissionCodes.NutritionPlansApprove, "Nutrition.RecordsView", "Nutrition.PhotosView",
            "Nutrition.NotesView", "Nutrition.ReviewManage",
        ],
        // NutritionReviewer es el rol funcional vigente del nutricionista (Nutritionist es legado).
        "NutritionReviewer" =>
        [
            "Program.Profile360View", "Program.AiView", PermissionCodes.MediaView, PermissionCodes.WellnessView,
            "Nutrition.View", PermissionCodes.NutritionManage, PermissionCodes.NutritionApprove,
            PermissionCodes.NutritionPlansApprove, "Nutrition.RecordsView", "Nutrition.PhotosView",
            "Nutrition.NotesView", "Nutrition.NotesManage", "Nutrition.ReviewManage",
        ],
        "CareCoordinator" =>
        [
            "Program.Profile360View", PermissionCodes.MediaView, PermissionCodes.WellnessView,
            "Nutrition.View", "Nutrition.RecordsView",
        ],
        _ => [],
    };
}
