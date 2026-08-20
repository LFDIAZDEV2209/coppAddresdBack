using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class InventoryMovementConfiguration : IEntityTypeConfiguration<InventoryMovement>
{
    public void Configure(EntityTypeBuilder<InventoryMovement> builder)
    {
        builder.ToTable("inventory_movements", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.DateTime).HasColumnName("date_time").HasColumnType("timestamptz").IsRequired();
        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.Direction).HasColumnName("direction").HasMaxLength(10).IsRequired();
        builder.Property(x => x.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(x => x.StockBefore).HasColumnName("stock_before").IsRequired();
        builder.Property(x => x.StockAfter).HasColumnName("stock_after").IsRequired();
        builder.Property(x => x.Lot).HasColumnName("lot").HasMaxLength(50);
        builder.Property(x => x.User).HasColumnName("user_name").HasMaxLength(100);
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(60);
        builder.Property(x => x.Reference).HasColumnName("reference").HasMaxLength(20);

        builder.HasOne(x => x.Product)
            .WithMany(p => p.Movements)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DateTime).HasDatabaseName("ix_inventory_movements_date_time");
        builder.HasIndex(x => x.ProductId).HasDatabaseName("ix_inventory_movements_product_id");
        builder.HasIndex(x => x.Direction).HasDatabaseName("ix_inventory_movements_direction");

        builder.HasData(InventorySeedData.Movements);
    }
}
