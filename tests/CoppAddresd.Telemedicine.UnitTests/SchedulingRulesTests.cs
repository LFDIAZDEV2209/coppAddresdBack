using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Domain.Exceptions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Reglas parametrizadas de agendamiento (SchedulingRules.ResolveSlot): duración,
/// anticipación mínima, ventana máxima y normalización a UTC.
/// </summary>
public class SchedulingRulesTests
{
    private static readonly TelemedicineSettings Settings = TestData.Settings();

    [Fact]
    public void ResolveSlot_DuracionDefault_CuandoNoSeSolicita()
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(1).AddMinutes(1);

        var (resolvedStart, end, duration) = SchedulingRules.ResolveSlot(
            start, null, Settings, now);

        Assert.Equal(30, duration);
        Assert.Equal(start.ToUniversalTime(), resolvedStart);
        Assert.Equal(resolvedStart.AddMinutes(30), end);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(240)]
    public void ResolveSlot_DuracionSolicitadaValida_SeUsa(int requested)
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(1);

        var (_, _, duration) = SchedulingRules.ResolveSlot(start, requested, Settings, now);

        Assert.Equal(requested, duration);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(241)]
    [InlineData(1000)]
    public void ResolveSlot_DuracionInvalida_CaeAlDefault(int requested)
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(1);

        var (_, _, duration) = SchedulingRules.ResolveSlot(start, requested, Settings, now);

        Assert.Equal(Settings.DefaultAppointmentDurationMinutes, duration);
    }

    [Fact]
    public void ResolveSlot_PocaAnticipacion_LanzaViolacion()
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddHours(Settings.MinAdvanceBookingHours).AddMinutes(-1);

        Assert.Throws<BusinessRuleViolationException>(() =>
            SchedulingRules.ResolveSlot(start, null, Settings, now));
    }

    [Fact]
    public void ResolveSlot_MasAllaDeVentanaMaxima_LanzaViolacion()
    {
        var now = DateTimeOffset.UtcNow;
        var start = now.AddDays(Settings.MaxAdvanceBookingDays).AddMinutes(1);

        Assert.Throws<BusinessRuleViolationException>(() =>
            SchedulingRules.ResolveSlot(start, null, Settings, now));
    }

    [Fact]
    public void ResolveSlot_ConOffsetDelCliente_NormalizaAUtc()
    {
        var now = DateTimeOffset.UtcNow;
        // El cliente envía una hora con su offset local (p. ej. UTC-5).
        var localStart = now.AddDays(1).AddMinutes(1).ToOffset(TimeSpan.FromHours(-5));

        var (resolvedStart, _, _) = SchedulingRules.ResolveSlot(localStart, null, Settings, now);

        // El resultado debe estar en UTC (offset 0) para persistir en timestamptz.
        Assert.Equal(TimeSpan.Zero, resolvedStart.Offset);
        Assert.Equal(localStart.ToUniversalTime(), resolvedStart);
    }
}
