using CoppAddresd.Domain.Entities.FoodAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.FoodAi;

public sealed class FoodAnalysisFeedbackConfiguration : IEntityTypeConfiguration<FoodAnalysisFeedback>
{
    public void Configure(EntityTypeBuilder<FoodAnalysisFeedback> builder)
    {
        builder.ToTable("food_analysis_feedback", "foodai");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.AnalysisId).HasColumnName("analysis_id").IsRequired();
        builder.Property(x => x.ItemId).HasColumnName("item_id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.FeedbackType).HasColumnName("feedback_type").HasMaxLength(30).IsRequired();

        builder.Property(x => x.OriginalFood).HasColumnName("original_food").HasMaxLength(100);
        builder.Property(x => x.CorrectedFood).HasColumnName("corrected_food").HasMaxLength(100);
        builder.Property(x => x.OriginalGrams).HasColumnName("original_grams");
        builder.Property(x => x.CorrectedGrams).HasColumnName("corrected_grams");
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");

        builder.HasOne(x => x.Analysis)
            .WithMany(x => x.Feedbacks)
            .HasForeignKey(x => x.AnalysisId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.AnalysisId, x.CreatedAt })
            .HasDatabaseName("ix_food_analysis_feedback_analysis_id_created_at");
    }
}