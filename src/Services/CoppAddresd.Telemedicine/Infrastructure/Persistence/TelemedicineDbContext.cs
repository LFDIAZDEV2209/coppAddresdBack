using CoppAddresd.Telemedicine.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Telemedicine.Infrastructure.Persistence;

/// <summary>
/// DbContext del microservicio de Telemedicina. Único dueño del schema
/// <c>tele</c> en PostgreSQL (regla del workspace: cada servicio solo toca su
/// schema). Los datos maestros (pacientes, profesionales, sedes) viven en otros
/// schemas y se referencian solo por Id (referencias débiles, sin FK).
/// </summary>
public sealed class TelemedicineDbContext(DbContextOptions<TelemedicineDbContext> options)
    : DbContext(options)
{
    public DbSet<TelemedicineRequest> Requests => Set<TelemedicineRequest>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<AppointmentCancellation> Cancellations => Set<AppointmentCancellation>();
    public DbSet<AppointmentReschedule> Reschedules => Set<AppointmentReschedule>();
    public DbSet<VirtualRoom> Rooms => Set<VirtualRoom>();
    public DbSet<TelemedicineSession> Sessions => Set<TelemedicineSession>();
    public DbSet<ClinicalEncounter> Encounters => Set<ClinicalEncounter>();
    public DbSet<TelemedicineAlert> Alerts => Set<TelemedicineAlert>();
    public DbSet<TelemedicineSettings> Settings => Set<TelemedicineSettings>();
    public DbSet<TelemedicineWebhookEvent> WebhookEvents => Set<TelemedicineWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Todo el modelo de este servicio vive en el schema tele.
        modelBuilder.HasDefaultSchema("tele");

        // Convención del repo: nombres de tabla/columna en snake_case
        // (se activa con UseSnakeCaseNamingConvention en las opciones del DbContext).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TelemedicineDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
