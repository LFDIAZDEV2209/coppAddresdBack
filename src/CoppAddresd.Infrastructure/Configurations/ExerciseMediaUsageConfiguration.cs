using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CoppAddresd.Infrastructure.Configurations;
public sealed class ExerciseMediaUsageConfiguration : IEntityTypeConfiguration<ExerciseMediaUsage>
{
    public void Configure(EntityTypeBuilder<ExerciseMediaUsage> builder)
    {
        builder.ToTable("exercise_media_usage", "app");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.Bytes).HasColumnName("bytes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        builder.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("ix_exercise_media_usage_user_created");
    }
}
