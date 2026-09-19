using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Adendas del encuentro (F4): matriz de autorización (profesional/supervisor
/// OK; paciente y ajeno 403), precondición de encuentro <c>Completed</c>
/// (Draft/sin encuentro/otro estado → 409), orden cronológico estable,
/// snapshot del autor y validación 1–2000. No hay endpoints de edición ni
/// borrado (append-only por contrato).
/// </summary>
public class EncounterAddendumHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeEncounterRepository _encounters = new();
    private readonly FakeEncounterAddendumRepository _addenda = new();
    private readonly AddEncounterAddendumCommandHandler _add;
    private readonly GetEncounterAddendaQueryHandler _list;

    public EncounterAddendumHandlerTests()
    {
        _add = new AddEncounterAddendumCommandHandler(
            _appointments, _encounters, _addenda, _referenceData,
            NullLogger<AddEncounterAddendumCommandHandler>.Instance);
        _list = new GetEncounterAddendaQueryHandler(
            _appointments, _encounters, _addenda, _referenceData);
    }

    private Appointment AddAppointment(
        AppointmentStatus status = AppointmentStatus.Completed)
    {
        var appointment = TestData.Appointment(status: status);
        _appointments.Items.Add(appointment);
        return appointment;
    }

    private ClinicalEncounter AddEncounter(
        Appointment appointment,
        EncounterStatus status = EncounterStatus.Completed)
    {
        var encounter = new ClinicalEncounter
        {
            AppointmentId = appointment.Id,
            PatientId = appointment.PatientId,
            ProfessionalId = appointment.ProfessionalId,
            Status = status,
            Notes = "nota",
            CreatedBy = TestData.UserId,
        };
        _encounters.Items.Add(encounter);
        return encounter;
    }

    private void AsProfessional(Appointment appointment)
    {
        _referenceData.Professionals[appointment.ProfessionalId] =
            TestData.Professional(userId: TestData.UserId);
        _referenceData.UserToProfessional[TestData.UserId] = appointment.ProfessionalId;
    }

    private void AsPatient(Appointment appointment)
    {
        _referenceData.Patients[appointment.PatientId] = TestData.Patient();
        _referenceData.UserToPatient[TestData.PatientUserId] = appointment.PatientId;
    }

    [Fact]
    public async Task Add_ProfesionalConEncuentroCompletado_CreaAutorYNombre()
    {
        var appointment = AddAppointment();
        AsProfessional(appointment);
        var encounter = AddEncounter(appointment);

        var dto = await _add.Handle(
            new AddEncounterAddendumCommand(
                appointment.Id, "  corrección del diagnóstico  ", "Dra. Ana Pérez",
                TestData.UserId, false),
            CancellationToken.None);

        Assert.Single(_addenda.Items);
        Assert.Equal(encounter.Id, dto.EncounterId);
        Assert.Equal(TestData.UserId, dto.AuthorUserId);
        Assert.Equal("Dra. Ana Pérez", dto.AuthorName);
        Assert.Equal("corrección del diagnóstico", dto.Body);
    }

    [Fact]
    public async Task Add_SinNombreEnElClaim_AutorNameNull()
    {
        var appointment = AddAppointment();
        AsProfessional(appointment);
        AddEncounter(appointment);

        var dto = await _add.Handle(
            new AddEncounterAddendumCommand(
                appointment.Id, "aclaración", null, TestData.UserId, false),
            CancellationToken.None);

        Assert.Null(dto.AuthorName);
    }

    [Fact]
    public async Task Add_NombreLargo_SeTruncaAlMaximoDeColumna()
    {
        var appointment = AddAppointment();
        AsProfessional(appointment);
        AddEncounter(appointment);

        var dto = await _add.Handle(
            new AddEncounterAddendumCommand(
                appointment.Id, "aclaración", new string('x', 250), TestData.UserId, false),
            CancellationToken.None);

        Assert.Equal(200, dto.AuthorName!.Length);
    }

    [Fact]
    public async Task Add_Supervisor_EsPermitido()
    {
        var appointment = AddAppointment();
        AddEncounter(appointment);

        var dto = await _add.Handle(
            new AddEncounterAddendumCommand(
                appointment.Id, "revisión de supervisión", "Supervisor", Guid.NewGuid(), true),
            CancellationToken.None);

        Assert.Equal("revisión de supervisión", dto.Body);
    }

    [Fact]
    public async Task Add_Paciente_LanzaForbidden()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);
        AddEncounter(appointment);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _add.Handle(
                new AddEncounterAddendumCommand(
                    appointment.Id, "intento", "Paciente", TestData.PatientUserId, false),
                CancellationToken.None));

        Assert.Empty(_addenda.Items);
    }

    [Fact]
    public async Task Add_UsuarioAjeno_LanzaForbidden()
    {
        var appointment = AddAppointment();
        AddEncounter(appointment);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _add.Handle(
                new AddEncounterAddendumCommand(
                    appointment.Id, "intruso", null, Guid.NewGuid(), false),
                CancellationToken.None));

        Assert.Empty(_addenda.Items);
    }

    [Fact]
    public async Task Add_CitaInexistente_LanzaNotFound()
    {
        var appointment = TestData.Appointment();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _add.Handle(
                new AddEncounterAddendumCommand(
                    appointment.Id, "adenda", null, TestData.UserId, true),
                CancellationToken.None));
    }

    [Fact]
    public async Task Add_SinEncuentro_LanzaViolacion()
    {
        var appointment = AddAppointment();
        AsProfessional(appointment);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _add.Handle(
                new AddEncounterAddendumCommand(
                    appointment.Id, "adenda", null, TestData.UserId, false),
                CancellationToken.None));

        Assert.Empty(_addenda.Items);
    }

    [Fact]
    public async Task Add_EncuentroDraft_LanzaViolacion()
    {
        var appointment = AddAppointment(AppointmentStatus.InProgress);
        AsProfessional(appointment);
        AddEncounter(appointment, EncounterStatus.Draft);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _add.Handle(
                new AddEncounterAddendumCommand(
                    appointment.Id, "adenda", null, TestData.UserId, false),
                CancellationToken.None));

        Assert.Empty(_addenda.Items);
    }

    [Fact]
    public async Task List_OrdenaPorCreatedAtEId()
    {
        var appointment = AddAppointment();
        AddEncounter(appointment);
        var encounter = _encounters.Items[0];
        var t0 = new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);
        // Inserta desordenado y con empate de instante para probar el desempate por id.
        var later = new EncounterAddendum
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            EncounterId = encounter.Id,
            AuthorUserId = TestData.UserId,
            Body = "segunda",
            CreatedAt = t0.AddMinutes(1),
        };
        var tieHigh = new EncounterAddendum
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            EncounterId = encounter.Id,
            AuthorUserId = TestData.UserId,
            Body = "empate-alto",
            CreatedAt = t0,
        };
        var tieLow = new EncounterAddendum
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            EncounterId = encounter.Id,
            AuthorUserId = TestData.UserId,
            Body = "empate-bajo",
            CreatedAt = t0,
        };
        _addenda.Items.AddRange([later, tieHigh, tieLow]);

        var result = await _list.Handle(
            new GetEncounterAddendaQuery(appointment.Id, TestData.UserId, true),
            CancellationToken.None);

        Assert.Equal(["empate-bajo", "empate-alto", "segunda"], result.Select(a => a.Body));
    }

    [Fact]
    public async Task List_SinEncuentro_DevuelveVacio()
    {
        var appointment = AddAppointment();
        AsProfessional(appointment);

        var result = await _list.Handle(
            new GetEncounterAddendaQuery(appointment.Id, TestData.UserId, false),
            CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task List_Paciente_LanzaForbidden()
    {
        var appointment = AddAppointment();
        AsPatient(appointment);
        AddEncounter(appointment);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _list.Handle(
                new GetEncounterAddendaQuery(appointment.Id, TestData.PatientUserId, false),
                CancellationToken.None));
    }

    [Fact]
    public async Task List_CitaInexistente_LanzaNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _list.Handle(
                new GetEncounterAddendaQuery(Guid.NewGuid(), TestData.UserId, true),
                CancellationToken.None));
    }

    [Fact]
    public void Validator_BodyVacioOExcedido_Invalido()
    {
        var validator = new AddEncounterAddendumCommandValidator();

        Assert.False(validator.Validate(
            new AddEncounterAddendumCommand(Guid.NewGuid(), "", null, Guid.NewGuid(), false)).IsValid);
        Assert.False(validator.Validate(
            new AddEncounterAddendumCommand(Guid.NewGuid(), "   ", null, Guid.NewGuid(), false)).IsValid);
        Assert.False(validator.Validate(
            new AddEncounterAddendumCommand(
                Guid.NewGuid(), new string('x', 2001), null, Guid.NewGuid(), false)).IsValid);
    }

    [Fact]
    public void Validator_Valido_EsValido()
    {
        var validator = new AddEncounterAddendumCommandValidator();

        Assert.True(validator.Validate(
            new AddEncounterAddendumCommand(
                Guid.NewGuid(), "corrección", "Dra. Ana", Guid.NewGuid(), false)).IsValid);
    }
}
