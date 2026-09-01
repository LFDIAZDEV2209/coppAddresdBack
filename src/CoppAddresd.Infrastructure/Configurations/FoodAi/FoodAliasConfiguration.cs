using CoppAddresd.Domain.Entities.FoodAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.FoodAi;

public sealed class FoodAliasConfiguration : IEntityTypeConfiguration<FoodAlias>
{
    public void Configure(EntityTypeBuilder<FoodAlias> builder)
    {
        builder.ToTable("food_aliases", "foodai");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Alias).HasColumnName("alias").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Source).HasColumnName("source").HasMaxLength(100).IsRequired();

        builder.HasOne(x => x.Food)
            .WithMany(x => x.Aliases)
            .HasForeignKey(x => x.FoodId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Alias).IsUnique().HasDatabaseName("ix_food_aliases_alias");
    }
}