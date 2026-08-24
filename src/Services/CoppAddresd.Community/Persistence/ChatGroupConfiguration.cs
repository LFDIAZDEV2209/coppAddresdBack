using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

/// <summary>Configuración de la tabla community.chat_groups.</summary>
public sealed class ChatGroupConfiguration : IEntityTypeConfiguration<ChatGroup>
{
    public void Configure(EntityTypeBuilder<ChatGroup> builder)
    {
        builder.ToTable("chat_groups", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedByProfileId).HasColumnName("created_by_profile_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        // El creador no se elimina en cascada: borrar un perfil prohíbe eliminar grupos
        // que lo tengan como creador (Restrict evita la pérdida accidental de grupos).
        builder.HasOne<Profile>().WithMany().HasForeignKey(x => x.CreatedByProfileId).OnDelete(DeleteBehavior.Restrict);
    }
}
