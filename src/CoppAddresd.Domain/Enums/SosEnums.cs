namespace CoppAddresd.Domain.Enums;

/// <summary>
/// Estados del ciclo de vida de una alerta SOS (change sos-panic-real):
/// <c>Activa → Atendida</c> (staff ERP) / <c>Activa → Cancelada</c>
/// (paciente dueño). Ambas transiciones son TERMINALES: cualquier intento
/// posterior de transición debe rechazarse con 409.
/// </summary>
public enum SosAlertStatus
{
    Activa = 1,
    Atendida = 2,
    Cancelada = 3,
}

/// <summary>
/// Estado de un canal de notificación (SMS / push) de una alerta SOS:
/// <c>Pendiente</c> (creada, aún sin procesar), <c>Enviado</c>, <c>Fallido</c>
/// (proveedor rechazó o error), <c>Timeout</c> y <c>NoConfigurado</c>
/// (credenciales ausentes). Se persiste junto a la alerta y la fila de
/// deduplicación: los reintentos jamás reenvían un canal ya <c>Enviado</c>.
/// </summary>
public enum SosChannelStatus
{
    Pendiente = 1,
    Enviado = 2,
    Fallido = 3,
    Timeout = 4,
    NoConfigurado = 5,
}
