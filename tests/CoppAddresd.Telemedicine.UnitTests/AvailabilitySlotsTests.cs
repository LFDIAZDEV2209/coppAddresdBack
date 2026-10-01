using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;
using FluentValidation;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Disponibilidad de ranuras (<see cref="GetAvailabilitySlotsQueryHandler"/>) y
/// reprogramación directa por paciente (Decisión 2, Opción A): modos profesional
/// y especialidad, anticipación mínima, solapamiento, UTC y propiedad.
/// </summary>
public class AvailabilitySlotsTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly GetAvailabilitySlotsQueryHandler _handler;

    public AvailabilitySlotsTests()
    {
        _handler = new GetAvailabilitySlotsQueryHandler(_appointments, _referenceData, _settings);
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
    }

    private static DateOnly NextWeekday(DayOfWeek day)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        while (date.DayOfWeek != day)
        {
            date = date.AddDays(1);
        }
        return date;
    }

    private static int IsoWeekday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    private static DateTimeOffset UtcOf(DateOnly date, int hour, int minute = 0) =>
        new(date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc));

    [Fact]
    public async Task Handle_ProfesionalSinHorario_RetornaVacia()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            TestData.Clinic,
            null,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(TestData.ProfessionalId, result.ProfessionalId);
        Assert.Null(result.SpecialtyId);
        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task Handle_ProfesionalInexistente_LanzaNotFound()
    {
        var query = new GetAvailabilitySlotsQuery(
            Guid.NewGuid(),
            null,
            TestData.Org,
            null,
            null,
            NextWeekday(DayOfWeek.Monday)
        );

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_RanuraSolapada_ExcluyeOcupadoYConservaLibre()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        _referenceData.Schedules[TestData.ProfessionalId] =
        [
            new ProfessionalScheduleRefDto(IsoWeekday(date), "09:00", "10:00"),
        ];
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: UtcOf(date, 9, 30))
        );
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            null,
            null,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        // Veredicto B4: el slot ocupado 09:30 no está presente en la lista.
        var slot = Assert.Single(result.Slots);
        Assert.Equal(UtcOf(date, 9), slot.Start);
        Assert.True(slot.IsAvailable);
        Assert.Null(slot.ConflictReason);
        Assert.Equal(1, slot.AvailableProfessionalCount);
        Assert.Equal("+00:00", result.TimezoneOffset);
    }

    [Fact]
    public async Task Handle_AnticipacionMinima_ExcluyeSlotsEnVentana()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        _referenceData.Schedules[TestData.ProfessionalId] =
        [
            new ProfessionalScheduleRefDto(IsoWeekday(date), "00:00", "23:59"),
        ];
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            null,
            null,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        // Veredicto B4: ningún slot en ventana de anticipación está presente.
        // La jornada completa son 47 slots de 30 min; al menos los 4 primeros
        // (00:00–01:30) caen siempre dentro de now + 2 h.
        Assert.True(result.Slots.Count < 47);
        foreach (var slot in result.Slots)
        {
            Assert.True(slot.IsAvailable);
            Assert.Null(slot.ConflictReason);
            Assert.True(
                slot.Start
                    >= DateTimeOffset.UtcNow.AddHours(_settings.Settings.MinAdvanceBookingHours)
            );
        }
    }

    [Fact]
    public async Task Handle_CitaConOffsetLocal_SolapaEnUtc()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        _referenceData.Schedules[TestData.ProfessionalId] =
        [
            new ProfessionalScheduleRefDto(IsoWeekday(date), "09:00", "10:00"),
        ];
        // 09:00 UTC expresado con offset +05:00: el instante es el mismo.
        var startConOffset = new DateTimeOffset(
            date.ToDateTime(new TimeOnly(14, 0)),
            TimeSpan.FromHours(5)
        );
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: startConOffset)
        );
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            null,
            null,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        // El slot 09:00 UTC ocupado no está presente; solo queda el 09:30.
        var slot = Assert.Single(result.Slots);
        Assert.Equal(UtcOf(date, 9, 30), slot.Start);
        Assert.Equal(TimeSpan.Zero, slot.Start.Offset);
        Assert.True(slot.IsAvailable);
    }

    [Fact]
    public async Task Handle_ModoEspecialidad_UneSlotsYCuentaProfesionales()
    {
        var date = NextWeekday(DayOfWeek.Tuesday);
        var weekday = IsoWeekday(date);
        var otherProfessional = Guid.NewGuid();
        _referenceData.Candidates =
        [
            new ProfessionalCandidateRefDto(
                TestData.ProfessionalId,
                Guid.NewGuid(),
                TestData.UserId,
                "Dra. Ana Pérez",
                [TestData.Clinic],
                [TestData.LocationId],
                [new ProfessionalScheduleRefDto(weekday, "09:00", "10:00")]
            ),
            new ProfessionalCandidateRefDto(
                otherProfessional,
                Guid.NewGuid(),
                null,
                "Dr. Luis Gómez",
                [TestData.Clinic],
                [TestData.LocationId],
                [new ProfessionalScheduleRefDto(weekday, "09:00", "10:00")]
            ),
        ];
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: UtcOf(date, 9))
        );
        var query = new GetAvailabilitySlotsQuery(
            null,
            TestData.SpecialtyId,
            TestData.Org,
            TestData.Clinic,
            TestData.LocationId,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Null(result.ProfessionalId);
        Assert.Equal(TestData.SpecialtyId, result.SpecialtyId);
        Assert.Equal(2, result.Slots.Count);
        var first = result.Slots[0];
        Assert.True(first.IsAvailable);
        Assert.Equal(1, first.AvailableProfessionalCount);
        var second = result.Slots[1];
        Assert.True(second.IsAvailable);
        Assert.Equal(2, second.AvailableProfessionalCount);
    }

    [Fact]
    public async Task Handle_ModoEspecialidadSinCandidatos_RetornaVacia()
    {
        _referenceData.Candidates = [];
        var query = new GetAvailabilitySlotsQuery(
            null,
            TestData.SpecialtyId,
            TestData.Org,
            null,
            null,
            NextWeekday(DayOfWeek.Wednesday)
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task Handle_ModoEspecialidadSlotTotalmenteOcupado_ExcluyeSlot()
    {
        var date = NextWeekday(DayOfWeek.Thursday);
        var weekday = IsoWeekday(date);
        var otherProfessional = Guid.NewGuid();
        _referenceData.Candidates =
        [
            new ProfessionalCandidateRefDto(
                TestData.ProfessionalId,
                Guid.NewGuid(),
                TestData.UserId,
                "Dra. Ana Pérez",
                [TestData.Clinic],
                [TestData.LocationId],
                [new ProfessionalScheduleRefDto(weekday, "09:00", "10:00")]
            ),
            new ProfessionalCandidateRefDto(
                otherProfessional,
                Guid.NewGuid(),
                null,
                "Dr. Luis Gómez",
                [TestData.Clinic],
                [TestData.LocationId],
                [new ProfessionalScheduleRefDto(weekday, "09:00", "10:00")]
            ),
        ];
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: UtcOf(date, 9))
        );
        _appointments.Items.Add(
            TestData.Appointment(professionalId: otherProfessional, start: UtcOf(date, 9))
        );
        var query = new GetAvailabilitySlotsQuery(
            null,
            TestData.SpecialtyId,
            TestData.Org,
            null,
            null,
            date
        );

        var result = await _handler.Handle(query, CancellationToken.None);

        // Veredicto B4: el slot 09:00 (sin profesionales libres) no está presente.
        var slot = Assert.Single(result.Slots);
        Assert.Equal(UtcOf(date, 9, 30), slot.Start);
        Assert.True(slot.IsAvailable);
        Assert.Equal(2, slot.AvailableProfessionalCount);
    }

    [Fact]
    public async Task Handle_ModoEspecialidadInexistente_LanzaNotFound()
    {
        _referenceData.Candidates = null;
        var query = new GetAvailabilitySlotsQuery(
            null,
            Guid.NewGuid(),
            TestData.Org,
            null,
            null,
            NextWeekday(DayOfWeek.Wednesday)
        );

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None)
        );
    }

    [Fact]
    public void Validate_SinProfesionalNiEspecialidad_Invalido()
    {
        var validator = new GetAvailabilitySlotsQueryValidator();
        var query = new GetAvailabilitySlotsQuery(
            null,
            null,
            TestData.Org,
            null,
            null,
            NextWeekday(DayOfWeek.Monday)
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_EspecialidadSinOrganizacion_Invalido()
    {
        var validator = new GetAvailabilitySlotsQueryValidator();
        var query = new GetAvailabilitySlotsQuery(
            null,
            TestData.SpecialtyId,
            null,
            null,
            null,
            NextWeekday(DayOfWeek.Monday)
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_FechaDefault_Invalido()
    {
        var validator = new GetAvailabilitySlotsQueryValidator();
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            null,
            null,
            null,
            default
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_ModoProfesionalSinOrganizacion_Valido()
    {
        var validator = new GetAvailabilitySlotsQueryValidator();
        var query = new GetAvailabilitySlotsQuery(
            TestData.ProfessionalId,
            null,
            null,
            null,
            null,
            NextWeekday(DayOfWeek.Monday)
        );

        Assert.True(validator.Validate(query).IsValid);
    }
}

/// <summary>
/// Modo rango de availability (nueva llamada única para la ventana del wizard):
/// contrato days[] completo, tope de 14 días, exclusión de parámetros y
/// batching (consultas de BD constantes, no 1 por día/profesional).
/// </summary>
public class AvailabilityRangeSlotsTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly GetAvailabilityRangeSlotsQueryHandler _handler;
    private readonly GetAvailabilityRangeSlotsQueryValidator _validator = new();

    public AvailabilityRangeSlotsTests()
    {
        _handler = new GetAvailabilityRangeSlotsQueryHandler(
            _appointments,
            _referenceData,
            _settings
        );
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
    }

    private static int IsoWeekday(DateOnly date) => ((int)date.DayOfWeek + 6) % 7 + 1;

    private static DateTimeOffset UtcOf(DateOnly date, int hour, int minute = 0) =>
        new(date.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Utc));

    [Fact]
    public async Task Handle_Rango7Dias_DiasCompletosYSoloPrimerDiaConCupo()
    {
        // Lunes de la semana siguiente: garante de franja 09:00-10:00 solo 1 día.
        var monday = NextAvailableWeekday(DayOfWeek.Monday);
        _referenceData.Schedules[TestData.ProfessionalId] =
        [
            new ProfessionalScheduleRefDto(IsoWeekday(monday), "09:00", "10:00"),
        ];
        var from = monday.AddDays(-2); // sábado (sin franja)
        var query = RangoProfesional(from, 7);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.Equal(TestData.ProfessionalId, result.ProfessionalId);
        Assert.Null(result.SpecialtyId);
        Assert.Equal(7, result.Days.Count);
        // Days[] completo: 7 entradas en orden, la del lunes con 2 slots libres.
        var conCupo = result.Days.Where(d => d.Slots.Count > 0).ToList();
        var dia = Assert.Single(conCupo);
        Assert.Equal(monday, dia.Date);
        Assert.All(
            dia.Slots,
            s =>
            {
                Assert.True(s.IsAvailable);
                Assert.Null(s.ConflictReason);
            }
        );
        Assert.Equal(UtcOf(monday, 9), dia.Slots[0].Start);
        Assert.Equal(UtcOf(monday, 9, 30), dia.Slots[1].Start);
    }

    [Fact]
    public async Task Handle_ModoEspecialidadRango_UneSlotsYBatcheaCitas()
    {
        var monday = NextAvailableWeekday(DayOfWeek.Monday);
        var weekday = IsoWeekday(monday);
        var otherProfessional = Guid.NewGuid();
        _referenceData.Candidates =
        [
            new ProfessionalCandidateRefDto(
                TestData.ProfessionalId,
                Guid.NewGuid(),
                TestData.UserId,
                "Dra. Ana Pérez",
                [TestData.Clinic],
                [TestData.LocationId],
                [new ProfessionalScheduleRefDto(weekday, "09:00", "10:00")]
            ),
            new ProfessionalCandidateRefDto(
                otherProfessional,
                Guid.NewGuid(),
                null,
                "Dr. Luis Gómez",
                [TestData.Clinic],
                [TestData.LocationId],
                [new ProfessionalScheduleRefDto(weekday, "09:00", "10:00")]
            ),
        ];
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: UtcOf(monday, 9))
        );

        var result = await _handler.Handle(RangoEspecialidad(monday, 7), CancellationToken.None);

        var dia = Assert.Single(result.Days.Where(d => d.Slots.Count > 0));
        // 09:00 solo con 1 profesional libre (Ana tiene cita), 09:30 con 2.
        Assert.Equal(UtcOf(monday, 9), dia.Slots[0].Start);
        Assert.Equal(1, dia.Slots[0].AvailableProfessionalCount);
        Assert.Equal(2, dia.Slots[1].AvailableProfessionalCount);
    }

    [Fact]
    public async Task Handle_ModoEspecialidadRango_ConsultasConstantes()
    {
        // Batching (MUST del spec): 1 candidates + 1 citas batcheada, sin
        // importar días ni profesionales — el fake cuenta las llamadas.
        var monday = NextAvailableWeekday(DayOfWeek.Monday);
        var weekday = IsoWeekday(monday);
        _referenceData.Candidates = Enumerable
            .Range(0, 5)
            .Select(i => new ProfessionalCandidateRefDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                $"Médico {i}",
                [TestData.Clinic],
                [TestData.LocationId],
                new List<ProfessionalScheduleRefDto>
                {
                    new(weekday, "09:00", "10:00"),
                    new((weekday % 7) + 1, "09:00", "10:00"),
                }
            ))
            .ToList();
        _appointments.Items.Clear();

        var antes = _appointments.LlamadasListado;
        await _handler.Handle(RangoEspecialidad(monday, 14), CancellationToken.None);

        Assert.Equal(1, _appointments.LlamadasListado - antes);
    }

    [Fact]
    public async Task Handle_ModoEspecialidadSinCandidatos_DaysCompletosVacios()
    {
        _referenceData.Candidates = [];
        var from = NextAvailableWeekday(DayOfWeek.Friday);

        var result = await _handler.Handle(RangoEspecialidad(from, 5), CancellationToken.None);

        Assert.Equal(5, result.Days.Count);
        Assert.All(result.Days, d => Assert.Empty(d.Slots));
    }

    [Fact]
    public void Validate_RangoMayorA14Dias_Invalido()
    {
        Assert.False(
            _validator
                .Validate(RangoProfesional(DateOnly.FromDateTime(DateTime.UtcNow), 15))
                .IsValid
        );
    }

    [Fact]
    public void Validate_Rango14Dias_Valido()
    {
        Assert.True(
            _validator
                .Validate(RangoProfesional(DateOnly.FromDateTime(DateTime.UtcNow), 14))
                .IsValid
        );
    }

    [Fact]
    public void Validate_FechasDefault_Invalido()
    {
        var query = new GetAvailabilityRangeSlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            null,
            null,
            default,
            default
        );

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_OrdenInvertido_Invalido()
    {
        var query = new GetAvailabilityRangeSlotsQuery(
            TestData.ProfessionalId,
            null,
            TestData.Org,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(5),
            DateOnly.FromDateTime(DateTime.UtcNow)
        );

        Assert.False(_validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_EspecialidadSinOrganizacion_Invalido()
    {
        var query = new GetAvailabilityRangeSlotsQuery(
            null,
            TestData.SpecialtyId,
            null,
            null,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(6)
        );

        Assert.False(_validator.Validate(query).IsValid);
    }

    private static GetAvailabilityRangeSlotsQuery RangoProfesional(DateOnly from, int dias) =>
        new(TestData.ProfessionalId, null, TestData.Org, null, null, from, from.AddDays(dias - 1));

    private static GetAvailabilityRangeSlotsQuery RangoEspecialidad(DateOnly from, int dias) =>
        new(
            null,
            TestData.SpecialtyId,
            TestData.Org,
            TestData.Clinic,
            TestData.LocationId,
            from,
            from.AddDays(dias - 1)
        );

    private static DateOnly NextAvailableWeekday(DayOfWeek day)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        while (date.DayOfWeek != day)
        {
            date = date.AddDays(1);
        }
        return date;
    }
}

