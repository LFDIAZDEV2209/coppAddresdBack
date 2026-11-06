using Amazon.GeoPlaces;
using Amazon.GeoPlaces.Model;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Opt-in: la dirección del punto GPS se obtiene en background, sin usar el domicilio.</summary>
public sealed class SosGeocodingSettings
{
    public bool Enabled { get; set; }
    public string Region { get; set; } = "us-east-2";
}

/// <summary>
/// Un snapshot por alerta, compartido por SMS/correo y sus reintentos. Presupuesto
/// total de 2 s, sin reintentos AWS. Un fallo jamás impide despachar el SOS.
/// No registra coordenadas, direcciones, cuerpo de respuesta ni excepciones del proveedor.
/// </summary>
public sealed class SosLocationEnricher(
    Func<IAmazonGeoPlaces> clientFactory,
    IOptions<SosGeocodingSettings> settings,
    AppDbContext db,
    ILogger<SosLocationEnricher> logger)
{
    public async Task EnrichAsync(SosAlert alert, CancellationToken ct)
    {
        if (alert.LocationLookupCompletedAt is not null) return;
        string? description = null;
        var validCapture = alert.LocationCapturedAt is null ||
            (alert.LocationCapturedAt > DateTime.UnixEpoch && alert.LocationCapturedAt <= alert.CreatedAt.AddMinutes(1));
        // AWS/HERE no permite almacenar geocódigos de Japón. Ventana conservadora:
        // no consultar en modo Storage en esa región; el mapa original sigue disponible.
        var storageRestrictedArea = alert.Latitude is >= 20 and <= 47 && alert.Longitude is >= 122 and <= 155;
        if (settings.Value.Enabled && validCapture && !storageRestrictedArea && SosMessageFormatting.MapsLink(alert) is not null)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                // WaitAsync acota también la resolución inicial de credenciales del SDK.
                var response = await Task.Run(async () => await clientFactory().ReverseGeocodeAsync(new ReverseGeocodeRequest
                {
                    QueryPosition = [alert.Longitude!.Value, alert.Latitude!.Value],
                    MaxResults = 1,
                    Language = "es",
                    IntendedUse = ReverseGeocodeIntendedUse.Storage,
                }, budget.Token), budget.Token).WaitAsync(budget.Token);
                var item = response.ResultItems?.FirstOrDefault();
                var address = item?.Address;
                if (address is not null)
                {
                    // Una calle cercana no demuestra que el paciente esté en ese edificio.
                    // GPS grueso/desconocido o resultado lejano: mostrar solo ciudad/área.
                    var street = alert.AccuracyMeters is >= 0 and <= 100 && item!.Distance is >= 0 and <= 150
                        ? Clean(string.Join(" ", new[] { address.Street, address.AddressNumber }.Where(v => !string.IsNullOrWhiteSpace(v))))
                        : null;
                    var city = Clean(address.Locality) ?? Clean(address.District) ?? Clean(address.SubRegion?.Name);
                    var parts = new[] { street, city, Clean(address.Region?.Name), Clean(address.Country?.Name) }
                        .Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase);
                    description = Clean(string.Join(", ", parts));
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("SOS dirección no disponible: timeout, alertId={AlertId}.", alert.Id);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning("SOS dirección no disponible: provider, alertId={AlertId}, exceptionType={Type}.", alert.Id, ex.GetType().Name);
            }
        }
        ct.ThrowIfCancellationRequested();
        var completedAt = DateTime.UtcNow;
        try
        {
            // Compare-and-set: si dos réplicas procesan la misma alerta, gana un solo snapshot.
            await db.SosAlerts.Where(a => a.Id == alert.Id && a.LocationLookupCompletedAt == null)
                .ExecuteUpdateAsync(update => update
                    .SetProperty(a => a.LocationDescription, description)
                    .SetProperty(a => a.LocationLookupCompletedAt, completedAt), ct);
            var saved = await db.SosAlerts.AsNoTracking().Where(a => a.Id == alert.Id)
                .Select(a => new { a.LocationDescription, a.LocationLookupCompletedAt }).SingleAsync(ct);
            alert.LocationDescription = saved.LocationDescription;
            alert.LocationLookupCompletedAt = saved.LocationLookupCompletedAt;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // La disponibilidad de esta mejora no condiciona los canales de emergencia.
            alert.LocationDescription = null;
            logger.LogWarning("SOS dirección no disponible: snapshot, alertId={AlertId}.", alert.Id);
        }
    }

    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        // Longitud total acotada para SMS y para la columna varchar(300).
        return normalized.Length <= 300 ? normalized : normalized[..297] + "…";
    }
}
