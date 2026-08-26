using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Application.ReferenceData;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Telemedicine.Infrastructure.Services;

/// <summary>
/// Cliente de los internal endpoints de datos de referencia del backend
/// (<c>/api/v1/internal/telemedicine/*</c>, header <c>X-Internal-Key</c>).
/// La base URL, timeout y header se configuran en el HttpClient registrado en
/// DI (sección <c>Backend</c>). Devuelve <c>null</c> ante 404 (el recurso no
/// existe en el ERP) y traduce errores transitorios del backend a una
/// excepción de dominio comprensible.
/// </summary>
public sealed class AppointmentReferenceDataService(
    HttpClient httpClient,
    ILogger<AppointmentReferenceDataService> logger) : IAppointmentReferenceDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerOptions.Web);

    public async Task<ProfessionalRefDto?> GetProfessionalAsync(
        Guid professionalId, CancellationToken ct = default)
        => await GetAsync<ProfessionalRefDto>($"/api/v1/internal/telemedicine/professionals/{professionalId}", ct);

    public async Task<PatientRefDto?> GetPatientAsync(
        Guid patientId, CancellationToken ct = default)
        => await GetAsync<PatientRefDto>($"/api/v1/internal/telemedicine/patients/{patientId}", ct);

    public async Task<ProfessionalRefDto?> GetProfessionalByUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await GetAsync<ProfessionalRefDto>($"/api/v1/internal/telemedicine/professionals/by-user/{userId}", ct);

    public async Task<PatientRefDto?> GetPatientByUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await GetAsync<PatientRefDto>($"/api/v1/internal/telemedicine/patients/by-user/{userId}", ct);

    public async Task<SpecialtyRefDto?> GetSpecialtyAsync(
        Guid specialtyId, CancellationToken ct = default)
        => await GetAsync<SpecialtyRefDto>($"/api/v1/internal/telemedicine/specialties/{specialtyId}", ct);

    public async Task<LocationRefDto?> GetLocationAsync(
        Guid locationId, CancellationToken ct = default)
        => await GetAsync<LocationRefDto>($"/api/v1/internal/telemedicine/locations/{locationId}", ct);

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
                "El backend no respondió a tiempo al validar datos de referencia.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "El backend rechazó la lectura de datos de referencia en {Path}", path);
            throw new Application.Exceptions.UpstreamUnavailableException(
                $"No se pudo validar los datos de referencia contra el backend ({path}).");
        }
    }
}
