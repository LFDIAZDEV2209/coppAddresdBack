using System.Text.Json.Serialization;

namespace CoppAddresd.Application.Features.ProgramProgress.DTOs.League;

// ===========================================================================
// LEAGUE v1 — Contrato del paciente (GET /program/me/league, camelCase).
// Privacidad por diseño: la respuesta NUNCA contiene el id, nombre real o
// ciudad de otro participante; solo nicknames (seudónimos) o códigos
// anónimos derivados del id (hash determinista). El campo isMe se resuelve
// en el handler contra el patientId del JWT (nunca viaja en el caché).
// ===========================================================================

/// <summary>
/// Respuesta de <c>GET /program/me/league</c>: cohorte (estado o nacional,
/// según la k-rule) + bloque propio + las 4 categorías (racha/evo/adh/clin).
/// <c>computedAt</c> es el momento (UTC) en que se computó el cohorte (el
/// cliente renderiza "actualizado hace X"); en cache hit es el del cómputo
/// original, no el de la request.
/// </summary>
public sealed record LeagueResponseDto(
    [property: JsonPropertyName("cohort")] LeagueCohortDto Cohort,
    [property: JsonPropertyName("me")] LeagueMeDto Me,
    [property: JsonPropertyName("categories")] LeagueCategoriesDto Categories);

/// <summary>Alcance del cohorte usado: <c>state</c> o <c>national</c> (k-rule: &lt;10 opt-in en el estado → nacional).</summary>
public sealed record LeagueCohortDto(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("stateCode")] string? StateCode,
    [property: JsonPropertyName("participants")] int Participants,
    [property: JsonPropertyName("computedAt")] DateTime ComputedAt);

/// <summary>Estado propio del paciente en la liga (preferencias persistidas, leídas frescas — nunca cacheadas).</summary>
public sealed record LeagueMeDto(
    [property: JsonPropertyName("optedIn")] bool OptedIn,
    [property: JsonPropertyName("nickname")] string? Nickname);

/// <summary>Las 4 categorías de la liga (nombres wire fijos: racha/evo/adh/clin).</summary>
public sealed record LeagueCategoriesDto(
    [property: JsonPropertyName("racha")] LeagueCategoryDto Racha,
    [property: JsonPropertyName("evo")] LeagueCategoryDto Evo,
    [property: JsonPropertyName("adh")] LeagueCategoryDto Adh,
    [property: JsonPropertyName("clin")] LeagueCategoryDto Clin);

/// <summary>
/// Categoría del ranking: top 10 por valor DESC (desempate display ASC) + el
/// propio ranking. <c>entries</c> puede traer 11 filas: las 10 mejores + la
/// fila propia real cuando el paciente está fuera del top 10 (opt-in).
/// <c>myRank</c>/<c>myValue</c> null cuando el paciente no tiene valor en la
/// categoría o no está opt-in.
/// </summary>
public sealed record LeagueCategoryDto(
    [property: JsonPropertyName("entries")] IReadOnlyList<LeagueEntryDto> Entries,
    [property: JsonPropertyName("myRank")] int? MyRank,
    [property: JsonPropertyName("myValue")] int? MyValue,
    [property: JsonPropertyName("totalParticipants")] int TotalParticipants);

/// <summary>Fila del ranking. <c>display</c> = nickname o código anónimo (2 letras + 4 dígitos, hash SHA-256 del id).</summary>
public sealed record LeagueEntryDto(
    [property: JsonPropertyName("position")] int Position,
    [property: JsonPropertyName("display")] string Display,
    [property: JsonPropertyName("isMe")] bool IsMe,
    [property: JsonPropertyName("value")] int Value);

/// <summary>Estado persistido de las preferencias (PUT /program/me/league-preferences).</summary>
public sealed record LeaguePreferencesDto(
    [property: JsonPropertyName("optedIn")] bool OptedIn,
    [property: JsonPropertyName("nickname")] string? Nickname);

// ===========================================================================
// LEAGUE v1 — Payload de CACHÉ (cohorte compartido por alcance).
// Se serializa en Valkey (erp:league:{state|ALL}:v1) con TTL 5 min y NO
// contiene datos por-petición: sin isMe, sin bloque me. PatientId vive SOLO
// aquí (infraestructura server-side) para que el handler pueda marcar isMe
// contra el JWT en cada request; la respuesta wire jamás lo serializa.
// ===========================================================================

/// <summary>Cohorte cacheado: alcance, participantes y categorías completas (ordenadas).</summary>
public sealed record LeagueCohortCacheDto(
    string Scope,
    string? StateCode,
    int Participants,
    DateTime ComputedAt,
    LeagueCategoriesCacheDto Categories);

/// <summary>Las 4 categorías del cohorte cacheado (orden interno fijo).</summary>
public sealed record LeagueCategoriesCacheDto(
    LeagueCategoryCacheDto Racha,
    LeagueCategoryCacheDto Evo,
    LeagueCategoryCacheDto Adh,
    LeagueCategoryCacheDto Clin);

/// <summary>
/// Categoría del cohorte cacheado: TODOS los participantes con valor (no solo
/// top 10) y su posición exacta — el recorte a top 10 y el ranking propio se
/// resuelven por request. <c>TotalParticipants</c> = participantes con valor.
/// </summary>
public sealed record LeagueCategoryCacheDto(
    IReadOnlyList<LeagueEntryCacheDto> Entries,
    int TotalParticipants);

/// <summary>
/// Participante ordenado del cohorte. <c>PatientId</c> es INTERNO del caché
/// (merge de isMe en el handler): nunca se serializa en la respuesta wire.
///
/// TRIPWIRE (no usar [JsonIgnore] aquí): el payload SÍ se serializa en
/// Valkey (<c>ValkeyCacheService</c>) y el merge de isMe necesita el id tras
/// la deserialización — un JsonIgnore rompería el round-trip del caché. La
/// protección real es la separación de tipos: la respuesta usa
/// <see cref="LeagueEntryDto"/> (sin id); devolver este DTO cacheado desde
/// un endpoint rompería el test de seudonimización.
/// </summary>
public sealed record LeagueEntryCacheDto(
    Guid PatientId,
    int Position,
    string Display,
    int Value);

/// <summary>
/// Contexto de la liga del paciente (resultado del repositorio, no cacheado):
/// preferencias + estado resoluble + existencia de inscripción activa.
/// </summary>
public sealed record LeagueContext(
    Guid PatientId,
    bool OptedIn,
    string? Nickname,
    string? StateCode);

/// <summary>
/// Fila cruda del cohorte (resultado del repositorio): un paciente opt-in con
/// inscripción activa y sus 4 valores persistidos. Cualquier valor null =
/// el paciente NO participa de esa categoría (nunca 0).
/// </summary>
public sealed record LeagueParticipantRow(
    Guid PatientId,
    string? Nickname,
    int? Streak,
    int? Evo,
    int? Adherence,
    int? Clinical);