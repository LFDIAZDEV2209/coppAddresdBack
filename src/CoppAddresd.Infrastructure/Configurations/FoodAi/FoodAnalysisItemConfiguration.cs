using CoppAddresd.Domain.Entities.FoodAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.FoodAi;

public sealed class FoodAnalysisItemConfiguration : IEntityTypeConfiguration<FoodAnalysisItem>
{
    public void Configure(EntityTypeBuilder<FoodAnalysisItem> builder)
    {
        builder.ToTable("food_analysis_items", "foodai");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.AnalysisId).HasColumnName("analysis_id").IsRequired();
        builder.Property(x => x.ItemIndex).HasColumnName("item_index").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.DetectionConfidence).HasColumnName("detection_confidence").HasPrecision(6, 4).IsRequired();

        builder.Property(x => x.BboxX).HasColumnName("bbox_x").IsRequired();
        builder.Property(x => x.BboxY).HasColumnName("bbox_y").IsRequired();
        builder.Property(x => x.BboxWidth).HasColumnName("bbox_width").IsRequired();
        builder.Property(x => x.BboxHeight).HasColumnName("bbox_height").IsRequired();

        builder.Property(x => x.MaskKey).HasColumnName("mask_key").HasMaxLength(300);
        builder.Property(x => x.MaskAreaPixels).HasColumnName("mask_area_pixels");

        builder.Property(x => x.PortionSize).HasColumnName("portion_size").HasMaxLength(20);
        builder.Property(x => x.EstimatedGrams).HasColumnName("estimated_grams");
        builder.Property(x => x.MinGrams).HasColumnName("min_grams");
        builder.Property(x => x.MaxGrams).HasColumnName("max_grams");
        builder.Property(x => x.PortionConfidence).HasColumnName("portion_confidence").HasPrecision(6, 4);
        builder.Property(x => x.PortionMethod).HasColumnName("portion_method").HasMaxLength(50);

        builder.Property(x => x.NutritionStatus).HasColumnName("nutrition_status").HasMaxLength(30);
        builder.Property(x => x.Calories).HasColumnName("calories").HasPrecision(12, 2);
        builder.Property(x => x.Protein).HasColumnName("protein").HasPrecision(12, 2);
        builder.Property(x => x.Carbohydrates).HasColumnName("carbohydrates").HasPrecision(12, 2);
        builder.Property(x => x.Fat).HasColumnName("fat").HasPrecision(12, 2);
        builder.Property(x => x.Fiber).HasColumnName("fiber").HasPrecision(12, 2);
        builder.Property(x => x.Sugar).HasColumnName("sugar").HasPrecision(12, 2);
        builder.Property(x => x.Sodium).HasColumnName("sodium").HasPrecision(12, 2);
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(200);
        builder.Property(x => x.SourceVersion).HasColumnName("source_version").HasMaxLength(100);

        builder.HasOne(x => x.Analysis)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AnalysisId, x.ItemIndex })
            .IsUnique()
            .HasDatabaseName("ix_food_analysis_items_analysis_id_item_index");
    }
}