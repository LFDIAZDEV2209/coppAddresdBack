using System.Security.Cryptography;
using CoppAddresd.Application.Features.ProgramProgress.DTOs.League;

namespace CoppAddresd.Application.Services.ProgramProgress;

/// <summary>
/// Construcción pura del cohorte de la liga del paciente (LEAGUE v1): sin
/// I/O, determinista y unit-testable. Recibe las filas crudas del repositorio
/// y produce el payload cacheable (ordenado, posicionado, sin datos por
/// petición). La k-rule (estado con &lt;10 opt-in → cohorte nacional) y el
/// recorte a top 10 viven aquí/el handler: este builder resuelve el alcance
/// y ordena TODAS las filas; el handler recorta por request.
/// </summary>
public static class LeagueCohortBuilder
{
    /// <summary>k-rule: un estado con menos de esta cantidad de opt-in cae a cohorte nacional.</summary>
    public const int MinStateParticipants = 10;

    /// <summary>Entradas visibles por categoría (top 10); la fila propia se anexa si está fuera.</summary>
    public const int TopEntries = 10;

    /// <summary>
    /// Resuelve el alcance por la k-rule: &lt; <see cref="MinStateParticipants"/>
    /// opt-in en el estado → <c>national</c>; si no → <c>state</c>.
    /// </summary>
    public static string ResolveScope(int stateParticipantCount) =>
        stateParticipantCount < MinStateParticipants ? "national" : "state";

    /// <summary>
    /// Construye el cohorte cacheable a partir de las filas del alcance final:
    /// por categoría, filtra participantes SIN valor (excluidos, nunca 0),
    /// ordena por valor DESC + display ASC (desempate determinista) y asigna
    /// posiciones 1-based. <c>Participants</c> = tamaño del cohorte (opt-in
    /// con inscripción activa); <c>TotalParticipants</c> por categoría =
    /// participantes CON valor en esa categoría.
    /// </summary>
    public static LeagueCohortCacheDto Build(
        IReadOnlyList<LeagueParticipantRow> rows,
        string scope,
        string? stateCode,
        DateTime computedAt
    ) =>
        new(
            scope,
            string.IsNullOrWhiteSpace(stateCode) ? null : stateCode.Trim().ToUpperInvariant(),
            rows.Count,
            computedAt,
            new LeagueCategoriesCacheDto(
                BuildCategory(rows, r => r.Streak),
                BuildCategory(rows, r => r.Evo),
                BuildCategory(rows, r => r.Adherence),
                BuildCategory(rows, r => r.Clinical)
            )
        );

    private static LeagueCategoryCacheDto BuildCategory(
        IReadOnlyList<LeagueParticipantRow> rows,
        Func<LeagueParticipantRow, int?> valueOf
    )
    {
        var ordered = rows
            .Where(r => valueOf(r).HasValue)
            .Select(r => new LeagueEntryCacheDto(
                r.PatientId,
                0,
                DeriveDisplay(r),
                valueOf(r)!.Value
            ))
            .OrderByDescending(e => e.Value)
            .ThenBy(e => e.Display, StringComparer.OrdinalIgnoreCase)
            // Desempate FINAL por id: los nicknames duplicados son legales y
            // dos (value, display) idénticos no pueden caer al orden del heap
            // SQL / al orden de enumeración (determinismo total del ranking).
            .ThenBy(e => e.PatientId)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i] = ordered[i] with { Position = i + 1 };
        }

        return new LeagueCategoryCacheDto(ordered, ordered.Count);
    }

    /// <summary>
    /// Display del participante: su nickname cuando lo declaró; si no, un
    /// código anónimo ESTABLE derivado de su id (2 letras + 4 dígitos de
    /// SHA-256 — determinista, no secuencial, no reversible). Las colisiones
    /// son aceptables (display únicamente, nunca identidad).
    /// </summary>
    public static string DeriveDisplay(LeagueParticipantRow row) =>
        !string.IsNullOrWhiteSpace(row.Nickname) ? row.Nickname.Trim() : DeriveAnonymousDisplay(row.PatientId);

    private static string DeriveAnonymousDisplay(Guid patientId)
    {
        var hash = SHA256.HashData(patientId.ToByteArray());
        var l1 = (char)('A' + hash[0] % 26);
        var l2 = (char)('A' + hash[1] % 26);
        var digits = ((hash[2] << 8) | hash[3]) % 10000;
        return $"{l1}{l2}{digits:0000}";
    }
}