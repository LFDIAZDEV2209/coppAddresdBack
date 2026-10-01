using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Domain.Enums;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Profesionales con cupo en una ventana de días
/// (<see cref="GetAvailableProfessionalsQueryHandler"/>): modo especialidad,
/// primera ranura libre, días con cupo, anticipación mínima y validación de
/// la ventana. Alimenta el badge "Con cupo" del picker de la app.
/// </summary>
public class AvailableProfessionalsTests
{
    private readonly FakeReferenceDataService _referenceData = new();
    private readonly FakeAppointmentRepository _appointments = new();
    private readonly FakeSettingsProvider _settings = new();
    private readonly GetAvailableProfessionalsQueryHandler _handler;

    public AvailableProfessionalsTests()
    {
        _handler = new GetAvailableProfessionalsQueryHandler(
            _appointments,
            _referenceData,
            _settings
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

    private static ProfessionalCandidateRefDto Candidate(
        Guid professionalId,
        int weekday,
        string start = "09:00",
        string end = "10:00"
    ) =>
        new(
            professionalId,
            Guid.NewGuid(),
            null,
            "Dra. Demo",
            [TestData.Clinic],
            [TestData.LocationId],
            [new ProfessionalScheduleRefDto(weekday, start, end)]
        );

    private GetAvailableProfessionalsQuery Query(DateOnly from, DateOnly to) =>
        new(TestData.SpecialtyId, TestData.Org, TestData.Clinic, TestData.LocationId, from, to);

    [Fact]
    public async Task Handle_CandidatoConHorarioLibre_DevuelvePrimeraRanuraYDias()
    {
        var from = NextWeekday(DayOfWeek.Monday);
        var to = from.AddDays(13); // dos lunes dentro de la ventana
        _referenceData.Candidates = [Candidate(TestData.ProfessionalId, IsoWeekday(from))];

        var result = await _handler.Handle(Query(from, to), CancellationToken.None);

        var item = Assert.Single(result.Professionals);
        Assert.Equal(TestData.ProfessionalId, item.ProfessionalId);
        Assert.Equal(UtcOf(from, 9), item.NextAvailableStart);
        Assert.Equal(2, item.AvailableDays);
        Assert.Equal("+00:00", result.TimezoneOffset);
        Assert.Equal(TestData.SpecialtyId, result.SpecialtyId);
    }

    [Fact]
    public async Task Handle_RanuraParcialOcupada_DevuelveSiguienteLibre()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        _referenceData.Candidates = [Candidate(TestData.ProfessionalId, IsoWeekday(date))];
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: UtcOf(date, 9))
        );

        var result = await _handler.Handle(Query(date, date), CancellationToken.None);

