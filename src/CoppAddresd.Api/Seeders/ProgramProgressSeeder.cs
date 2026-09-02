using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seed de la plantilla por defecto del programa de 83 semanas
/// (<c>default-83w</c>): una fila en <c>app.program_templates</c> y 42 filas
/// (7 días × 6 tareas) en <c>app.weekly_day_templates</c> con los puntos base
/// del mock móvil (SPEC §8.4 y decisión 5), más los 5 pesos por defecto de
/// <c>app.health_score_weights</c> para el motor de puntajes (SPEC §13.1.1,
/// decisión 16; UPSERT idempotente por <c>dimension</c>) y las 24 reglas por
/// defecto de <c>app.xp_rules</c> (11 de adherencia/racha — SPEC §14.2,
/// decisión 20 — + 4 clínicas — SPEC §15 — + 4 de nutrición granular —
/// SPEC §18 — + 5 de la racha propia del nutracéutico — SPEC §19; UPSERT
/// idempotente por <c>code</c>).
///
/// Idempotente por <c>program_templates.code</c> (configurable vía
/// <c>appsettings → Program:DefaultTemplate:Code</c>, fallback
/// <c>default-83w</c>). El <c>media_id</c> de las filas queda NULL a propósito:
/// el vínculo de contenido (podcast) se puebla cuando existan medios
/// publicados (SPEC §4.4, P1). Nunca es destructivo: si la plantilla existe,
/// no inserta nada.
/// </summary>
public sealed class ProgramProgressSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ProgramProgressSeeder> logger) : IHostedService
{
    private readonly string _defaultTemplateCode =
        configuration["Program:DefaultTemplate:Code"] ?? "default-83w";

    private sealed record TaskSeed(TaskCode Code, int Points);

    /// <summary>
    /// Catálogo de tareas del mock móvil (PROGRAM_TASKS en
    /// antares-paciente/src/data/program.ts): mismo orden (sort_order 1..6) y
    /// mismos puntos base que la app.
    /// </summary>
    private static readonly IReadOnlyList<TaskSeed> TaskSeeds =
    [
        new(TaskCode.podcast, 80),
        new(TaskCode.vitals, 120),
        new(TaskCode.nut, 150),
        new(TaskCode.ejercicio, 150),
        new(TaskCode.nutraceutico, 80),
        new(TaskCode.emocional, 120),
    ];

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Seed de la plantilla de programa por defecto cancelado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el seed de la plantilla de programa por defecto");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        // Pesos por dimensión del Índice de Salud (SPEC §13.1.1, decisión 16):
        // se siembran SIEMPRE, independientemente de que la plantilla exista
        // (catálogo pequeño y estable; los puntajes los leen al calcular).
        await SeedHealthScoreWeightsAsync(ct);

        // Catálogo de reglas XP (SPEC §14, decisión 20): se siembra SIEMPRE,
        // como los pesos, porque es la configuración data-driven del
        // otorgamiento de XP y no depende de la plantilla.
        await SeedXpRulesAsync(ct);

        // Plantillas de hábito de alimentación/hidratación (SPEC §18, A): el
        // catálogo de comidas (des/alm/mer/cen) e hidratación (agua) que el
        // móvil registra con POST /nutrition/log. Se siembra SIEMPRE (catálogo
        // pequeño y estable, independiente de la plantilla del programa).
        await SeedNutritionHabitTemplatesAsync(ct);

        var templateId = await WithContext(
            db => db.ProgramTemplates
                .Where(x => x.Code == _defaultTemplateCode)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync(ct), ct);

        if (templateId is not null)
        {
            var dayCount = await WithContext(
                db => db.WeeklyDayTemplates.CountAsync(x => x.TemplateId == templateId, ct), ct);

            if (dayCount > 0)
            {
                logger.LogInformation(
                    "Plantilla {Code} ya existe: {DayCount} filas en weekly_day_templates (seed omitido)",
                    _defaultTemplateCode, dayCount);
                return;
            }

            // Plantilla creada por una versión anterior sin DayTemplates:
            // sembrar las 42 filas faltantes (7 días × 6 tareas).
            await WithContext(async db =>
            {
                var template = await db.ProgramTemplates
                    .Include(t => t.DayTemplates)
                    .FirstAsync(t => t.Id == templateId, ct);

                for (short weekday = 1; weekday <= 7; weekday++)
                {
                    for (var i = 0; i < TaskSeeds.Count; i++)
                    {
                        template.DayTemplates.Add(new WeeklyDayTemplate
                        {
                            TemplateId = templateId.Value,
                            Weekday = weekday,
                            TaskCode = TaskSeeds[i].Code,
                            Points = TaskSeeds[i].Points,
                            SortOrder = i + 1,
                            MediaId = null,
                        });
                    }
                }

                await db.SaveChangesAsync(ct);
                return true;
            }, ct);

            logger.LogInformation(
                "Plantilla {Code}: {DayCount} filas faltantes sembradas",
                _defaultTemplateCode, 7 * TaskSeeds.Count);
            return;
        }

