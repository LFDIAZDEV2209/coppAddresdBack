using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.comment_reports.</summary>
public sealed class CommentReportConfiguration : IEntityTypeConfiguration<CommentReport>
{
    public void Configure(EntityTypeBuilder<CommentReport> builder)
    {
        builder.ToTable("comment_reports", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.CommentId).HasColumnName("comment_id").IsRequired();
        builder.Property(x => x.ReportedByProfileId).HasColumnName("reported_by_profile_id");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Details).HasColumnName("details").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Comment)
            .WithMany(c => c.Reports)
            .HasForeignKey(x => x.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReportedBy)
            .WithMany()
            .HasForeignKey(x => x.ReportedByProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CommentId).HasDatabaseName("ix_comment_reports_comment_id");
        builder.HasIndex(x => x.ReportedByProfileId).HasDatabaseName("ix_comment_reports_reported_by_profile_id");
    }
}
