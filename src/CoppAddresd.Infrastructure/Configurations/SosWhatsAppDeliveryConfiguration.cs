using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace CoppAddresd.Infrastructure.Configurations;
public sealed class SosWhatsAppDeliveryConfiguration : IEntityTypeConfiguration<SosWhatsAppDelivery>
{
    public void Configure(EntityTypeBuilder<SosWhatsAppDelivery> b)
    {
        b.ToTable("sos_whatsapp_deliveries", "app"); b.HasKey(x=>x.Id);
        b.Property(x=>x.Destination).HasMaxLength(16);b.Property(x=>x.PatientName).HasMaxLength(500);
        b.Property(x=>x.State).HasMaxLength(20);b.Property(x=>x.Wamid).HasMaxLength(512);
        b.Property(x=>x.ErrorCode).HasMaxLength(80);b.Property(x=>x.DeliveryStatus).HasMaxLength(20);
        b.Property(x=>x.ParametersJson).HasColumnType("jsonb");
        b.HasOne<SosAlert>().WithMany().HasForeignKey(x=>x.AlertId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x=>new{x.AlertId,x.Destination}).IsUnique();
        b.HasIndex(x=>new{x.State,x.NextAttemptAt});
        b.HasIndex(x=>x.LeaseUntil).HasFilter("state = 'Processing'");
        foreach(var p in b.Metadata.GetProperties()) p.SetColumnName(System.Text.RegularExpressions.Regex.Replace(p.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant());
    }
}
