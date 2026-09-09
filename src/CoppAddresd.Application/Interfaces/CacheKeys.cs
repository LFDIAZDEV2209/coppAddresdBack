using System.Security.Cryptography;
using System.Text;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Convención de claves de caché de la capa de aplicación. Formato
/// <c>&lt;dominio&gt;:&lt;nombre&gt;[:&lt;alcance&gt;]:v&lt;n&gt;</c> (el prefijo de
/// servicio lo añade la implementación). El sufijo de versión permite
/// invalidar globalmente un dominio subiendo la versión. Ver
/// docs/modules/cache/README.md para la tabla completa de claves.
/// </summary>
public static class CacheKeys
{
    /// <summary>Versión actual de todas las claves (bump para invalidar un dominio entero).</summary>
    public const string Version = "v1";

    /// <summary>TTL estándar de catálogos: lectura masiva, escritura rara.</summary>
    public static readonly TimeSpan CatalogTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Rango de TTL de stats (segundos): jitter anti-stampede para que las
    /// claves no expiren sincronizadas (design D7 del change valkey-cache).
    /// </summary>
    public static readonly TimeSpan StatsTtlMin = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan StatsTtlMax = TimeSpan.FromSeconds(60);

    /// <summary>Clave de un catálogo completo: <c>catalog:blood-types:v1</c>.</summary>
    public static string Catalog(string name) => $"catalog:{name}:{Version}";

    /// <summary>
    /// Clave de un catálogo particionado por un id (ej. estados por país):
    /// <c>catalog:states:{countryId}:v1</c>.
    /// </summary>
    public static string Catalog(string name, Guid scopeId) =>
        $"catalog:{name}:{scopeId}:{Version}";

    /// <summary>
    /// Clave de un agregado de stats, hashada por alcance (clínica, profesional
    /// propio, organización): dos usuarios con distinto alcance nunca comparten
    /// clave — el scoping lo resuelve el backend, nunca se filtra entre keys.
    /// </summary>
    public static string Stats(string name, string scopeHash) =>
        $"stats:{name}:{scopeHash}:{Version}";

    /// <summary>Hash estable del alcance para claves de stats (SHA-256, hex).</summary>
    public static string HashScope(params string?[] parts)
    {
        var raw = string.Join("|", parts);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    /// <summary>TTL con jitter para stats dentro del rango configurado.</summary>
    public static TimeSpan StatsTtl() =>
        TimeSpan.FromSeconds(
            Random.Shared.Next((int)StatsTtlMin.TotalSeconds, (int)StatsTtlMax.TotalSeconds + 1)
        );

    /// <summary>
    /// TTL del cohorte de la liga del paciente (LEAGUE v1): corto (5 min)
    /// porque la liga es social y el paciente espera ver su racha/puntaje con
    /// retraso mínimo. Al cambiar las preferencias DEL PACIENTE se invalidan
    /// sus claves (<c>league:{estado}</c> + <c>league:ALL</c>): la revocación
    /// del opt-in es inmediata (privacidad). Los cambios de OTROS pacientes
    /// no invalidan nada (el TTL corto absorbe; sin invalidaciones fan-out).
    /// </summary>
    public static readonly TimeSpan LeagueTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Clave del cohorte de la liga: <c>league:{stateCode|ALL}:v1</c> (el
    /// prefijo del servicio lo añade la implementación: <c>erp:league:CA:v1</c>).
    /// Compartida por todos los pacientes del mismo alcance: contiene el
    /// cohorte SIN datos por-petición (sin <c>isMe</c>, sin bloque <c>me</c>);
    /// el merge con la identidad del JWT ocurre en el handler, por request.
    /// </summary>
    public static string League(string? stateCode) =>
        $"league:{(string.IsNullOrWhiteSpace(stateCode) ? "ALL" : stateCode.Trim().ToUpperInvariant())}:{Version}";

    /// <summary>
    /// TTL del historial de puntajes del paciente (scores-history): 5 min. El
    /// móvil re-consulta la serie en cada visita a la pestaña Evolución; el
    /// dato solo cambia al calcularse una semana, así que el TTL corto absorbe
    /// el staleness sin invalidaciones. Fail-open garantizado por la
    /// abstracción (ver docs/modules/cache/README.md).
    /// </summary>
    public static readonly TimeSpan ScoresHistoryTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Clave del historial de puntajes: <c>scores-history:{patientId}:v1</c>
    /// (prefijo del servicio: <c>erp:scores-history:{patientId}:v1</c>). Clave
    /// POR PACIENTE — datos propios, nunca compartida entre pacientes (a
    /// diferencia del cohorte de la liga, el scoping lo garantiza la propia
    /// clave: ningún payload contiene datos de otros pacientes).
    /// </summary>
    public static string ScoresHistory(Guid patientId) =>
        $"scores-history:{patientId}:{Version}";

    /// <summary>
    /// TTL del historial de métricas clínicas del paciente (metrics-history):
    /// 5 min. Las completaciones de signos vitales cambian el dato con un lag
    /// ≤ TTL — mismo tradeoff que scores-history (documentado en
    /// docs/modules/cache/README.md).
    /// </summary>
    public static readonly TimeSpan MetricsHistoryTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Clave del historial de métricas: <c>metrics-history:{patientId}:v1</c>
    /// (prefijo del servicio: <c>erp:metrics-history:{patientId}:v1</c>).
    /// Clave POR PACIENTE — datos propios, nunca compartida (mismo criterio
    /// que scores-history).
    /// </summary>
    public static string MetricsHistory(Guid patientId) =>
        $"metrics-history:{patientId}:{Version}";
}
