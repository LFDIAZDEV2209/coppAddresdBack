using CoppAddresd.Domain.Entities.FoodAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.FoodAi;

public sealed class FoodAnalysisConfiguration : IEntityTypeConfiguration<FoodAnalysis>
{
    public void Configure(EntityTypeBuilder<FoodAnalysis> builder)
    {
        builder.ToTable("food_analyses", "foodai");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.AnalysisId).HasColumnName("analysis_id").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        builder.Property(x => x.ImageKey).HasColumnName("image_key").HasMaxLength(300);
        builder.Property(x => x.UserId).HasColumnName("user_id");

        builder.Property(x => x.DetectorVersion).HasColumnName("detector_version").HasMaxLength(100).IsRequired();
        builder.Property(x => x.SegmenterVersion).HasColumnName("segmenter_version").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ClassifierVersion).HasColumnName("classifier_version").HasMaxLength(100).IsRequired();
        builder.Property(x => x.PortionMethod).HasColumnName("portion_method").HasMaxLength(50).IsRequired();
        builder.Property(x => x.DepthModelVersion).HasColumnName("depth_model_version").HasMaxLength(100);

        builder.Property(x => x.SummaryCalories).HasColumnName("summary_calories").HasPrecision(12, 2);
        builder.Property(x => x.SummaryProtein).HasColumnName("summary_protein").HasPrecision(12, 2);
        builder.Property(x => x.SummaryCarbohydrates).HasColumnName("summary_carbohydrates").HasPrecision(12, 2);
        builder.Property(x => x.SummaryFat).HasColumnName("summary_fat").HasPrecision(12, 2);
        builder.Property(x => x.SummaryFiber).HasColumnName("summary_fiber").HasPrecision(12, 2);
        builder.Property(x => x.SummarySugar).HasColumnName("summary_sugar").HasPrecision(12, 2);
        builder.Property(x => x.SummarySodium).HasColumnName("summary_sodium").HasPrecision(12, 2);
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(200);
        builder.Property(x => x.SourceVersion).HasColumnName("source_version").HasMaxLength(100);

        builder.HasIndex(x => x.AnalysisId).IsUnique().HasDatabaseName("ix_food_analyses_analysis_id");
        builder.HasIndex(x => x.UserId).HasDatabaseName("ix_food_analyses_user_id");
    }
}