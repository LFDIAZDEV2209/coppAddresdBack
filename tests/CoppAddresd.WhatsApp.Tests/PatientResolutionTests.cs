using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using CoppAddresd.WhatsApp.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CoppAddresd.WhatsApp.Tests;

[Trait("Category", "Integration")]
public sealed class PatientResolutionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task NewPhone_LinksOnce_AndReusesThreadWithoutAnotherPatientQuery()
    {
        await using var scope = Services().BuildServiceProvider();
        using var requestScope = scope.CreateScope();
        var services = requestScope.ServiceProvider;
        var patient = new PatientProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), FirstName = "Ana", LastName = "Prueba", PhoneCountryCode = "57", PhoneNumber = "305 392-4819" };
        await using var db = fixture.CreateDb();
        await CreatePatientTableAsync(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO app.patient_profiles (id, user_id, first_name, phone_country_code, phone_number, status)
            VALUES ({patient.Id}, {patient.UserId}, {patient.FirstName}, {patient.PhoneCountryCode}, {patient.PhoneNumber}, 'Activo')
            """, CancellationToken.None);
        var resolver = services.GetRequiredService<WhatsAppPatientResolver>();
        var first = await resolver.ResolveAsync("573053924819", DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(patient.Id, first!.PatientId);
        var thread = await db.Conversations.AsNoTracking().SingleAsync(x => x.PatientId == patient.Id);
        // El vínculo es la fuente del canal (D3), incluso si el perfil cambia después.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app.patient_profiles SET phone_number = 'otro' WHERE id = {patient.Id}", CancellationToken.None);
        var second = await resolver.ResolveAsync("+57 (305) 392-4819", DateTimeOffset.UtcNow, CancellationToken.None);
        Assert.Equal(first.Id, second!.Id);
        Assert.Equal(1, await db.PatientLinks.CountAsync(x => x.PatientId == patient.Id));
        Assert.Equal(thread.ThreadId, (await db.Conversations.AsNoTracking().SingleAsync(x => x.PatientId == patient.Id)).ThreadId);
    }

    [Fact]
    public async Task UnknownPhone_GuidesWithoutCreatingConversationOrCallingChat()
    {
        await using var db = fixture.CreateDb();
        await CreatePatientTableAsync(db);
        var sender = Substitute.For<IWhatsAppMessageSender>();
        sender.SendTextAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new WhatsAppSendResult("wamid.guide"));
        await using var provider = Services(sender).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<WhatsAppPatientResolver>();
        Assert.Null(await resolver.ResolveAsync("+573000000000", DateTimeOffset.UtcNow, CancellationToken.None));
        Assert.True((await resolver.GuideUnknownAsync("+573000000000", DateTimeOffset.UtcNow, CancellationToken.None)).Succeeded);
        await sender.Received(1).SendTextAsync("+573000000000", Arg.Is<string>(text => text.Contains("regístrate")), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        Assert.False(await db.PatientLinks.AnyAsync(x => x.WaId == "+573000000000"));
    }

    private ServiceCollection Services(IWhatsAppMessageSender? sender = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(sender ?? Substitute.For<IWhatsAppMessageSender>());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<FindActivePatientByPhoneQuery>());
        services.AddScoped<IPatientChannelQuery, PatientChannelQuery>();
        services.AddDbContext<AppDbContext>(db => db.UseNpgsql(fixture.ConnectionString));
        services.AddDbContext<CoppAddresd.WhatsApp.Persistence.WhatsAppDbContext>(db => db.UseNpgsql(fixture.ConnectionString));
        services.AddScoped<WhatsAppPatientResolver>();
        return services;
    }

    // Solo el contrato de lectura de pacientes en una BD desechable; no migra otros módulos.
    private static Task CreatePatientTableAsync(CoppAddresd.WhatsApp.Persistence.WhatsAppDbContext db) => db.Database.ExecuteSqlRawAsync("""
        CREATE SCHEMA IF NOT EXISTS app;
        CREATE TABLE IF NOT EXISTS app.patient_profiles (
            id uuid PRIMARY KEY, user_id uuid, first_name text NOT NULL,
            phone_country_code text, phone_number text, status text NOT NULL, deleted_at timestamptz
        );
        """, CancellationToken.None);
}

[Trait("Category", "Unit")]
public sealed class PhoneTests
{
    [Theory]
    [InlineData("305 392-4819", "+573053924819")]
    [InlineData("+57 (305) 392-4819", "+573053924819")]
    [InlineData("00573053924819", "+573053924819")]
    [InlineData("573053924819", "+573053924819")]
    [InlineData("abc3053924819", null)]
    [InlineData("123", null)]
    public void Normalize_HandlesProfileAndMetaFormats(string input, string? expected) => Assert.Equal(expected, PatientPhone.Normalize(input));
}
