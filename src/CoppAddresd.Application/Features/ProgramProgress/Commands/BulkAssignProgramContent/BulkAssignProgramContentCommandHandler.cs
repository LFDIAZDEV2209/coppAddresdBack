using System.Text.Json;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Application.Features.ProgramProgress.Commands.BulkAssignProgramContent;

/// <summary>
/// Fila de un snapshot de semana (<c>ProgramWeek.TasksSnapshot</c>, jsonb).
/// Reproduce el shape de la SPEC §3.4 que serializa ProgramRepository:
/// <c>{ weekday, task_code, points, sort_order, routine_id, nutrition_plan_id, media_id }</c>.
/// Los ids de referencia son null cuando la fila no los tiene.
/// </summary>
public sealed record WeekSnapshotTaskRow(
    short Weekday,
    string TaskCode,
    int Points,
    int SortOrder,
    Guid? RoutineId = null,
    Guid? NutritionPlanId = null,
    Guid? MediaId = null
);

/// <summary>Semana de una inscripción para la propagación de contenido.</summary>
public sealed record EnrollmentMediaWeek(
    int WeekNumber,
    ProgramWeekStatus Status,
    DateOnly WeekEndDateLocal,
    IReadOnlyList<WeekSnapshotTaskRow> Rows
);

/// <summary>Contexto de semanas de una inscripción en el rango solicitado.</summary>
public sealed record EnrollmentMediaAssignmentContext(IReadOnlyList<EnrollmentMediaWeek> Weeks);

/// <summary>Snapshot re-serializado a persistir para una semana concreta.</summary>
public sealed record WeekSnapshotUpdate(int WeekNumber, JsonElement Snapshot);

/// <summary>
/// (Des)serialización del TasksSnapshot con el shape canónico (snake_case,
/// igual que <c>ProgramRepository.BuildSnapshot/ParseSnapshot</c>): el móvil
/// y el ERP siguen leyendo el mismo contrato jsonb.
/// </summary>
public static class WeekSnapshotJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // Los nulls se escriben para que el shape sea estable (las lecturas
        // existentes hacen GetProperty sobre todas las claves base).
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

    public static IReadOnlyList<WeekSnapshotTaskRow> Parse(JsonElement snapshot)
    {
        var result = new List<WeekSnapshotTaskRow>();
        if (snapshot.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in snapshot.EnumerateArray())
        {
            Guid? ParseGuid(string property) =>
                item.TryGetProperty(property, out var prop)
                && prop.ValueKind == JsonValueKind.String
                && Guid.TryParse(prop.GetString(), out var parsed)
                    ? parsed
                    : null;

            result.Add(
                new WeekSnapshotTaskRow(
                    (short)item.GetProperty("weekday").GetInt32(),
                    item.GetProperty("task_code").GetString() ?? string.Empty,
                    item.GetProperty("points").GetInt32(),
                    item.GetProperty("sort_order").GetInt32(),
                    ParseGuid("routine_id"),
                    ParseGuid("nutrition_plan_id"),
                    ParseGuid("media_id")
                )
            );
        }

        return result;
    }

    public static JsonElement Serialize(IReadOnlyList<WeekSnapshotTaskRow> rows) =>
        JsonSerializer.SerializeToElement(
            rows.Select(row => new
            {
                weekday = (int)row.Weekday,
                task_code = row.TaskCode,
                points = row.Points,
                sort_order = row.SortOrder,
                routine_id = row.RoutineId,
                nutrition_plan_id = row.NutritionPlanId,
                media_id = row.MediaId,
            }),
            Options
        );
}

