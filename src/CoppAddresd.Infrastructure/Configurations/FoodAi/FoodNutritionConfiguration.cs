using CoppAddresd.Domain.Entities.FoodAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.FoodAi;

public sealed class FoodNutritionConfiguration : IEntityTypeConfiguration<FoodNutrition>
{
    public void Configure(EntityTypeBuilder<FoodNutrition> builder)
    {
        builder.ToTable("food_nutrition", "foodai");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        // Unidad canónica: valores SIEMPRE por ServingGrams (default 100 g).
        builder.Property(x => x.ServingGrams).HasColumnName("serving_grams").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Calories).HasColumnName("calories").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Protein).HasColumnName("protein").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Carbohydrates).HasColumnName("carbohydrates").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Fat).HasColumnName("fat").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Fiber).HasColumnName("fiber").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Sugar).HasColumnName("sugar").HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Sodium).HasColumnName("sodium").HasPrecision(10, 2).IsRequired();

        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(200).IsRequired();
        builder.Property(x => x.SourceVersion).HasColumnName("source_version").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ImportedAt).HasColumnName("imported_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");

        builder.HasOne(x => x.Food)
            .WithMany(x => x.NutritionEntries)
            .HasForeignKey(x => x.FoodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.FoodId, x.ImportedAt })
            .HasDatabaseName("ix_food_nutrition_food_id_imported_at");
    }
}