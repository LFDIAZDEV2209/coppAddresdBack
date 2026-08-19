using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class StoreItemConfiguration : IEntityTypeConfiguration<StoreItem>
{
    public void Configure(EntityTypeBuilder<StoreItem> builder)
    {
        builder.ToTable("store_items", "store");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(x => x.SalePrice).HasColumnName("sale_price").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.Featured).HasColumnName("featured").HasDefaultValue(false);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Visible");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        builder.HasOne(x => x.Product)
            .WithMany(p => p.StoreItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProductId).HasDatabaseName("ix_store_items_product_id");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_store_items_status");
        builder.HasIndex(x => x.Featured).HasDatabaseName("ix_store_items_featured");

        builder.HasData(InventorySeedData.StoreItems);
    }
}
