using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class InventoryExitLineConfiguration : IEntityTypeConfiguration<InventoryExitLine>
{
    public void Configure(EntityTypeBuilder<InventoryExitLine> builder)
    {
        builder.ToTable("inventory_exit_lines", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ExitId).HasColumnName("exit_id").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(x => x.Lot).HasColumnName("lot").HasMaxLength(50);
        builder.Property(x => x.ExpirationDate).HasColumnName("expiration_date").HasColumnType("date");
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasColumnType("numeric(12,2)").HasDefaultValue(0m);

        builder.HasOne(x => x.Exit)
            .WithMany(e => e.Lines)
            .HasForeignKey(x => x.ExitId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.ExitLines)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(InventorySeedData.ExitLines);
    }
}
