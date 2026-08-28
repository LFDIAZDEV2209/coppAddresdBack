using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using CoppAddresd.Telemedicine.Infrastructure.Cache;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.Infrastructure.Services;

/// <summary>
/// Cliente de los internal endpoints de datos de referencia del backend
/// (<c>/api/v1/internal/telemedicine/*</c>, header <c>X-Internal-Key</c>).
/// La base URL, timeout y header se configuran en el HttpClient registrado en
/// DI (sección <c>Backend</c>). Devuelve <c>null</c> ante 404 (el recurso no
/// existe en el ERP) y traduce errores transitorios del backend a una
/// excepción de dominio comprensible. Cache-aside DISTRIBUIDO con TTL 10 min
/// por referencia (profesional/paciente/especialidad/sede): las pantallas de
/// agenda y salas repiten las mismas referencias por cita; datos catalogables
/// sin PHI a nivel fila. Fail-open: con Valkey caído, el backend se consulta
/// siempre. Los 404 NO se cachean (evita envenenar el caché con recursos que
/// aparecen después).
/// </summary>
public sealed class AppointmentReferenceDataService(
    HttpClient httpClient,
    ICacheService cache,
    ILogger<AppointmentReferenceDataService> logger
) : IAppointmentReferenceDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web);

    /// <summary>TTL de referencias del backend: mutaciones poco frecuentes, tolerancia documentada.</summary>
    private static readonly TimeSpan ReferenceTtl = TimeSpan.FromMinutes(10);

    private const string KeyVersion = "v1";

    public async Task<ProfessionalRefDto?> GetProfessionalAsync(
        Guid professionalId,
        CancellationToken ct = default
    ) => await GetCachedAsync<ProfessionalRefDto>($"professionals/{professionalId}", ct);

    public async Task<PatientRefDto?> GetPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    ) => await GetCachedAsync<PatientRefDto>($"patients/{patientId}", ct);

    public async Task<ProfessionalRefDto?> GetProfessionalByUserIdAsync(
        Guid userId,
        CancellationToken ct = default
    ) => await GetCachedAsync<ProfessionalRefDto>($"professionals/by-user/{userId}", ct);

    public async Task<PatientRefDto?> GetPatientByUserIdAsync(
        Guid userId,
        CancellationToken ct = default
    ) => await GetCachedAsync<PatientRefDto>($"patients/by-user/{userId}", ct);

    public async Task<SpecialtyRefDto?> GetSpecialtyAsync(
        Guid specialtyId,
        CancellationToken ct = default
    ) => await GetCachedAsync<SpecialtyRefDto>($"specialties/{specialtyId}", ct);

    public async Task<LocationRefDto?> GetLocationAsync(
        Guid locationId,
        CancellationToken ct = default
    ) => await GetCachedAsync<LocationRefDto>($"locations/{locationId}", ct);

    private Task<T?> GetCachedAsync<T>(string referencePath, CancellationToken ct)
        where T : class =>
        cache.GetOrCreateAsync<T?>(
            $"ref:{referencePath}:{KeyVersion}",
            ReferenceTtl,
            token => GetAsync<T>($"/api/v1/internal/telemedicine/{referencePath}", token),
            ct
        );

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
        where T : class
    {
        try
        {
            var response = await httpClient.GetAsync(path, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogError("Timeout del backend al leer {Path}", path);
            throw new Application.Exceptions.UpstreamUnavailableException(
                "El backend no respondió a tiempo al validar datos de referencia."
            );
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(
                ex,
                "El backend rechazó la lectura de datos de referencia en {Path}",
                path
            );
            throw new Application.Exceptions.UpstreamUnavailableException(
                $"No se pudo validar los datos de referencia contra el backend ({path})."
            );
        }
    }
}
