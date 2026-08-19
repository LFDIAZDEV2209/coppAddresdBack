using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class InventoryExitConfiguration : IEntityTypeConfiguration<InventoryExit>
{
    public void Configure(EntityTypeBuilder<InventoryExit> builder)
    {
        builder.ToTable("inventory_exits", "inventory");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Date).HasColumnName("date").HasColumnType("date").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Responsible).HasColumnName("responsible").HasMaxLength(100);
        builder.Property(x => x.PatientName).HasColumnName("patient_name").HasMaxLength(150);
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");

        builder.HasIndex(x => x.Reference).IsUnique().HasDatabaseName("ix_inventory_exits_reference");
        builder.HasIndex(x => x.Date).HasDatabaseName("ix_inventory_exits_date");

        builder.HasData(InventorySeedData.Exits);
    }
}
