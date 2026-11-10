using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class LegalDocumentAcceptanceConfiguration : IEntityTypeConfiguration<LegalDocumentAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalDocumentAcceptance> builder)
    {
        builder.ToTable("legal_document_acceptances", "app");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.LegalDocumentVersionId).HasColumnName("legal_document_version_id");
        builder.Property(x => x.Scope).HasColumnName("scope").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Locale).HasColumnName("locale").HasMaxLength(2).IsRequired();
        builder.Property(x => x.DisclosureId).HasColumnName("disclosure_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.DisclosureSnapshot).HasColumnName("disclosure_snapshot").HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
        builder.Property(x => x.AcceptedAt).HasColumnName("accepted_at").HasColumnType("timestamptz");
        builder.Property(x => x.WithdrawnAt).HasColumnName("withdrawn_at").HasColumnType("timestamptz");
        builder.HasOne<LegalDocumentVersion>().WithMany().HasForeignKey(x => x.LegalDocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique()
            .HasDatabaseName("uq_legal_acceptance_user_request");
        builder.HasIndex(x => new { x.UserId, x.Scope, x.LegalDocumentVersionId })
            .HasDatabaseName("ix_legal_acceptance_user_scope_version");
    }
}
