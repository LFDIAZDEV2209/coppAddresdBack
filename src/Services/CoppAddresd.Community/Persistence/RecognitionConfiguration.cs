using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.recognitions.</summary>
public sealed class RecognitionConfiguration : IEntityTypeConfiguration<Recognition>
{
    public void Configure(EntityTypeBuilder<Recognition> builder)
    {
        builder.ToTable("recognitions", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.TypeLabel).HasColumnName("type_label").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Xp).HasColumnName("xp").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).HasDefaultValue(RecognitionStatus.Sent).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.TriggeredByProfileId).HasColumnName("triggered_by_profile_id");

        builder.HasOne<Profile>().WithMany().HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Profile>().WithMany().HasForeignKey(x => x.TriggeredByProfileId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("ix_recognitions_created_at");
        builder.HasIndex(x => x.ProfileId).HasDatabaseName("ix_recognitions_profile_id");
    }
}