        var template = new ProgramTemplate
        {
            Code = _defaultTemplateCode,
            Name = "Programa 83 semanas",
            Description = "Plantilla integral por defecto del programa de 83 semanas (auto-sembrada).",
            TotalWeeks = 83,
            // Activa desde el seed: es la plantilla por defecto del MVP y debe
            // poder usarse en inscripciones sin pasar por el flujo de publicación.
            Status = TemplateStatus.Active,
            Version = 1,
            // Umbral de racha y tareas esenciales (SPEC §17, A): 1 tarea por día
            // mantiene la racha (default) y el rescate con congelamiento exige
            // una tarea esencial (referencia ADRED: nut / ejercicio /
            // nutraceutico). Solo se fijan en el create: si la plantilla ya
            // existe, el seed no toca su configuración (idempotencia).
            StreakMinTasks = 1,
            EssentialTaskCodes = ["nut", "ejercicio", "nutraceutico"],
        };

        for (short weekday = 1; weekday <= 7; weekday++)
        {
            for (var i = 0; i < TaskSeeds.Count; i++)
            {
                template.DayTemplates.Add(new WeeklyDayTemplate
                {
                    Weekday = weekday,
                    TaskCode = TaskSeeds[i].Code,
                    Points = TaskSeeds[i].Points,
                    SortOrder = i + 1,
                    // MediaId NULL: el contenido (podcast) se vincula luego,
                    // cuando existan medios publicados (SPEC §4.4, P1).
                    MediaId = null,
                });
            }
        }