        var item = Assert.Single(result.Professionals);
        Assert.Equal(UtcOf(date, 9, 30), item.NextAvailableStart);
        Assert.Equal(1, item.AvailableDays);
    }

    [Fact]
    public async Task Handle_DiaSinRanurasLibres_NoCuentaEseDia()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        _referenceData.Candidates =
        [
            Candidate(TestData.ProfessionalId, IsoWeekday(date), "09:00", "10:00"),
        ];
        // Ocupa las dos ranuras del día (09:00 y 09:30).
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: UtcOf(date, 9))
        );
        _appointments.Items.Add(
            TestData.Appointment(professionalId: TestData.ProfessionalId, start: UtcOf(date, 9, 30))
        );

        var result = await _handler.Handle(Query(date, date), CancellationToken.None);

        Assert.Empty(result.Professionals);
    }

    [Fact]
    public async Task Handle_AnticipacionMinima_ExcluyeDiaCompleto()
    {
        // Horario hoy en la madrugada: con 2h de anticipación mínima ya no hay
        // ranuras libres (misma regla que /availability).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _referenceData.Candidates =
        [
            Candidate(TestData.ProfessionalId, IsoWeekday(today), "00:00", "00:30"),
        ];

        var result = await _handler.Handle(Query(today, today), CancellationToken.None);

        Assert.Empty(result.Professionals);
    }

    [Fact]
    public async Task Handle_CandidatoSinHorario_NoAparece()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        var other = Guid.NewGuid();
        _referenceData.Candidates =
        [
            Candidate(TestData.ProfessionalId, IsoWeekday(date)),
            new ProfessionalCandidateRefDto(
                other,
                Guid.NewGuid(),
                null,
                "Dr. Sin Horario",
                [TestData.Clinic],
                [TestData.LocationId],
                []
            ),
        ];

        var result = await _handler.Handle(Query(date, date), CancellationToken.None);

        var item = Assert.Single(result.Professionals);
        Assert.Equal(TestData.ProfessionalId, item.ProfessionalId);
    }

    [Fact]
    public async Task Handle_CitasCanceladasNoBloquean()
    {
        var date = NextWeekday(DayOfWeek.Monday);
        _referenceData.Candidates = [Candidate(TestData.ProfessionalId, IsoWeekday(date))];
        _appointments.Items.Add(
            TestData.Appointment(
                status: AppointmentStatus.Cancelled,
                professionalId: TestData.ProfessionalId,
                start: UtcOf(date, 9)
            )
        );

        var result = await _handler.Handle(Query(date, date), CancellationToken.None);

        var item = Assert.Single(result.Professionals);
        Assert.Equal(UtcOf(date, 9), item.NextAvailableStart);
    }

    [Fact]
    public async Task Handle_SinCandidatos_RetornaVacia()
    {
        _referenceData.Candidates = [];

        var result = await _handler.Handle(
            Query(NextWeekday(DayOfWeek.Monday), NextWeekday(DayOfWeek.Monday)),
            CancellationToken.None
        );

        Assert.Empty(result.Professionals);
    }

    [Fact]
    public async Task Handle_EspecialidadInexistente_LanzaNotFound()
    {
        _referenceData.Candidates = null;

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _handler.Handle(
                Query(NextWeekday(DayOfWeek.Monday), NextWeekday(DayOfWeek.Monday)),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public void Validate_SinEspecialidad_Invalido()
    {
        var validator = new GetAvailableProfessionalsQueryValidator();
        var query = new GetAvailableProfessionalsQuery(
            Guid.Empty,
            TestData.Org,
            null,
            null,
            NextWeekday(DayOfWeek.Monday),
            NextWeekday(DayOfWeek.Monday)
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_SinOrganizacion_Invalido()
    {
        var validator = new GetAvailableProfessionalsQueryValidator();
        var query = new GetAvailableProfessionalsQuery(
            TestData.SpecialtyId,
            null,
            null,
            null,
            NextWeekday(DayOfWeek.Monday),
            NextWeekday(DayOfWeek.Monday)
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_RangoInvertido_Invalido()
    {
        var validator = new GetAvailableProfessionalsQueryValidator();
        var from = NextWeekday(DayOfWeek.Monday);
        var query = new GetAvailableProfessionalsQuery(
            TestData.SpecialtyId,
            TestData.Org,
            null,
            null,
            from,
            from.AddDays(-1)
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_VentanaMayorAlTope_Invalido()
    {
        var validator = new GetAvailableProfessionalsQueryValidator();
        var from = NextWeekday(DayOfWeek.Monday);
        var query = new GetAvailableProfessionalsQuery(
            TestData.SpecialtyId,
            TestData.Org,
            null,
            null,
            from,
            from.AddDays(GetAvailableProfessionalsQueryValidator.MaxWindowDays)
        );

        Assert.False(validator.Validate(query).IsValid);
    }

    [Fact]
    public void Validate_VentanaTope_Valido()
    {
        var validator = new GetAvailableProfessionalsQueryValidator();
        var from = NextWeekday(DayOfWeek.Monday);
        var query = new GetAvailableProfessionalsQuery(
            TestData.SpecialtyId,
            TestData.Org,
            null,
            null,
            from,
            from.AddDays(GetAvailableProfessionalsQueryValidator.MaxWindowDays - 1)
        );

        Assert.True(validator.Validate(query).IsValid);
    }
}
