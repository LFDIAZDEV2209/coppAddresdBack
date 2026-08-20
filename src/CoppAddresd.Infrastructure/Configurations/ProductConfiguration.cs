using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.SeedData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", "erp");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductType).HasColumnName("product_type").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ActiveIngredient).HasColumnName("active_ingredient").HasMaxLength(200);
        builder.Property(x => x.Presentation).HasColumnName("presentation").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Concentration).HasColumnName("concentration").HasMaxLength(50);
        builder.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Manufacturer).HasColumnName("manufacturer").HasMaxLength(150);
        builder.Property(x => x.Supplier).HasColumnName("supplier").HasMaxLength(150);
        builder.Property(x => x.Lot).HasColumnName("lot").HasMaxLength(50);
        builder.Property(x => x.ExpirationDate).HasColumnName("expiration_date").HasColumnType("date");
        builder.Property(x => x.Stock).HasColumnName("stock").HasDefaultValue(0);
        builder.Property(x => x.MinimumStock).HasColumnName("minimum_stock").HasDefaultValue(0);
        builder.Property(x => x.MaximumStock).HasColumnName("maximum_stock").HasDefaultValue(100);
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(100);
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("Activo");
        builder.Property(x => x.UnitCost).HasColumnName("unit_cost").HasColumnType("numeric(12,2)").HasDefaultValue(0m);
        builder.Property(x => x.Notes).HasColumnName("notes");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");

        builder.HasIndex(x => x.Sku).IsUnique().HasDatabaseName("ix_products_sku");
        builder.HasIndex(x => x.ProductType).HasDatabaseName("ix_products_product_type");
        builder.HasIndex(x => x.Category).HasDatabaseName("ix_products_category");
        builder.HasIndex(x => x.Status).HasDatabaseName("ix_products_status");

        builder.HasData(InventorySeedData.Products);
    }
}
