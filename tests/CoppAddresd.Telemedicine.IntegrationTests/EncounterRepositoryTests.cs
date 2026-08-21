using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;

using CoppAddresd.Telemedicine.UnitTests;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Idempotencia del encuentro clínico contra PostgreSQL: 1:1 con la cita
/// (índice único) — la creación perezosa concurrente devuelve el existente y
/// los estados Draft/Completed/Cancelled persisten.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class EncounterRepositoryTests
{
    private readonly TelemedicineTestContext _ctx;

    public EncounterRepositoryTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    private async Task<TelemedicineAppointment> SeedAppointmentAsync(AppointmentStatus status = AppointmentStatus.InProgress)
    {
        var appointment = new TelemedicineAppointment
        {
            // Profesional único por test (la BD de la colección es compartida).
            ProfessionalId = Guid.NewGuid(),
            PatientId = TestData.PatientId,
            SpecialtyId = TestData.SpecialtyId,
            OrganizationId = TestData.Org,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            ScheduledStart = DateTimeOffset.UtcNow.AddMinutes(5),
            ScheduledEnd = DateTimeOffset.UtcNow.AddMinutes(35),
            DurationMinutes = 30,
            Status = status,
            CreatedBy = TestData.UserId,
        };
        await using var db = _ctx.Create();
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment;
    }

    [Fact]
    public async Task CreacionConcurrente_DevuelveElExistente()
    {
        var appointment = await SeedAppointmentAsync();
        var repo = new EncounterRepository(_ctx.Create());
        var first = new ClinicalEncounter
        {
            AppointmentId = appointment.Id,
            PatientId = TestData.PatientId,
            ProfessionalId = TestData.ProfessionalId,
            EncounterDate = DateTimeOffset.UtcNow,
            Status = EncounterStatus.Draft,
            Notes = "Primer borrador",
        };

        var created = await repo.AddAsync(first);

        // Carrera: otro guardado concurrente sobre la misma cita devuelve el existente.
        var retry = new ClinicalEncounter
        {
            AppointmentId = appointment.Id,
            PatientId = TestData.PatientId,
            ProfessionalId = TestData.ProfessionalId,
            EncounterDate = DateTimeOffset.UtcNow,
            Status = EncounterStatus.Draft,
            Notes = "Borrador del perdedor de la carrera",
        };
        var returned = await repo.AddAsync(retry);

        Assert.Equal(created.Id, returned.Id);
        Assert.Equal("Primer borrador", returned.Notes);
    }

    [Fact]
    public async Task GuardadoYCompletado_PersistenEstados()
    {
        var appointment = await SeedAppointmentAsync();
        var repo = new EncounterRepository(_ctx.Create());
        var encounter = new ClinicalEncounter
        {
            AppointmentId = appointment.Id,
            PatientId = TestData.PatientId,
            ProfessionalId = TestData.ProfessionalId,
            EncounterDate = DateTimeOffset.UtcNow,
            Status = EncounterStatus.Draft,
            Notes = "Nota",
        };
        await repo.AddAsync(encounter);

        var forUpdate = await repo.GetForUpdateByAppointmentIdAsync(appointment.Id);
        Assert.NotNull(forUpdate);
        forUpdate!.Status = EncounterStatus.Completed;
        forUpdate.Notes = "Nota final";
        await repo.UpdateAsync(forUpdate);

        var loaded = await repo.GetByAppointmentIdAsync(appointment.Id);
        Assert.Equal(EncounterStatus.Completed, loaded!.Status);
        Assert.Equal("Nota final", loaded.Notes);
    }

    [Fact]
    public async Task EncuentroDeCitaCancelada_SePersisteComoCancelled()
    {
        var appointment = await SeedAppointmentAsync();
        var repo = new EncounterRepository(_ctx.Create());
        var encounter = new ClinicalEncounter
        {
            AppointmentId = appointment.Id,
            PatientId = TestData.PatientId,
            ProfessionalId = TestData.ProfessionalId,
            EncounterDate = DateTimeOffset.UtcNow,
            Status = EncounterStatus.Cancelled,
            Notes = "Borrador cancelado por cancelación de la cita",
        };
        await repo.AddAsync(encounter);

        var loaded = await repo.GetByAppointmentIdAsync(appointment.Id);

        Assert.Equal(EncounterStatus.Cancelled, loaded!.Status);
    }
}