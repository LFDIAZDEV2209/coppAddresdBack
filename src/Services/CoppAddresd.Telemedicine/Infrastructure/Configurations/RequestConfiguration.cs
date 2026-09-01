using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Telemedicine.Infrastructure.Configurations;

/// <summary>
/// Configuración EF de la solicitud de telemedicina (<c>tele.telemedicine_requests</c>).
/// Referencias débiles al ERP: sin FK, solo índices sobre las columnas.
/// </summary>
public sealed class RequestConfiguration : IEntityTypeConfiguration<TelemedicineRequest>
{
    public void Configure(EntityTypeBuilder<TelemedicineRequest> builder)
    {
        builder.ToTable("telemedicine_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason).HasMaxLength(2000);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(x => x.PatientId);
        builder.HasIndex(x => x.ProfessionalId);
        builder.HasIndex(x => x.SpecialtyId);
        builder.HasIndex(x => new { x.OrganizationId, x.Status });
        builder.HasIndex(x => x.Status);
    }
}
