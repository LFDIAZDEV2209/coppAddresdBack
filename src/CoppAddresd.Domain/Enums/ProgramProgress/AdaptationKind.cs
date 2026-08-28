namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Tipo de adaptación propuesta al programa. Los cambios de dificultad/nivel/
/// plantilla requieren aprobación clínica; los refrescos de contenido se
/// auto-aplican cuando la regla lo permite.
/// </summary>
public enum AdaptationKind
{
    DifficultyChange = 1,
    LevelChange = 2,
    TemplateSwap = 3,
    RoutineContentRefresh = 4,
    NutritionPlanRefresh = 5,
    MediaRotation = 6,
}