using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Vincula el contenido por defecto a las filas de
/// <c>app.weekly_day_templates</c> (SPEC §4.4, P1): cada tarea del programa
/// nace asignada con todo — podcast, plan nutricional y rutina — pero todo
/// es editable después (plantilla vía <c>PUT weekday-tasks</c>, inscripción
/// vía <c>PUT content/week</c> y <c>PUT content/range</c>).
///
/// Reglas:
/// - Solo rellena campos NULL: nunca pisa una edición curada en producción.
/// - Idempotente: re-ejecutable sin duplicar ni alterar lo ya vinculado.
/// - Si toca filas, incrementa <c>ProgramTemplate.Version</c> para que las
///   nuevas activaciones de semana queden trazadas
///   (<c>TemplateVersionAtStart</c>). Las semanas ya activadas conservan su
///   snapshot horneado por diseño.
/// - Se registra DESPUÉS de <see cref="DevProgramSeeder"/> para que los
///   medios, planes y rutinas ya existan en la BD.
/// </summary>
public sealed class DevProgramContentSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<DevProgramContentSeeder> logger
) : IHostedService
{
    private const string PreferredPlanName = "Plan Mediterráneo Antiinflamatorio";

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Seed de contenido del programa cancelado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el seed de contenido del programa");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var templates = await db
            .ProgramTemplates.Include(t => t.DayTemplates)
            .Where(t => t.Status == TemplateStatus.Active)
            .ToListAsync(ct);

        if (templates.Count == 0)
        {
            logger.LogWarning("Sin plantillas activas: seed de contenido omitido");
            return;
        }

        var podcastIds = await db
            .MediaItems.AsNoTracking()
            .Where(m => m.MediaType == MediaType.Podcast && m.Status == MediaStatus.Published)
            .OrderBy(m => m.SortOrder)
            .ThenBy(m => m.CreatedAt)
            .Select(m => m.Id)
            .ToListAsync(ct);

        var planId =
            await db
                .NutritionPlans.AsNoTracking()
                .Where(p => p.Status == NutritionPlanStatus.Active && p.Name == PreferredPlanName)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct)
            ?? await db
                .NutritionPlans.AsNoTracking()
                .Where(p => p.Status == NutritionPlanStatus.Active)
                .OrderBy(p => p.CreatedAt)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);

        var routines = await db
            .ExerciseRoutines.AsNoTracking()
            .Select(r => new { r.Id, r.Name })
            .ToListAsync(ct);
        var routineByName = routines
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var firstRoutineId = routines.FirstOrDefault()?.Id;

        Guid? RoutineForWeekday(short weekday) =>
            (
                weekday switch
                {
                    1 => TryRoutine("Cardio Básico"),
                    2 => TryRoutine("Pierna"),
                    3 => TryRoutine("Cardio HIIT"),
                    4 => TryRoutine("Espalda"),
                    5 => TryRoutine("Movilidad y Core") ?? TryRoutine("Full Body"),
                    _ => TryRoutine("Full Body"),
                }
            ) ?? firstRoutineId;

        Guid? TryRoutine(string name) => routineByName.TryGetValue(name, out var id) ? id : null;

        var totalLinked = 0;
        foreach (var template in templates)
        {
            var linked = 0;
            foreach (var row in template.DayTemplates)
            {
                var code = row.TaskCode.ToString();
                if (
                    code.Equals("podcast", StringComparison.OrdinalIgnoreCase)
                    && row.MediaId is null
                    && podcastIds.Count > 0
                )
                {
                    row.MediaId = podcastIds[(row.Weekday - 1) % podcastIds.Count];
                    linked++;
                }
                else if (
                    code.Equals("nut", StringComparison.OrdinalIgnoreCase)
                    && row.NutritionPlanId is null
                    && planId is not null
                )
                {
                    row.NutritionPlanId = planId;
                    linked++;
                }
                else if (
                    code.Equals("ejercicio", StringComparison.OrdinalIgnoreCase)
                    && row.RoutineId is null
                )
                {
                    var routineId = RoutineForWeekday(row.Weekday);
                    if (routineId is not null)
                    {
                        row.RoutineId = routineId;
                        linked++;
                    }
                }
            }

            if (linked > 0)
            {
                template.Version++;
                totalLinked += linked;
                logger.LogInformation(
                    "Plantilla {Code}: {Linked} tareas vinculadas con contenido (versión {Version})",
                    template.Code,
                    linked,
                    template.Version
                );
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
        }

        await BackfillMissingPodcastFilesAsync(db, ct);

        logger.LogInformation(
            "Seed de contenido del programa completado: {Linked} vínculos en {Templates} plantillas",
            totalLinked,
            templates.Count
        );
    }

    /// <summary>
    /// Los episodios sembrados apuntan a claves como
    /// <c>media/podcasts/&lt;guid&gt;.mp3</c> que no existen en disco (el
    /// proveedor Local sirve desde el cwd del Api) y el reproductor del móvil
    /// recibe 404. Este backfill copia un mp3 real existente bajo
    /// <c>media/</c> a cada clave faltante: audio de prueba, suficiente para
    /// validar reproducción end-to-end en todos los locales del equipo.
    /// Idempotente: solo crea archivos ausentes, nunca sobrescribe.
    /// </summary>
    private async Task BackfillMissingPodcastFilesAsync(AppDbContext db, CancellationToken ct)
    {
        var keys = await db
            .MediaItems.AsNoTracking()
            .Where(m => m.MediaType == MediaType.Podcast && m.Status == MediaStatus.Published)
            .Select(m => m.StorageKey)
            .ToListAsync(ct);

        if (keys.Count == 0)
        {
            return;
        }

        var root = Path.GetFullPath(".");
        var donor = Directory
            .EnumerateFiles(Path.Combine(root, "media"), "*.mp3", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (donor is null)
        {
            logger.LogWarning("Sin mp3 donante bajo media/: backfill de audio omitido");
            return;
        }

        var created = 0;
        foreach (var key in keys.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.GetFullPath(Path.Combine(root, key));
            if (
                !fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                || File.Exists(fullPath)
            )
            {
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.Copy(donor, fullPath);
            created++;
        }

        if (created > 0)
        {
            logger.LogInformation(
                "Backfill de audio: {Created} archivos de prueba creados desde {Donor}",
                created,
                Path.GetFileName(donor)
            );
        }
    }
}
