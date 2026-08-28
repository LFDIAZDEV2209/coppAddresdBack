using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.post_reports.</summary>
public sealed class PostReportConfiguration : IEntityTypeConfiguration<PostReport>
{
    public void Configure(EntityTypeBuilder<PostReport> builder)
    {
        builder.ToTable("post_reports", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(x => x.ReportedByProfileId).HasColumnName("reported_by_profile_id");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Details).HasColumnName("details").HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Post)
            .WithMany(p => p.Reports)
            .HasForeignKey(x => x.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ReportedBy)
            .WithMany()
            .HasForeignKey(x => x.ReportedByProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.PostId).HasDatabaseName("ix_post_reports_post_id");
        builder.HasIndex(x => x.ReportedByProfileId).HasDatabaseName("ix_post_reports_reported_by_profile_id");
    }
}
