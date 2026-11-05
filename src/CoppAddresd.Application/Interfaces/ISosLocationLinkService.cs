using CoppAddresd.Domain.Entities;

namespace CoppAddresd.Application.Interfaces;

/// <summary>Enlaces de ubicación compartidos, sin coordenadas ni identidad en la URL.</summary>
public interface ISosLocationLinkService
{
    string? CreateLink(SosAlert alert);
    bool TryReadToken(string? token, out Guid alertId);
}
