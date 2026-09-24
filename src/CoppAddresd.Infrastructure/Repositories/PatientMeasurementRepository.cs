using System.Globalization;
using System.Text;
using CoppAddresd.Application.Features.Measurements.Queries.GetMyMeasurements;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Repositories;

/// <summary>
/// Lectura paginada de mediciones clínicas propias (Fase 7, móvil).
/// Una sola query set-based sobre <c>app.clinical_measurements</c> con joins
/// de catálogo (métrica y unidad), orden estable
/// (<c>observed_at DESC, id DESC</c>) y cursor opaco keyset. El
/// <c>patient_id</c> ya viene resuelto del handler (anti-IDOR): este
/// repositorio nunca resuelve identidad, solo filtra por el id recibido.
/// </summary>
public sealed class PatientMeasurementRepository(AppDbContext dbContext)
    : IPatientMeasurementRepository
{
    /// <inheritdoc />
    public async Task<CursorPagedResult<MeasurementItemDto>> GetPagedAsync(
        Guid patientId,
        string[]? metricCodes,
        int pageSize,
        string? cursor,
        CancellationToken ct
    )
    {
        // Filtro de códigos normalizado a minúsculas para comparar
        // case-insensitive en SQL (LOWER(mm.code) IN (...)).
        string[]? lowerCodes = NormalizeCodes(metricCodes);

        IQueryable<Domain.Entities.ClinicalMeasurement> query = dbContext
            .ClinicalMeasurements.AsNoTracking()
            .Where(m => m.PatientId == patientId);

        if (lowerCodes is { Length: > 0 })
        {
            query = query.Where(m => lowerCodes.Contains(m.Metric!.Code.ToLower()));
        }

        // Cursor keyset: (observed_at, id) < (cursorObservedAt, cursorId) en
        // orden DESC. Sin cursor (null/vacío) = primera página.
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var (cursorObservedAt, cursorId) = DecodeCursor(cursor);
            var cursorUtc = cursorObservedAt.UtcDateTime;
            query = query.Where(m =>
                m.ObservedAt < cursorUtc
                || (m.ObservedAt == cursorUtc && m.Id.CompareTo(cursorId) < 0)
            );
        }

        // Orden estable obligatorio (índice ix_clinical_measurements_patient_observed
        // cubre patient_id + observed_at; el Id desempata filas con igual fecha).
        // Take(pageSize + 1): peek para HasNextPage sin COUNT (skill pagination).
        var rows = await query
            .OrderByDescending(m => m.ObservedAt)
            .ThenByDescending(m => m.Id)
            .Take(pageSize + 1)
            .Select(m => new MeasurementItemDto(
                m.Id,
                m.Metric!.Code,
                m.Metric!.Name,
                m.Value,
                m.Unit!.Code,
                m.Unit!.Symbol,
                new DateTimeOffset(DateTime.SpecifyKind(m.ObservedAt, DateTimeKind.Utc)),
                m.Source
            ))
            .ToListAsync(ct);

        // Peek: si vino la fila extra hay siguiente página.
        if (rows.Count > pageSize)
        {
            var items = rows.Take(pageSize).ToList();
            var last = items[^1];
            return new CursorPagedResult<MeasurementItemDto>(
                items,
                EncodeCursor(last.ObservedAt, last.Id),
                HasNextPage: true
            );
        }

        return new CursorPagedResult<MeasurementItemDto>(
            rows,
            NextCursor: null,
            HasNextPage: false
        );
    }

    /// <summary>
    /// Normaliza el filtro de códigos a minúsculas invariantes, sin vacíos ni
    /// duplicados. Null/vacío → null (sin filtro).
    /// </summary>
    private static string[]? NormalizeCodes(string[]? codes)
    {
        if (codes is null || codes.Length == 0)
        {
            return null;
        }

        var normalized = codes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalized.Length == 0 ? null : normalized;
    }

    /// <summary>
    /// Codifica el cursor opaco: Base64(UTF-8) de
    /// <c>observedAt ISO-8601 round-trip + '|' + id</c>.
    /// </summary>
    private static string EncodeCursor(DateTimeOffset observedAt, Guid id)
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{observedAt.ToString("O", CultureInfo.InvariantCulture)}|{id:D}"
        );
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
    }

    /// <summary>
    /// Decodifica el cursor opaco. Formato inválido (no Base64, sin '|',
    /// fecha o Guid no parseables) → <see cref="ValidationException"/> (400
    /// por el middleware, sin exponer detalles internos).
    /// </summary>
    /// <exception cref="ValidationException">Cursor malformado.</exception>
    private static (DateTimeOffset ObservedAt, Guid Id) DecodeCursor(string cursor)
    {
        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Trim()));
        }
        catch (FormatException)
        {
            throw CursorInvalid();
        }
        catch (ArgumentException)
        {
            throw CursorInvalid();
        }

        var sep = payload.IndexOf('|');
        if (sep <= 0 || sep == payload.Length - 1)
        {
            throw CursorInvalid();
        }

        var datePart = payload[..sep];
        var idPart = payload[(sep + 1)..];

        if (
            !DateTimeOffset.TryParse(
                datePart,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var observedAt
            )
        )
        {
            throw CursorInvalid();
        }

        if (!Guid.TryParse(idPart, out var id))
        {
            throw CursorInvalid();
        }

        return (observedAt, id);
    }

    /// <summary>Construye la excepción controlada de cursor malformado (400).</summary>
    private static ValidationException CursorInvalid() =>
        new([
            new ValidationFailure("cursor", "CURSOR_INVALID: el cursor de paginación es inválido."),
        ]);
}