/// <summary>
/// Handler de la asignación masiva (REQ-PCA-04, design D2). La lógica de
/// propagación vive aquí (testable con fakes): por cada semana del rango
/// decide si está congelada (Completed o con fecha de cierre ya ocurrida) y,
/// salvo <c>forceFrozen</c>, la omite preservando el histórico clínico;
/// las semanas pendientes/futuras actualizan su snapshot solo en las filas
/// podcast de los días seleccionados. Las plantillas actualizan sus filas
/// <c>WeeklyDayTemplate</c> de días concretos vía repositorio.
/// </summary>
public sealed class BulkAssignProgramContentCommandHandler(
    IMediaItemRepository mediaRepository,
    IProgramRepository programRepository,
    ILogger<BulkAssignProgramContentCommandHandler> logger
) : IRequestHandler<BulkAssignProgramContentCommand, BulkAssignProgramContentResult>
{
    /// <summary>El media_id del snapshot/regla solo aplica a tareas podcast.</summary>
    private const string PodcastTaskCode = "podcast";

    public async Task<BulkAssignProgramContentResult> Handle(
        BulkAssignProgramContentCommand request,
        CancellationToken ct
    )
    {
        var targetType =
            BulkAssignProgramContentCommandValidator.NormalizeTargetType(request.TargetType)
            ?? throw new UnprocessableEntityException(
                "El targetType debe ser 'Template' o 'Enrollment'."
            );

        // El medio debe existir y estar publicado (REQ-PCA-04: "un mediaId
        // publicado"); los Draft no llegan a los pacientes.
        var media =
            await mediaRepository.GetByIdAsync(request.MediaId, ct)
            ?? throw new NotFoundException($"El medio {request.MediaId} no existe.");

        if (media.Status != MediaStatus.Published)
        {
            throw new UnprocessableEntityException(
                $"El medio '{media.Title}' no está publicado (estado {media.Status}); "
                    + "solo se pueden asignar lecciones en estado Published."
            );
        }

        var weekdays = request.Weekdays.Distinct().OrderBy(d => d).ToArray();
        var totalWeeksTargeted = request.ToWeek - request.FromWeek + 1;

        if (targetType == "Template")
        {
            return await AssignToTemplateAsync(request, weekdays, media, totalWeeksTargeted, ct);
        }

        return await AssignToEnrollmentAsync(request, weekdays, media, totalWeeksTargeted, ct);
    }

    private async Task<BulkAssignProgramContentResult> AssignToTemplateAsync(
        BulkAssignProgramContentCommand request,
        short[] weekdays,
        Domain.Entities.MediaItem media,
        int totalWeeksTargeted,
        CancellationToken ct
    )
    {
        // Las plantillas son semanales-recurrentes (sin dimensión de semana):
        // se actualizan las reglas podcast de los días indicados.
        var updatedRows =
            await programRepository.AssignMediaToTemplateAsync(
                request.TargetId,
                weekdays,
                media.Id,
                request.ActorId,
                ct
            ) ?? throw new NotFoundException($"La plantilla {request.TargetId} no existe.");

        logger.LogInformation(
            "Program.BulkAssign (Template): template={TemplateId} media={MediaId} weekdays={Weekdays} reglas={Rows}",
            request.TargetId,
            media.Id,
            string.Join(",", weekdays),
            updatedRows
        );

        return new BulkAssignProgramContentResult(
            Success: true,
            totalWeeksTargeted,
            UpdatedWeeks: updatedRows,
            FrozenWeeksSkipped: 0,
            AffectedEnrollments: 0,
            Message: $"Asignación completada. Se actualizaron {updatedRows} reglas de plantilla "
                + $"con el medio '{media.Title}'."
        );
    }

    private async Task<BulkAssignProgramContentResult> AssignToEnrollmentAsync(
        BulkAssignProgramContentCommand request,
        short[] weekdays,
        Domain.Entities.MediaItem media,
        int totalWeeksTargeted,
        CancellationToken ct
    )
    {
        var context =
            await programRepository.GetEnrollmentForMediaAssignmentAsync(
                request.TargetId,
                request.FromWeek,
                request.ToWeek,
                ct
            ) ?? throw new NotFoundException($"La inscripción {request.TargetId} no existe.");

        // Hoy local del paciente (SPEC §6.11): la marca de congelada compara
        // la fecha de cierre local de la semana; sin zona resoluble se usa la
        // fecha UTC del servidor (aproximación documentada).
        var todayLocal =
            await programRepository.GetPatientLocalTodayAsync(request.TargetId, ct)
            ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var updates = new List<WeekSnapshotUpdate>();
        var frozenWeeksSkipped = 0;
        var forcedUpdates = 0;

        foreach (var week in context.Weeks)
        {
            // Congelada (design D2): completada o con fecha de cierre ya
            // ocurrida. Su snapshot preserva adherencia/métricas/XP históricas.
            var isFrozen =
                week.Status == ProgramWeekStatus.Completed || week.WeekEndDateLocal < todayLocal;

            if (isFrozen && !request.ForceFrozen)
            {
                frozenWeeksSkipped++;
                continue;
            }

            var rows = week.Rows.Select(r => r).ToList();
            var modified = false;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (!weekdays.Contains(row.Weekday))
                {
                    continue;
                }

                if (
                    !string.Equals(
                        row.TaskCode,
                        PodcastTaskCode,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    continue; // Solo las tareas podcast llevan lección.
                }

                if (row.MediaId == media.Id)
                {
                    continue; // Ya asignado: no reescribe ni cuenta como update.
                }

                rows[i] = row with { MediaId = media.Id };
                modified = true;
            }

            if (!modified)
            {
                continue;
            }

            updates.Add(new WeekSnapshotUpdate(week.WeekNumber, WeekSnapshotJson.Serialize(rows)));
            if (isFrozen)
            {
                forcedUpdates++;
            }
        }

        if (updates.Count > 0)
        {
            await programRepository.SaveEnrollmentWeekSnapshotsAsync(
                request.TargetId,
                updates,
                request.ActorId,
                ct
            );
        }

        if (forcedUpdates > 0)
        {
            // Evento de auditoría crítico (REQ-PCA-04, escenario forceFrozen):
            // fila semántica explícita + log de advertencia. El UPDATE de cada
            // semana ya queda registrado por el trigger con old/new data.
            await programRepository.WriteAuditRowAsync(
                "BulkAssignForceFrozen",
                "app",
                "program_weeks",
                request.TargetId,
                request.ActorId,
                ct
            );
            logger.LogWarning(
                "Program.BulkAssign FORCE: {Count} semanas congeladas modificadas por fuerza explícita "
                    + "en la inscripción {EnrollmentId} (media={MediaId}). Evento crítico registrado en auditoría.",
                forcedUpdates,
                request.TargetId,
                media.Id
            );
        }

        logger.LogInformation(
            "Program.BulkAssign (Enrollment): enrollment={EnrollmentId} media={MediaId} "
                + "rango=[{FromWeek}..{ToWeek}] weekdays={Weekdays} updated={Updated} frozenSkipped={FrozenSkipped}",
            request.TargetId,
            media.Id,
            request.FromWeek,
            request.ToWeek,
            string.Join(",", weekdays),
            updates.Count,
            frozenWeeksSkipped
        );

        var message =
            forcedUpdates > 0
                ? $"Asignación completada con corrección forzada: se actualizaron {updates.Count} semanas "
                    + "(incluye semanas congeladas); el evento quedó registrado en auditoría."
            : frozenWeeksSkipped > 0
                ? $"Asignación completada. Se actualizaron {updates.Count} semanas futuras; "
                    + $"{frozenWeeksSkipped} semanas pasadas permanecen congeladas."
            : $"Asignación completada. Se actualizaron {updates.Count} semanas.";

        return new BulkAssignProgramContentResult(
            Success: true,
            totalWeeksTargeted,
            UpdatedWeeks: updates.Count,
            FrozenWeeksSkipped: frozenWeeksSkipped,
            AffectedEnrollments: 1,
            Message: message
        );
    }
}
