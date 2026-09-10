using System.ComponentModel.DataAnnotations.Schema;

namespace CoppAddresd.Community.Entities;

/// <summary>Club temático de la comunidad (salud, deporte, bienestar, etc.).</summary>
public sealed class Club
{
    public Guid Id { get; set; }

    /// <summary>Identificador URL único del club (minúsculas, sin espacios).</summary>
    public string Slug { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string Description { get; set; } = default!;

    /// <summary>Reglas de convivencia del club.</summary>
    public string[] Rules { get; set; } = [];

    /// <summary>Objetivos del club.</summary>
    public string[] Objectives { get; set; } = [];

    /// <summary>Categoría del catálogo (Salud, Deporte, Bienestar, …).</summary>
    public string Category { get; set; } = default!;

    /// <summary>Etiquetas de búsqueda del club.</summary>
    public string[] Tags { get; set; } = [];

    /// <summary>Clave de la portada en el storage (community/covers/…).</summary>
    public string? CoverKey { get; set; }

    /// <summary>Clave del logo en el storage (community/logos/…).</summary>
    public string? LogoKey { get; set; }

    public ClubVisibility Visibility { get; set; } = ClubVisibility.Publico;

    /// <summary>Capacidad máxima de miembros (null = sin límite).</summary>
    public int? MaxMembers { get; set; }

    public ClubStatus Status { get; set; } = ClubStatus.Activo;

    /// <summary>Perfil creador del club (queda como Admin).</summary>
    public Guid CreatedByProfileId { get; set; }

    /// <summary>Club de sistema (sembrado por la plataforma, no editable vía UI).</summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public Profile? CreatedByProfile { get; set; }

    public ICollection<ClubMember> Members { get; set; } = [];
    public ICollection<ClubInvitation> Invitations { get; set; } = [];
    public ICollection<ClubEvent> Events { get; set; } = [];
    public ICollection<LiveSession> LiveSessions { get; set; } = [];
    public ICollection<ClubNotification> Notifications { get; set; } = [];
    public ICollection<ModerationLog> ModerationLogs { get; set; } = [];

    /// <summary>Cantidad de miembros (se rellena en memoria en la consulta clubs).</summary>
    [NotMapped]
    public int MemberCount { get; set; }
}