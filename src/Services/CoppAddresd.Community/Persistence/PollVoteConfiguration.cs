using CoppAddresd.Community.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Community.Persistence;

public sealed class PollVoteConfiguration : IEntityTypeConfiguration<PollVote>
{
    public void Configure(EntityTypeBuilder<PollVote> builder)
    {
        builder.ToTable("poll_votes", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OptionId).HasColumnName("option_id").IsRequired();
        builder.Property(x => x.ProfileId).HasColumnName("profile_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();

        builder.HasOne(x => x.Option)
            .WithMany(o => o.Votes)
            .HasForeignKey(x => x.OptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Un perfil solo puede votar UNA vez por encuesta (la opción pertenece a
        // una encuesta: violar la unicidad por opción + perfil no lo evita, así
        // que el 1-voto-por-perfil se valida además en el caso de uso).
        builder.HasIndex(x => new { x.OptionId, x.ProfileId }).IsUnique().HasDatabaseName("ux_poll_votes_option_profile");
        builder.HasIndex(x => x.ProfileId).HasDatabaseName("ix_poll_votes_profile_id");
    }
}
