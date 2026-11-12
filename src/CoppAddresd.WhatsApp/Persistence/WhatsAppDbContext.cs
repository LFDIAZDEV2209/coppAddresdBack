using CoppAddresd.Domain.Entities.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.WhatsApp.Persistence;

public sealed class WhatsAppDbContext(DbContextOptions<WhatsAppDbContext> options) : DbContext(options)
{
    public DbSet<WhatsAppRawEvent> RawEvents => Set<WhatsAppRawEvent>();
    public DbSet<PatientLink> PatientLinks => Set<PatientLink>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<ReminderDelivery> ReminderDeliveries => Set<ReminderDelivery>();
    public DbSet<BindingSession> BindingSessions => Set<BindingSession>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("whatsapp");
        var binding = model.Entity<BindingSession>();
        binding.ToTable("binding_sessions", t => t.HasCheckConstraint("ck_binding_limits",
            "attempts BETWEEN 0 AND 3 AND otp_sends BETWEEN 0 AND 2 AND cardinality(otp_sent_at) <= 2"));
        binding.HasKey(x => x.Id);
        binding.Property(x => x.WaId).HasMaxLength(16);
        binding.Property(x => x.ContactId).HasMaxLength(20);
        binding.Property(x => x.State).HasConversion<string>().HasMaxLength(30);
        binding.Property(x => x.LastReply).HasMaxLength(1000);
        binding.Ignore(x => x.IsActive);
        binding.HasIndex(x => x.WaId).IsUnique();
        var raw = model.Entity<WhatsAppRawEvent>();
        raw.ToTable("raw_events");
        raw.HasKey(x => x.Id);
        raw.Property(x => x.Wamid).HasMaxLength(512);
        raw.Property(x => x.Payload).HasColumnType("jsonb");
        raw.Property(x => x.WaId).HasMaxLength(16);
        raw.Property(x => x.ErrorCode).HasMaxLength(80);
        raw.HasIndex(x => x.Wamid).IsUnique();
        raw.HasIndex(x => x.NextAttemptAt).HasFilter("processed_at IS NULL");
        var link = model.Entity<PatientLink>();
        link.ToTable("patient_links");
        link.HasKey(x => x.Id);
        link.Property(x => x.WaId).HasMaxLength(16);
        link.Property(x => x.FirstName).HasMaxLength(200);
        link.Property(x => x.Language).HasMaxLength(2);
        link.HasIndex(x => x.WaId).IsUnique();
        link.HasIndex(x => x.PatientId).IsUnique();
        var conversation = model.Entity<Conversation>();
        conversation.ToTable("conversations");
        conversation.HasKey(x => x.Id);
        conversation.Property(x => x.ThreadId).HasMaxLength(160);
        conversation.Property(x => x.Language).HasMaxLength(2);
        conversation.HasIndex(x => x.PatientId).IsUnique();
        var reminder = model.Entity<Reminder>();
        reminder.ToTable("reminders", t => {
            t.HasCheckConstraint("ck_reminders_attempts", "attempts >= 0 AND max_attempts BETWEEN 1 AND 5 AND attempts <= max_attempts AND repeat_every_minutes >= 120");
        });
        reminder.HasKey(x => x.Id);
        reminder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        reminder.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        reminder.Property(x => x.SourceKey).HasMaxLength(200);
        reminder.Property(x => x.Detail).HasMaxLength(500);
        reminder.Property(x => x.CancellationReason).HasMaxLength(80);
        reminder.HasIndex(x => x.SourceKey).IsUnique();
        reminder.HasIndex(x => new { x.PatientId, x.Kind }).IsUnique().HasFilter("state = 'Pending'");
        reminder.HasIndex(x => x.NextAttemptAt).HasFilter("state = 'Pending'");
        reminder.HasIndex(x => x.AssignmentId).HasFilter("assignment_id IS NOT NULL");
        var delivery = model.Entity<ReminderDelivery>();
        delivery.ToTable("reminder_deliveries");
        delivery.HasKey(x => x.Id);
        delivery.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        delivery.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        delivery.Property(x => x.Wamid).HasMaxLength(512);
        delivery.Property(x => x.ErrorCode).HasMaxLength(80);
        delivery.HasOne<Reminder>().WithMany().HasForeignKey(x => x.ReminderId).OnDelete(DeleteBehavior.Cascade);
        delivery.HasIndex(x => new { x.ReminderId, x.Attempt }).IsUnique();
        delivery.HasIndex(x => new { x.PatientId, x.Kind, x.AcceptedAt }).HasFilter("accepted_at IS NOT NULL");
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant());
    }
}
