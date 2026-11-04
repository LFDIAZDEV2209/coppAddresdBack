using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Entities.ProgramProgress;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations.ProgramProgress;

/// <summary>Persistencia aditiva de reintentos y avisos de asignación, dentro del esquema app.</summary>
public sealed class ProgramAssignmentRecoveryConfiguration : IEntityTypeConfiguration<ProgramAssignmentRecovery>
{
    public void Configure(EntityTypeBuilder<ProgramAssignmentRecovery> b)
    {
        b.ToTable("program_assignment_recoveries", "app");
        b.HasKey(x => x.BatteryAssignmentId);
        b.Property(x => x.BatteryAssignmentId).HasColumnName("battery_assignment_id").ValueGeneratedNever();
        b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at").HasColumnType("timestamptz");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        b.HasOne<HealthTestBatteryAssignment>().WithMany().HasForeignKey(x => x.BatteryAssignmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ProgramAssignmentNoticeConfiguration : IEntityTypeConfiguration<ProgramAssignmentNotice>
{
    public void Configure(EntityTypeBuilder<ProgramAssignmentNotice> b)
    {
        b.ToTable("program_assignment_notices", "app");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.RecommendationId).HasColumnName("recommendation_id");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.Kind).HasColumnName("kind").HasMaxLength(32);
        b.Property(x => x.TitleEs).HasColumnName("title_es").HasMaxLength(120);
        b.Property(x => x.TitleEn).HasColumnName("title_en").HasMaxLength(120);
        b.Property(x => x.MessageEs).HasColumnName("message_es").HasColumnType("text");
        b.Property(x => x.MessageEn).HasColumnName("message_en").HasColumnType("text");
        b.Property(x => x.Link).HasColumnName("link").HasMaxLength(300);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        b.Property(x => x.ReadAt).HasColumnName("read_at").HasColumnType("timestamptz");
        b.Property(x => x.DeliveredAt).HasColumnName("delivered_at").HasColumnType("timestamptz");
        b.Property(x => x.PushCompleted).HasColumnName("push_completed");
        b.Property(x => x.ChatCompleted).HasColumnName("chat_completed");
        b.Property(x => x.Attempts).HasColumnName("attempts");
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at").HasColumnType("timestamptz");
        b.Property(x => x.LeaseUntil).HasColumnName("lease_until").HasColumnType("timestamptz");
        b.HasIndex(x => new { x.RecommendationId, x.UserId, x.Kind }).IsUnique().HasDatabaseName("uq_program_assignment_notices_rec_user_kind");
        b.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("ix_program_assignment_notices_user_created");
        b.HasIndex(x => x.NextAttemptAt).HasFilter("delivered_at IS NULL").HasDatabaseName("ix_program_assignment_notices_pending");
        b.HasOne<ProgramAssignmentRecommendation>().WithMany().HasForeignKey(x => x.RecommendationId).OnDelete(DeleteBehavior.Restrict);
    }
}
