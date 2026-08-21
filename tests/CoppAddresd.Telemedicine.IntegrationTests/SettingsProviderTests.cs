using CoppAddresd.Telemedicine.Domain.Entities;
using CoppAddresd.Telemedicine.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;

using CoppAddresd.Telemedicine.UnitTests;

namespace CoppAddresd.Telemedicine.IntegrationTests;

/// <summary>
/// Resolución de configuración operativa (TelemedicineSettingsProvider):
/// preferencia clínica → organización → defaults, contra PostgreSQL real con
/// caché en memoria.
/// </summary>
[Collection(TelemedicineTestCollection.Name)]
public class SettingsProviderTests
{
    private readonly TelemedicineTestContext _ctx;

    public SettingsProviderTests(TelemedicineTestDatabase database)
    {
        _ctx = new TelemedicineTestContext(database.ConnectionString);
    }

    private static TelemedicineSettingsProvider CreateProvider(TelemedicineTestContext ctx)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new TelemedicineSettingsProvider(ctx.Create(), cache);
    }

    [Fact]
    public async Task SinConfiguracion_DevuelveDefaults()
    {
        var provider = CreateProvider(_ctx);
        var orgId = Guid.NewGuid();

        var settings = await provider.GetSettingsAsync(orgId, null);

        Assert.Equal(30, settings.DefaultAppointmentDurationMinutes);
        Assert.Equal(2, settings.MinAdvanceBookingHours);
        Assert.Equal(30, settings.MaxAdvanceBookingDays);
        Assert.Equal(2, settings.MaxReschedules);
    }

    [Fact]
    public async Task ConFilaDeOrganizacion_UsaEsosValores()
    {
        var orgId = Guid.NewGuid();
        await using (var db = _ctx.Create())
        {
            db.Settings.Add(new TelemedicineSettings
            {
                OrganizationId = orgId,
                ClinicId = null,
                DefaultAppointmentDurationMinutes = 45,
                MinAdvanceBookingHours = 4,
            });
            await db.SaveChangesAsync();
        }

        var provider = CreateProvider(_ctx);
        var settings = await provider.GetSettingsAsync(orgId, null);

        Assert.Equal(45, settings.DefaultAppointmentDurationMinutes);
        Assert.Equal(4, settings.MinAdvanceBookingHours);
    }

    [Fact]
    public async Task FilaDeClinica_PrecedeALaDeOrganizacion()
    {
        var orgId = Guid.NewGuid();
        var clinicId = Guid.NewGuid();
        await using (var db = _ctx.Create())
        {
            db.Settings.AddRange(
                new TelemedicineSettings
                {
                    OrganizationId = orgId,
                    ClinicId = null,
                    DefaultAppointmentDurationMinutes = 45,
                },
                new TelemedicineSettings
                {
                    OrganizationId = orgId,
                    ClinicId = clinicId,
                    DefaultAppointmentDurationMinutes = 60,
                    MinAdvanceBookingHours = 6,
                });
            await db.SaveChangesAsync();
        }

        var provider = CreateProvider(_ctx);
        var settings = await provider.GetSettingsAsync(orgId, clinicId);

        Assert.Equal(60, settings.DefaultAppointmentDurationMinutes);
        Assert.Equal(6, settings.MinAdvanceBookingHours);
    }

    [Fact]
    public async Task ClinicSinFila_CaeALaDeOrganizacion()
    {
        var orgId = Guid.NewGuid();
        await using (var db = _ctx.Create())
        {
            db.Settings.Add(new TelemedicineSettings
            {
                OrganizationId = orgId,
                ClinicId = null,
                DefaultAppointmentDurationMinutes = 45,
            });
            await db.SaveChangesAsync();
        }

        var provider = CreateProvider(_ctx);
        var settings = await provider.GetSettingsAsync(orgId, Guid.NewGuid());

        Assert.Equal(45, settings.DefaultAppointmentDurationMinutes);
    }
}