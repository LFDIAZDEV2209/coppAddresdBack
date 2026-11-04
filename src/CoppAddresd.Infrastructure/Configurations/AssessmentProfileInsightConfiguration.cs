using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class AssessmentProfileInsightConfiguration : IEntityTypeConfiguration<AssessmentProfileInsight>
{
    public void Configure(EntityTypeBuilder<AssessmentProfileInsight> b)
    {
        b.ToTable("assessment_profile_insights", "app");
        b.HasKey(x => x.Id); b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id");
        b.Property(x => x.RequestedBy).HasColumnName("requested_by");
        b.Property(x => x.SourceRevision).HasColumnName("source_revision").HasMaxLength(64);
        b.Property(x => x.Language).HasColumnName("language").HasMaxLength(2);
        b.Property(x => x.ContentJson).HasColumnName("content_json").HasColumnType("jsonb");
        b.Property(x => x.GeneratedAt).HasColumnName("generated_at");
        b.HasOne<PatientProfile>().WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.PatientId, x.SourceRevision, x.Language }).IsUnique().HasDatabaseName("uq_assessment_insight_source_language");
    }
}
