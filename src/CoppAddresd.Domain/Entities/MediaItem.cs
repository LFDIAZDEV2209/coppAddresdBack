using CoppAddresd.Domain.Enums;

namespace CoppAddresd.Domain.Entities;

/// <summary>
/// Medio multimedia (podcast, video, audio) que el equipo clínico sube desde
/// el ERP para que los pacientes lo consuman como lecciones diarias. La entidad
/// guarda únicamente la metadata; el archivo reposa en el storage de objetos
/// (S3) bajo <see cref="StorageKey"/>.
/// </summary>
public sealed class MediaItem
{
    public Guid Id { get; set; }

    public string Title { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Autor/a del contenido, nombre visible en la lección.</summary>
    public string Author { get; set; } = default!;

    public MediaType MediaType { get; set; }

    /// <summary>Categoría temática de la lección.</summary>
    public MediaCategory Category { get; set; }

    /// <summary>Clave del objeto en el storage (convención S3), ej. <c>media/podcasts/{id}.mp3</c>.</summary>
    public string StorageKey { get; set; } = default!;

    /// <summary>Clave de la imagen de portada en el storage (opcional), ej. <c>media/thumbnails/{id}.jpg</c>.</summary>
    public string? ThumbnailKey { get; set; }

    /// <summary>Content-Type del archivo, ej. <c>audio/mpeg</c>.</summary>
    public string? ContentType { get; set; }

    public long? FileSizeBytes { get; set; }

    public int? DurationSecs { get; set; }

    public MediaStatus Status { get; set; }

    /// <summary>Orden de la lección dentro de la secuencia diaria.</summary>
    public int SortOrder { get; set; }

    /// <summary>Día de la lección dentro del programa.</summary>
    public int Day { get; set; }

    /// <summary>Mes de la lección dentro del programa.</summary>
    public int Month { get; set; }

    /// <summary>Momento en que pasó a Published. Null mientras sea Draft.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>AspNetUsers.Id del creador (futuro Identity). Null hasta que exista.</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Capítulos interactivos con marca de tiempo en segundos.</summary>
    public List<MediaChapterDto> Chapters { get; set; } = [];

    /// <summary>Puntos clave o conclusiones clínicas del medio.</summary>
    public List<string> Takeaways { get; set; } = [];
}

/// <summary>Capítulo multimedia con marca de tiempo en segundos.</summary>
public record MediaChapterDto(int AtSeconds, string Label);
