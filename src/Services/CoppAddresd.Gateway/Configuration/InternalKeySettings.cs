namespace CoppAddresd.Gateway.Configuration;

/// <summary>
/// Configuración de la clave interna del gateway (bind a la sección <c>InternalKey</c>).
/// <c>Enabled</c> permite deshabilitar el bloqueo sin borrar la clave; <c>Key</c> es
/// el secreto compartido que deben enviar los servicios en el header <c>X-Internal-Key</c>.
/// El constructor sin argumentos existe para que <c>IOptions&lt;T&gt;</c> (que activa la
/// instancia por defecto y luego aplica el binder de configuración) pueda construirlo.
/// </summary>
public sealed record InternalKeySettings(string Key, bool Enabled)
{
    public InternalKeySettings() : this("", true) { }
}
