using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Mapeo del catálogo de asignación en el esquema app con JSON nativo de Npgsql.</summary>
public sealed class ProgramConditionConfiguration : IEntityTypeConfiguration<ProgramCondition>
{
    public void Configure(EntityTypeBuilder<ProgramCondition> builder)
    {
        builder.ToTable("program_conditions", "app");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(x => x.NameEs).HasColumnName("name_es").HasMaxLength(200);
        builder.Property(x => x.NameEn).HasColumnName("name_en").HasMaxLength(200);
        builder.Property(x => x.Icd10Code).HasColumnName("icd10_code").HasMaxLength(20);
        builder.Property(x => x.SourceRef).HasColumnName("source_ref").HasColumnType("text");
        builder.Property(x => x.Criteria).HasColumnName("criteria").HasColumnType("jsonb");
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.HasAlternateKey(x => x.Code).HasName("uq_program_conditions_code");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
    }
}
