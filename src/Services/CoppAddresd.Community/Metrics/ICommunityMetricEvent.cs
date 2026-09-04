namespace CoppAddresd.Community.Metrics;

/// <summary>Interfaz marcadora para todos los eventos de métricas de Comunidad.</summary>
public interface ICommunityMetricEvent;

/// <summary>Se emite cuando se crea una publicación (de cualquier tipo).</summary>
public sealed record PostCreatedMetricEvent(
    Guid PostId,
    DateOnly MetricDate,
    string PostType,   // "texto", "imagen", "video", "poll", "logro", "general" — normalizado desde PostType (Encuesta → "poll")
    int HourOfDay      // DateTime.UtcNow.Hour — para horas pico
) : ICommunityMetricEvent;

/// <summary>Se emite cuando se crea un comentario en cualquier publicación.</summary>
public sealed record CommentCreatedMetricEvent(
    Guid CommentId,
    DateOnly MetricDate,
    int HourOfDay
) : ICommunityMetricEvent;

/// <summary>Se emite cuando se agrega un like a una publicación o a un comentario.</summary>
public sealed record LikeAddedMetricEvent(
    Guid TargetId,
    DateOnly MetricDate,
    int HourOfDay
) : ICommunityMetricEvent;

/// <summary>
/// Se emite cuando se quita un like. <c>MetricDate</c> es la fecha ORIGINAL del
/// like (el día en que se agregó), para decrementar el bucket correcto del
/// rollup: un like agregado el día X y removido el día Y debe decrementar el
/// día X, no el día Y.
/// </summary>
public sealed record LikeRemovedMetricEvent(
    Guid TargetId,
    DateOnly MetricDate  // Fecha original del like (día en que se agregó)
) : ICommunityMetricEvent;

/// <summary>Se emite cuando se crea un repost.</summary>
public sealed record RepostCreatedMetricEvent(
    Guid RepostId,
    DateOnly MetricDate,
    int HourOfDay
) : ICommunityMetricEvent;

/// <summary>
/// Se emite cuando se elimina un repost. <c>MetricDate</c> es la fecha ORIGINAL
/// del repost (el día en que se creó) y <c>HourOfDay</c> la hora original, para
/// decrementar los buckets correctos de <c>reposts_count</c> y <c>hourly_activity</c>.
/// </summary>
public sealed record RepostRemovedMetricEvent(
    Guid RepostId,
    DateOnly MetricDate,  // Fecha original del repost (día en que se creó)
    int HourOfDay         // Hora original del repost (para hourly_activity)
) : ICommunityMetricEvent;