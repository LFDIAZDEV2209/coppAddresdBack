using CoppAddresd.Telemedicine.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Telemedicine.Application.Features.Telemedicine;

/// <summary>
/// Utilidades compartidas de las notificaciones F2: envío best-effort y armado
/// de los datos de navegación del contrato (<c>data</c>). Telemedicina emite
/// el evento; el backend hace la entrega. Un fallo de notificación se registra
/// y JAMÁS interrumpe el flujo de negocio que la originó.
/// </summary>
internal static class NotificationSupport
{
    /// <summary>
    /// Envía la notificación si hay notificador configurado; captura cualquier
    /// fallo del cliente (red, backend, serialización) y lo registra. La
    /// cancelación de la petición en curso se propaga para respetar el apagado.
    /// </summary>
    public static async Task TrySendAsync(
        ITelemedicineNotifier? notifier,
        ILogger? logger,
        TelemedicineNotification notification,
        CancellationToken ct
    )
    {
        if (notifier is null)
        {
            return;
        }

        try
        {
            await notifier.SendAsync(notification, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "No se pudo notificar al usuario {UserId} ({Title}).",
                notification.UserId,
                notification.Title
            );
        }
    }

    /// <summary>
    /// Datos de navegación del contrato (se omiten las claves sin valor): el
    /// backend los reenvía a la app móvil/ERP sin interpretarlos.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Data(
        Guid? appointmentId = null,
        Guid? requestId = null,
        string? screen = null
    )
    {
        var data = new Dictionary<string, string>();
        if (appointmentId is { } id)
        {
            data["appointmentId"] = id.ToString();
        }
        if (requestId is { } request)
        {
            data["requestId"] = request.ToString();
        }
        if (!string.IsNullOrWhiteSpace(screen))
        {
            data["screen"] = screen;
        }
        return data;
    }
}
