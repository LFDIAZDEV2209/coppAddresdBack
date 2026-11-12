using CoppAddresd.Domain.Entities.WhatsApp;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.WhatsApp.Persistence;

public sealed class WhatsAppDbContext(DbContextOptions<WhatsAppDbContext> options) : DbContext(options)
{
    public DbSet<WhatsAppRawEvent> RawEvents => Set<WhatsAppRawEvent>();
    public DbSet<PatientLink> PatientLinks => Set<PatientLink>();
    public DbSet<Conversation> Conversations => Set<Conversation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("whatsapp");
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
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties())
                property.SetColumnName(System.Text.RegularExpressions.Regex.Replace(property.Name, "(?<!^)([A-Z])", "_$1").ToLowerInvariant());
    }
}
