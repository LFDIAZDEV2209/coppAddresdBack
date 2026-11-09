using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Enlaces de ubicación compartidos, sin coordenadas ni identidad en la URL.</summary>
public interface ISosLocationLinkService
{
    /// <summary>Prepara el digest de la capacidad corta antes del commit de alerta/outbox.</summary>
    void Prepare(SosAlert alert);
    string? CreateLink(SosAlert alert);
    bool TryReadToken(string? token, out Guid alertId);
    bool TryGetShortCodeHash(string? token, out string hash);
}
