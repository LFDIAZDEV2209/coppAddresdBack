using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CoppAddresd.Infrastructure.Configurations;
public sealed class ExerciseMediaImportJobConfiguration : IEntityTypeConfiguration<ExerciseMediaImportJob>
{
    public void Configure(EntityTypeBuilder<ExerciseMediaImportJob> b)
    {
        b.ToTable("exercise_media_import_jobs", "app");b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.SourceId).HasColumnName("source_id");b.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(10);
        b.Property(x => x.Status).HasColumnName("status").HasMaxLength(20);b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until");b.Property(x => x.MediaId).HasColumnName("media_id");
        b.Property(x => x.Error).HasColumnName("error").HasMaxLength(1000);
        b.Property(x => x.CreatedAt).HasColumnName("created_at");b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("ix_exercise_media_jobs_user_created");
        b.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("ix_exercise_media_jobs_pending").HasFilter("status IN ('queued', 'processing')");
    }
}
