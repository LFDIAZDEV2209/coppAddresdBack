using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Configuración de recomendaciones de adaptación en el schema <c>app</c>.</summary>
public sealed class AdaptationRecommendationConfiguration : IEntityTypeConfiguration<AdaptationRecommendation>
{
    // target_entity_type se persiste en snake_case (valores que consume el ERP):
    // WeeklyDayTemplates → "weekly_day_templates", etc. Los lambdas envuelven
    // llamadas a métodos estáticos (los switch no caben en expression trees).
    private static readonly ValueConverter<AdaptationTargetEntityType, string> TargetEntityTypeConverter = new(
        v => ToStore(v),
        v => FromStore(v));

    private static string ToStore(AdaptationTargetEntityType value) => value switch
    {
        AdaptationTargetEntityType.WeeklyDayTemplates => "weekly_day_templates",
        AdaptationTargetEntityType.ProgramEnrollments => "program_enrollments",
        AdaptationTargetEntityType.NutritionPlans => "nutrition_plans",
        AdaptationTargetEntityType.ExerciseRoutines => "exercise_routines",
        AdaptationTargetEntityType.MediaProgressions => "media_progressions",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    private static AdaptationTargetEntityType FromStore(string value) => value switch
    {
        "weekly_day_templates" => AdaptationTargetEntityType.WeeklyDayTemplates,
        "program_enrollments" => AdaptationTargetEntityType.ProgramEnrollments,
        "nutrition_plans" => AdaptationTargetEntityType.NutritionPlans,
        "exercise_routines" => AdaptationTargetEntityType.ExerciseRoutines,
        "media_progressions" => AdaptationTargetEntityType.MediaProgressions,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null),
    };

    public void Configure(EntityTypeBuilder<AdaptationRecommendation> builder)
    {
        builder.ToTable("adaptation_recommendations", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.EnrollmentId)
            .HasColumnName("enrollment_id");

        builder.Property(x => x.Kind)
            .HasColumnName("kind")
            .HasMaxLength(30)
            .HasConversion<string>();

        // target_entity_type se persiste en snake_case (valores que consume el
        // ERP): WeeklyDayTemplates → "weekly_day_templates", etc.
        builder.Property(x => x.TargetEntityType)
            .HasColumnName("target_entity_type")
            .HasMaxLength(30)
            .HasConversion(TargetEntityTypeConverter);

        builder.Property(x => x.TargetEntityId)
            .HasColumnName("target_entity_id");

        builder.Property(x => x.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb");

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasColumnType("text");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion<string>()
            .HasDefaultValue(AdaptationStatus.Pending);

        builder.Property(x => x.RequiresApproval)
            .HasColumnName("requires_approval");

        // Los actores (requested_by/decided_by) apuntan a auth.users; la FK se
        // crea por SQL en la migración (fuera del modelo EF).
        builder.Property(x => x.RequestedBy)
            .HasColumnName("requested_by");

        builder.Property(x => x.DecidedBy)
            .HasColumnName("decided_by");

        builder.Property(x => x.DecidedAt)
            .HasColumnName("decided_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.AppliedAt)
            .HasColumnName("applied_at")
            .HasColumnType("timestamptz");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Indexes
        builder.HasIndex(x => x.EnrollmentId)
            .HasDatabaseName("ix_adaptation_recommendations_enrollment_id");

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_adaptation_recommendations_status");

        builder.HasIndex(x => x.Kind)
            .HasDatabaseName("ix_adaptation_recommendations_kind");
    }
}