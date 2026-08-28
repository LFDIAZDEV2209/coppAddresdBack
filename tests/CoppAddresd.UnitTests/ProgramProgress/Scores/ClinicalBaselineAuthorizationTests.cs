using CoppAddresd.Application.Features.ProgramProgress.DTOs.Scores;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Application.Services.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CoppAddresd.UnitTests.ProgramProgress.Scores;

/// <summary>
/// Guardia de autoría clínica de <c>app.clinical_baselines</c> (AC-22, SPEC
/// §13.1.2/§13.6): <c>set_by</c> es obligatorio y el llamador debe tener un
/// rol clínico (<c>Physician</c>, <c>Nutritionist</c>, <c>Psychologist</c>,
/// <c>ClinicalDirector</c> o <c>Admin</c>); un paciente auto-asignándose →
/// 403 FORBIDDEN. Las dos guardias corren ANTES de tocar la BD, por lo que se
/// prueban en unit sin PostgreSQL (el flujo exitoso y los límites de
/// <c>target_value</c> se cubren en los tests de integración).
/// </summary>
public sealed class ClinicalBaselineAuthorizationTests
{
    /// <summary>
    /// Repositorio sin BD real: las guardias AC-22 rechazan antes de la
    /// primera query; el ctor solo lee configuración en memoria.
    /// </summary>
    private static IProgramRepository Repository()
        => new ProgramRepository(
            new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options),
            new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Program:Streak:FreezeGrantEveryPerfectDays"] = "7" })
                .Build());

    private static ClinicalBaselineWrite Write(
        Guid? setBy = null, decimal value = 82.5m, decimal? targetValue = null)
        => new(
            PatientId: Guid.NewGuid(),
            MetricId: Guid.NewGuid(),
            Value: value,
            UnitId: Guid.NewGuid(),
            FavorableDirection: FavorableDirection.LowerIsBetter,
            MeasuredAt: new DateOnly(2026, 9, 14),
            SetBy: setBy ?? Guid.NewGuid(),
            TargetValue: targetValue);

    /// <summary>AC-22: <c>set_by</c> vacío se rechaza (422, nunca se infiere).</summary>
    [Fact]
    public async Task Upsert_SetByVacio_LanzaUnprocessable()
    {
        var ex = await Assert.ThrowsAsync<UnprocessableEntityException>(
            () => Repository().UpsertClinicalBaselineAsync(
                Write(setBy: Guid.Empty), callerRoles: ["Physician"]));

        Assert.Contains("SET_BY_REQUIRED", ex.Message);
    }

    /// <summary>
    /// AC-22: un paciente (rol sin capacidad clínica) auto-asignándose una
    /// línea base → 403 FORBIDDEN con semántica exacta.
    /// </summary>
    [Fact]
    public async Task Upsert_LlamadorPaciente_LanzaForbidden403()
    {
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => Repository().UpsertClinicalBaselineAsync(
                Write(), callerRoles: ["Patient"]));

        Assert.Contains("BASELINE_SET_BY_REQUIRES_CLINICIAN", ex.Message);
    }

    /// <summary>AC-22: un rol clínico NO pasa la guardia (p. ej. Nurse).</summary>
    [Fact]
    public async Task Upsert_LlamadorConRolNoClinico_LanzaForbidden()
    {
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => Repository().UpsertClinicalBaselineAsync(
                Write(), callerRoles: ["Nurse", "Receptionist"]));

        Assert.Contains("BASELINE_SET_BY_REQUIRES_CLINICIAN", ex.Message);
    }

    /// <summary>AC-22: la guardia es por ROL, no por set_by: con rol clínico no se lanza aquí.</summary>
    [Theory]
    [InlineData("Physician")]
    [InlineData("Nutritionist")]
    [InlineData("Psychologist")]
    [InlineData("ClinicalDirector")]
    [InlineData("Admin")]
    public async Task Upsert_LlamadorClinico_PasaLaGuardiaDeRol(string role)
    {
        // La guardia de rol no lanza: la validación avanza y falla en la
        // consulta de métrica (BD no disponible). Lo importante: NO es
        // ForbiddenException (el rol clínico autoriza).
        var ex = await Record.ExceptionAsync(
            () => Repository().UpsertClinicalBaselineAsync(Write(), callerRoles: [role]));

        Assert.NotNull(ex);
        Assert.IsNotType<ForbiddenException>(ex);
        Assert.IsNotType<UnprocessableEntityException>(ex);
    }
}