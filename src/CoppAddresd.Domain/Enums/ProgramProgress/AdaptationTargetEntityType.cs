namespace CoppAddresd.Domain.Enums.ProgramProgress;

/// <summary>
/// Tipo de entidad objetivo de una recomendación de adaptación. Se persiste
/// en snake_case (<c>weekly_day_templates</c>, <c>program_enrollments</c>,
/// <c>nutrition_plans</c>, <c>exercise_routines</c>, <c>media_progressions</c>)
/// vía el conversor de la configuración EF.
/// </summary>
public enum AdaptationTargetEntityType
{
    WeeklyDayTemplates = 1,
    ProgramEnrollments = 2,
    NutritionPlans = 3,
    ExerciseRoutines = 4,
    MediaProgressions = 5,
}