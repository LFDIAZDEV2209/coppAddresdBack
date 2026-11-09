using System.Globalization;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using MediatR;

namespace CoppAddresd.Application.Features.Sos;

public sealed record ResolveSosLocationLinkQuery(string Token) : IRequest<string?>;

/// <summary>
/// El enlace solo revela la ubicación capturada durante 24 horas. Cancelar
/// revoca el enlace; la atención clínica no impide que el contacto lo consulte.
/// </summary>
public sealed class ResolveSosLocationLinkHandler(
    ISosLocationLinkService links,
    ISosAlertRepository repository,
    TimeProvider clock
) : IRequestHandler<ResolveSosLocationLinkQuery, string?>
{
    public async Task<string?> Handle(ResolveSosLocationLinkQuery request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var alert = links.TryReadToken(request.Token, out var id)
            ? await repository.GetByIdAsync(id, ct)
            : links.TryGetShortCodeHash(request.Token, out var hash)
                ? await repository.GetByLocationLinkHashAsync(hash, ct)
                : null;
        if (alert is null || alert.Status == SosAlertStatus.Cancelada) return null;
        var createdAt = new DateTimeOffset(DateTime.SpecifyKind(alert.CreatedAt, DateTimeKind.Utc));
        var age = clock.GetUtcNow() - createdAt;
        if (age < TimeSpan.FromMinutes(-1) || age >= TimeSpan.FromHours(24)
            || alert.Latitude is not double latitude || alert.Longitude is not double longitude
            || !double.IsFinite(latitude) || !double.IsFinite(longitude)
            || Math.Abs(latitude) > 90 || Math.Abs(longitude) > 180) return null;
        return string.Create(CultureInfo.InvariantCulture,
            $"https://www.google.com/maps/search/?api=1&query={latitude:F6},{longitude:F6}");
    }
}
