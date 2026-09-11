using CoppAddresd.Telemedicine.Application.Features.Telemedicine;
using CoppAddresd.Telemedicine.Application.Interfaces;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Handler del backfill: mapea defaults del comando al servicio
/// (rango abierto → historia completa/hoy) y propaga el resultado.
/// </summary>
public class BackfillMetricsHandlerTests
{
    private sealed class StubBackfillService : IMetricsBackfillService
    {
        public DateOnly ReceivedFrom { get; private set; }
        public DateOnly ReceivedTo { get; private set; }
        public Guid? ReceivedClinicId { get; private set; }
        public bool ReceivedDryRun { get; private set; }

        public Task<BackfillMetricsResult> BackfillAsync(
            DateOnly from,
            DateOnly to,
            Guid? clinicId,
            bool dryRun,
            CancellationToken ct
        )
        {
            ReceivedFrom = from;
            ReceivedTo = to;
            ReceivedClinicId = clinicId;
            ReceivedDryRun = dryRun;
            return Task.FromResult(
                new BackfillMetricsResult(from, to, dryRun, 10, 20, 5, TimeSpan.Zero, "ok")
            );
        }
    }

    [Fact]
    public async Task Handle_RangoExplicito_PasaAlServicio()
    {
        var stub = new StubBackfillService();
        var handler = new BackfillMetricsCommandHandler(stub);
        var from = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero);
        var clinic = Guid.NewGuid();

        var result = await handler.Handle(
            new BackfillMetricsCommand(from, to, clinic),
            CancellationToken.None
        );

        Assert.Equal(DateOnly.FromDateTime(from.UtcDateTime), stub.ReceivedFrom);
        Assert.Equal(DateOnly.FromDateTime(to.UtcDateTime), stub.ReceivedTo);
        Assert.Equal(clinic, stub.ReceivedClinicId);
        Assert.False(stub.ReceivedDryRun);
        Assert.Equal(10, result.AppointmentsConsidered);
    }

    [Fact]
    public async Task Handle_SinRango_UsaHistoriaCompletaHastaHoy()
    {
        var stub = new StubBackfillService();
        var handler = new BackfillMetricsCommandHandler(stub);

        var result = await handler.Handle(
            new BackfillMetricsCommand(null, null, null, DryRun: true),
            CancellationToken.None
        );

        Assert.Equal(DateOnly.MinValue, stub.ReceivedFrom);
        Assert.Equal(DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime), stub.ReceivedTo);
        Assert.True(result.DryRun);
    }
}
