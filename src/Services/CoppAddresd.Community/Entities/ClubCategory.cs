namespace CoppAddresd.Community.Entities;

/// <summary>Categoría del catálogo de clubes (administrada por el ERP).</summary>
public sealed class ClubCategory
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string Slug { get; set; } = default!;

    /// <summary>Icono/emoji o clave visual de la categoría.</summary>
    public string? Icon { get; set; }
}