/// <summary>
/// Reprogramación directa por paciente (Decisión 2, Opción A): propiedad vía
/// perfil resuelto por usuario Auth, forzado de <c>RequestedBy.Patient</c> y
/// ventana de anticipación de la cita original.
/// </summary>
public class PatientRescheduleHandlerTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly FakeAlertRepository _alerts = new();
    private readonly RescheduleAppointmentCommandHandler _handler;

    private readonly Guid _otherUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Guid _otherPatientId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public PatientRescheduleHandlerTests()
    {
        _handler = new RescheduleAppointmentCommandHandler(
            _appointments,
            _referenceData,
            _settings,
            _alerts
        );
        _referenceData.Professionals[TestData.ProfessionalId] = TestData.Professional(
            userId: TestData.UserId
        );
        _referenceData.Patients[TestData.PatientId] = TestData.Patient(
            userId: TestData.PatientUserId
        );
        _referenceData.UserToPatient[TestData.PatientUserId] = TestData.PatientId;
        _referenceData.Patients[_otherPatientId] = TestData.Patient(
            id: _otherPatientId,
            userId: _otherUserId
        );
        _referenceData.UserToPatient[_otherUserId] = _otherPatientId;
        _referenceData.Specialties[TestData.SpecialtyId] = TestData.Specialty();
    }

    [Fact]
    public async Task Handle_PacienteDueno_ReprogramaYFuerzaRequestedByPatient()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(3));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            null,
            "Cambio de horario",
            RescheduleRequestedBy.Admin,
            TestData.PatientUserId,
            TestData.PatientUserId
        );

        var dto = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, dto.RescheduleCount);
        var reschedule = Assert.Single(appointment.Reschedules);
        Assert.Equal(RescheduleRequestedBy.Patient, reschedule.RequestedBy);
    }

    [Fact]
    public async Task Handle_PacienteAjeno_LanzaForbidden()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(3));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            null,
            null,
            RescheduleRequestedBy.Patient,
            _otherUserId,
            _otherUserId
        );

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
        Assert.Empty(appointment.Reschedules);
    }

    [Fact]
    public async Task Handle_PacienteSinPerfil_LanzaForbidden()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(3));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            null,
            null,
            RescheduleRequestedBy.Patient,
            Guid.NewGuid(),
            Guid.NewGuid()
        );

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Handle_CitaEnVentanaDeAnticipacion_LanzaViolacion()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddHours(1));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(2),
            null,
            null,
            RescheduleRequestedBy.Admin,
            TestData.UserId
        );

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _handler.Handle(command, CancellationToken.None)
        );
        Assert.Empty(appointment.Reschedules);
    }

    [Fact]
    public async Task Handle_ErpSinPaciente_ConservaRequestedByDelBody()
    {
        var appointment = TestData.Appointment(start: DateTimeOffset.UtcNow.AddDays(3));
        _appointments.Items.Add(appointment);
        var command = new RescheduleAppointmentCommand(
            appointment.Id,
            DateTimeOffset.UtcNow.AddDays(4),
            null,
            null,
            RescheduleRequestedBy.Professional,
            TestData.UserId
        );

        await _handler.Handle(command, CancellationToken.None);

        var reschedule = Assert.Single(appointment.Reschedules);
        Assert.Equal(RescheduleRequestedBy.Professional, reschedule.RequestedBy);
    }
}
