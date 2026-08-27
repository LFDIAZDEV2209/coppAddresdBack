namespace CoppAddresd.Community.Entities;

/// <summary>Canal de red social de la comunidad (TikTok, Instagram, Facebook, etc.).</summary>
public sealed class NetworkChannel
{
    public Guid Id { get; set; }

    /// <summary>Nombre del canal (máx 100).</summary>
    public string Name { get; set; } = default!;

    /// <summary>Handle o usuario del canal (máx 100, nullable).</summary>
    public string? Handle { get; set; }

    /// <summary>Cantidad de seguidores.</summary>
    public int Followers { get; set; }

    /// <summary>Color hexadecimal del canal (máx 20, ej. "#000000").</summary>
    public string Color { get; set; } = default!;

    /// <summary>Orden de visualización.</summary>
    public int SortOrder { get; set; }

    public ICollection<NetworkGrowthPoint> GrowthPoints { get; set; } = [];
}

/// <summary>Punto de crecimiento mensual de un canal de red social.</summary>
public sealed class NetworkGrowthPoint
{
    public Guid Id { get; set; }

    /// <summary>FK al canal (cascade delete).</summary>
    public Guid ChannelId { get; set; }

    /// <summary>Mes en formato "yyyy-MM" (máx 7).</summary>
    public string Month { get; set; } = default!;

    /// <summary>Valor de seguidores en ese mes.</summary>
    public int Value { get; set; }

    public NetworkChannel? Channel { get; set; }
}
