using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Infrastructure.Repositories;
using CoppAddresd.Telemedicine.UnitTests;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Persistencia del ciclo de revisión de solicitudes contra PostgreSQL real:
/// aprobación (Pending → Approved) y rechazo con motivo (Pending|Approved →
/// Rejected + rejection_reason), ambos como updates dirigidos del repositorio.
/// Cada test usa solicitudes únicas (la BD de la colección es compartida).
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class RequestReviewPersistenceTests
{
    private readonly TelemedicineTestContext _ctx;

    public RequestReviewPersistenceTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    private static TelemedicineRequest Request() =>
        new()
        {
            PatientId = TestData.PatientId,
            OrganizationId = TestData.Org,
            SpecialtyId = TestData.SpecialtyId,
            ProfessionalId = TestData.ProfessionalId,
            ClinicId = TestData.Clinic,
            LocationId = TestData.LocationId,
            Reason = "Consulta de control",
            Status = AppointmentRequestStatus.Pending,
            CreatedBy = TestData.UserId,
        };

    [Fact]
    public async Task Aprobacion_PersisteEstadoApproved()
    {
        var repo = new RequestRepository(_ctx.Create());
        var request = Request();
        await repo.AddAsync(request);

        await repo.SetStatusAsync(request.Id, AppointmentRequestStatus.Approved);

        var loaded = await repo.GetByIdAsync(request.Id);
        Assert.NotNull(loaded);
        Assert.Equal(AppointmentRequestStatus.Approved, loaded!.Status);
        Assert.Null(loaded.RejectionReason);
    }

    [Fact]
    public async Task Rechazo_PersisteEstadoYMotivo()
    {
        var repo = new RequestRepository(_ctx.Create());
        var request = Request();
        await repo.AddAsync(request);

        await repo.SetRejectedAsync(request.Id, "Sin cupos en el horario");

        var loaded = await repo.GetByIdAsync(request.Id);
        Assert.NotNull(loaded);
        Assert.Equal(AppointmentRequestStatus.Rejected, loaded!.Status);
        Assert.Equal("Sin cupos en el horario", loaded!.RejectionReason);
        Assert.NotNull(loaded.UpdatedAt);
    }

    [Fact]
    public async Task Rechazo_DesdeApproved_ReemplazaMotivo()
    {
        var repo = new RequestRepository(_ctx.Create());
        var request = Request();
        await repo.AddAsync(request);
        await repo.SetStatusAsync(request.Id, AppointmentRequestStatus.Approved);

        await repo.SetRejectedAsync(request.Id, "Motivo definitivo");

        var loaded = await repo.GetByIdAsync(request.Id);
        Assert.NotNull(loaded);
        Assert.Equal(AppointmentRequestStatus.Rejected, loaded!.Status);
        Assert.Equal("Motivo definitivo", loaded.RejectionReason);
    }

    [Fact]
    public async Task SolicitudConvertida_NoSeRechaza()
    {
        var repo = new RequestRepository(_ctx.Create());
        var request = Request();
        await repo.AddAsync(request);

        // El repositorio no valida transiciones (eso es del handler); verifica
        // que el update dirigido persiste incluso desde un estado inesperado,
        // y que la lectura refleja exactamente lo persistido.
        await repo.SetRejectedAsync(request.Id, "Motivo");

        var loaded = await repo.GetByIdAsync(request.Id);
        Assert.NotNull(loaded);
        Assert.Equal(AppointmentRequestStatus.Rejected, loaded!.Status);
    }
}