        await WithContext(async db =>
        {
            db.ProgramTemplates.Add(template);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

        logger.LogInformation(
            "Plantilla por defecto sembrada: {Code} (83 semanas, {DayCount} filas diarias)",
            _defaultTemplateCode, 7 * TaskSeeds.Count);
    }

    /// <summary>
    /// UPSERT idempotente de los 5 pesos por defecto de
    /// <c>app.health_score_weights</c> (SPEC §13.1.1): una fila por dimensión,
    /// única por <c>dimension</c> (equivalente a <c>ON CONFLICT (dimension) DO
    /// NOTHING</c>: si la dimensión ya existe, no se toca). Defaults
    /// <c>adherence=0.30, clinical=0.30, nutrition=0.20, psychology=0.10,
    /// exercise=0.10</c>, SUM = 1.0000 (invariante AC-19). NO siembra
    /// <c>clinical_baselines</c>/<c>health_scores</c>/<c>transformation_scores</c>:
    /// son por paciente y se crean en runtime (bases por clínicos, puntajes por
    /// el calculador al leer, SPEC §8.4 y §13.3).
    /// </summary>
    private async Task SeedHealthScoreWeightsAsync(CancellationToken ct)
    {
        var defaultWeights = new (ScoreDimension Dimension, decimal Weight, string Description)[]
        {
            (ScoreDimension.adherence, 0.30m, "Adherencia al programa (check-ins y congelamientos)"),
            (ScoreDimension.clinical, 0.30m, "Evolución clínica frente a línea base"),
            (ScoreDimension.nutrition, 0.20m, "Cumplimiento de hábitos de alimentación"),
            (ScoreDimension.psychology, 0.10m, "Bienestar psicológico (ánimo reportado)"),
            (ScoreDimension.exercise, 0.10m, "Actividad física (tareas ejercicio completadas)"),
        };

        foreach (var (dimension, weight, description) in defaultWeights)
        {
            var exists = await WithContext(
                db => db.HealthScoreWeights.AnyAsync(x => x.Dimension == dimension, ct), ct);

            if (exists)
            {
                continue;
            }

            await WithContext(async db =>
            {
                db.HealthScoreWeights.Add(new HealthScoreWeight
                {
                    Dimension = dimension,
                    Weight = weight,
                    Description = description,
                });
                await db.SaveChangesAsync(ct);
                return true;
            }, ct);

            logger.LogInformation(
                "Peso de dimensión sembrado: {Dimension} = {Weight}",
                dimension, weight);
        }
    }

/// <summary>
/// UPSERT idempotente de las 31 reglas por defecto de <c>app.xp_rules</c>
/// (SPEC §14.2 + §15 + §18 + §19 + §22): una fila por código, única por <c>code</c>.
/// Si la regla ya existe NO se toca (equivalente a <c>ON CONFLICT (code) DO
/// NOTHING</c>): el catálogo es editable por el administrador (SPEC §14.4,
/// prospective only) y el seeder nunca pisa una configuración ajustada en
/// producción. Los puntos base de <c>STREAK_*</c> (100/200/500/1500) quedan
/// así consistentes con el motor de hitos que los otorga una vez por
/// inscripción (SPEC §16). Las 4 reglas clínicas (categoría <c>clinical</c>,
/// SPEC §15) alimentan el motor de XP clínica que se dispara en
/// <c>POST /scores/calculate</c>; <c>CLINICAL_SIGNIFICANT</c> es la única con
/// <c>requires_validation = true</c> (la decide un clínico). Las 4 reglas de
/// nutrición granular (categoría <c>nutrition</c>, SPEC §18) alimentan
/// <c>POST /nutrition/log</c> y los premios semanales de <c>/calculate</c>.
/// Las 5 reglas <c>NB_STREAK_*</c> (categoría <c>nutriobiotic</c>, SPEC §19)
/// premian los hitos de la racha propia de la tarea nutraceutico.
/// Las 7 reglas de intervenciones (categoría <c>intervention</c>, SPEC §22,
/// "Paso 7d") premian los eventos del ciclo de vida de las intervenciones
/// derivadas de debilidades: evaluación, aceptación, teleconsulta
/// (agendada/asistida/cumplida), completación y misión de recuperación.
/// </summary>
private async Task SeedXpRulesAsync(CancellationToken ct)
{
    var defaultRules = new[]
    {
        // --- Adherencia: tareas (base_xp NULL → puntos de weekly_day_templates) ---
        new XpRuleSeed(XpRuleCodes.TaskPodcast, "Tarea podcast", "adherence", null, 1, 7),
        new XpRuleSeed(XpRuleCodes.TaskVitals, "Tarea signos vitales", "adherence", null, 1, 7),
        new XpRuleSeed(XpRuleCodes.TaskNut, "Tarea plan nutricional", "adherence", null, 1, 7),
        new XpRuleSeed(XpRuleCodes.TaskEjercicio, "Tarea ejercicio", "adherence", null, 1, 7),
        new XpRuleSeed(XpRuleCodes.TaskNutraceutico, "Tarea nutracéutico", "adherence", null, 1, 7),
        new XpRuleSeed(XpRuleCodes.TaskEmocional, "Tarea evaluación emocional", "adherence", null, 1, 7),
        // --- Adherencia: bonus de día perfecto ---
        new XpRuleSeed(XpRuleCodes.DayBonus, "Bonus día perfecto", "adherence", 50, 1, 7),
        // --- Racha: hitos (sin otorgamiento automático en el MVP actual) ---
        new XpRuleSeed(XpRuleCodes.Streak7, "Hito de racha 7 días", "streak", 100, 1, 1),
        new XpRuleSeed(XpRuleCodes.Streak11, "Hito de racha 11 días", "streak", 200, 1, 1),
        new XpRuleSeed(XpRuleCodes.Streak14, "Hito de racha 14 días", "streak", 300, 1, 1),
        new XpRuleSeed(XpRuleCodes.Streak22, "Hito de racha 22 días", "streak", 500, 1, 1),
        new XpRuleSeed(XpRuleCodes.Streak30, "Hito de racha 30 días", "streak", 800, 1, 1),
        new XpRuleSeed(XpRuleCodes.Streak50, "Hito de racha 50 días", "streak", 1500, 1, 1),
        new XpRuleSeed(XpRuleCodes.Streak75, "Hito de racha 75 días", "streak", 2500, 1, 1),
        new XpRuleSeed(XpRuleCodes.Streak100, "Hito de racha 100 días", "streak", 5000, 1, 1),
        // --- Clínica (SPEC §15): se otorgan en POST /scores/calculate ---
        // Sin topes por día/semana: una mejoría por período clínico (el dedupe
        // parcial de xp_ledger (clinical_period, health_score, reason) limita a
        // un otorgamiento por regla y período).
        new XpRuleSeed(XpRuleCodes.ClinicalImprove, "Mejoría clínica (1%..umbral)", "clinical", 50, null, null),
        new XpRuleSeed(XpRuleCodes.ClinicalSignificant, "Mejoría clínica significativa", "clinical", 100, null, null, RequiresValidation: true),
        new XpRuleSeed(XpRuleCodes.ClinicalStable, "Métrica clínica estable", "clinical", 20, null, null),
        new XpRuleSeed(XpRuleCodes.ClinicalWeeklyAllUp, "Todas las métricas mejoraron", "clinical", 150, null, null),
        // --- Nutrición granular (SPEC §18): se otorgan en POST /nutrition/log
        // (B: NUTRITION_MEAL_COMPLETE 10×4/día, NUTRITION_HYDRATION 5×1/día) y
        // en POST /scores/calculate (C: NUTRITION_WEEK_85 75×1/período,
        // NUTRITION_RECOVERY 50×1/período). Aditivas a la tarea nut existente
        // (decisión 24: la XP granular SUMA a los puntos de la tarea del
        // programa; el doble premio se documenta y se tunea vía xp_rules).
        new XpRuleSeed(XpRuleCodes.NutritionMealComplete, "Comida registrada (XP granular)", "nutrition", 10, 4, 28),
        new XpRuleSeed(XpRuleCodes.NutritionHydration, "Hidratación registrada (XP granular)", "nutrition", 5, 1, 7),
        new XpRuleSeed(XpRuleCodes.NutritionWeek85, "Adherencia nutricional semanal ≥ 85%", "nutrition", 75, 1, 1),
        new XpRuleSeed(XpRuleCodes.NutritionRecovery, "Recuperación nutricional (+20pp vs período anterior)", "nutrition", 50, 1, 1),
        // --- Racha propia del nutracéutico (SPEC §19, B): hitos de la racha
        // CONSECUTIVA de la tarea nutraceutico, independiente de la racha
        // general (un día perdido la rompe; los congelamientos NO la protegen).
        // Se otorgan en el camino de completación de la tarea (solo primera
        // escritura), con el multiplicador del paciente y topes 1/día y 1/semana.
        new XpRuleSeed(XpRuleCodes.NbStreak7, "Hito racha nutracéutico 7 días", "nutriobiotic", 50, 1, 1),
        new XpRuleSeed(XpRuleCodes.NbStreak14, "Hito racha nutracéutico 14 días", "nutriobiotic", 100, 1, 1),
        new XpRuleSeed(XpRuleCodes.NbStreak30, "Hito racha nutracéutico 30 días", "nutriobiotic", 250, 1, 1),
        new XpRuleSeed(XpRuleCodes.NbStreak60, "Hito racha nutracéutico 60 días", "nutriobiotic", 500, 1, 1),
        new XpRuleSeed(XpRuleCodes.NbStreak90, "Hito racha nutracéutico 90 días", "nutriobiotic", 1000, 1, 1),
        // --- Intervenciones (SPEC §22, "Paso 7d"): XP por eventos del ciclo
        // de vida de las intervenciones derivadas de debilidades. Se otorgan
        // por el camino del catálogo (§14.3) con dedupe parcial del libro
        // mayor. WEAKNESS_ASSESS (20, crea intervención); INTERV_ACCEPT (15,
        // paciente acepta); TELE_SCHEDULE (50, teleconsulta agendada);
        // TELE_ATTEND (100, requires_validation, clínico confirma asistencia);
        // TELE_COMPLY (50, evaluación de cumplimiento); INTERV_COMPLETE (200,
        // requires_validation, intervención completada con resultado);
        // RECOVERY_MISSION (50, paciente acepta recovery_mission).
        new XpRuleSeed(XpRuleCodes.WeaknessAssess, "Evaluación de debilidad (intervención creada)", "intervention", 20, 1, 1),
        new XpRuleSeed(XpRuleCodes.IntervAccept, "Aceptación de intervención", "intervention", 15, 1, 7),
        new XpRuleSeed(XpRuleCodes.TeleSchedule, "Teleconsulta agendada", "intervention", 50, 1, 7),
        new XpRuleSeed(XpRuleCodes.TeleAttend, "Teleconsulta asistida (validación clínica)", "intervention", 100, null, null, RequiresValidation: true),
        new XpRuleSeed(XpRuleCodes.TeleComply, "Cumplimiento de teleconsulta", "intervention", 50, 1, 7),
        new XpRuleSeed(XpRuleCodes.IntervComplete, "Intervención completada (validación clínica)", "intervention", 200, null, null, RequiresValidation: true),
        new XpRuleSeed(XpRuleCodes.RecoveryMission, "Misión de recuperación aceptada", "intervention", 50, 1, 7),
    };

    foreach (var seed in defaultRules)
    {
        var exists = await WithContext(
            db => db.XpRules.AnyAsync(x => x.Code == seed.Code, ct), ct);

        if (exists)
        {
            continue;
        }

        await WithContext(async db =>
        {
            db.XpRules.Add(new XpRule
            {
                Code = seed.Code,
                Name = seed.Name,
                Category = seed.Category,
                BaseXp = seed.BaseXp,
                Multiplier = 1.0m,
                MaxPerDay = seed.MaxPerDay,
                MaxPerWeek = seed.MaxPerWeek,
                RequiresValidation = seed.RequiresValidation,
                Active = true,
                // ValidFrom por defecto (hoy) y ValidUntil null = vigencia abierta.
            });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

        logger.LogInformation(
            "Regla de XP sembrada: {Code} (categoría {Category}, base {BaseXp}, validación {RequiresValidation})",
            seed.Code, seed.Category, seed.BaseXp?.ToString() ?? "template", seed.RequiresValidation);
    }
}

/// <summary>Descriptor de una regla por defecto del catálogo de XP.</summary>
private sealed record XpRuleSeed(
    string Code,
    string Name,
    string Category,
    int? BaseXp,
    int? MaxPerDay,
    int? MaxPerWeek,
    bool RequiresValidation = false);

/// <summary>
/// UPSERT idempotente de las plantillas de hábito de alimentación/hidratación
/// (SPEC §18, A): una fila por código de comida del móvil
/// (<c>des</c>/<c>alm</c>/<c>mer</c>/<c>cen</c>/<c>agua</c>), única por
/// <c>code</c> (equivalente a <c>ON CONFLICT (code) DO NOTHING</c>). Categoría
/// <c>alimentacion</c> para las 4 comidas (alimenta la dimensión de nutrición
/// del Índice de Salud, SPEC §13.4.3) y <c>agua</c> para la hidratación. El
/// <c>code</c> es el <c>mealCode</c> del contrato de <c>POST /nutrition/log</c>.
/// </summary>
private async Task SeedNutritionHabitTemplatesAsync(CancellationToken ct)
{
    var defaultTemplates = new (string Code, string Name, string Category, int SortOrder)[]
    {
        ("des", "Desayuno", "alimentacion", 1),
        ("alm", "Almuerzo", "alimentacion", 2),
        ("mer", "Merienda", "alimentacion", 3),
        ("cen", "Cena", "alimentacion", 4),
        ("agua", "Hidratación", "agua", 5),
    };

    foreach (var (code, name, category, sortOrder) in defaultTemplates)
    {
        var exists = await WithContext(
            db => db.HabitTemplates.AnyAsync(x => x.Code == code, ct), ct);

        if (exists)
        {
            continue;
        }

        await WithContext(async db =>
        {
            db.HabitTemplates.Add(new HabitTemplate
            {
                Code = code,
                Name = name,
                Category = category,
                SortOrder = sortOrder,
            });
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

        logger.LogInformation(
            "Plantilla de hábito sembrada: {Code} (categoría {Category})",
            code, category);
    }
}

/// <summary>
/// Ejecuta una operación con un scope propio: cada llamada resuelve un
/// DbContext distinto, evitando conflictos de tracking EF entre operaciones.
/// </summary>
    private async Task<T> WithContext<T>(Func<AppDbContext, Task<T>> action, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }
}