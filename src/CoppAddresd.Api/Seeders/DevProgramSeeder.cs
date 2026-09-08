using System.Text.Json;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.ProgramProgress;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seeder masivo e idempotente para el módulo de Progreso del Programa (83 semanas).
///
/// Propósito:
/// 1. Garantiza la existencia de al menos 24 pacientes activos en <c>app.patient_profiles</c>.
/// 2. Siembra un catálogo enriquecido de planes de nutrición (Keto, Mediterráneo, DASH,
///    Diabetes, Longevidad) con días, comidas, macros y recetas gourmet.
/// 3. Siembra lecciones de podcast multimedia en <c>app.media_items</c>.
/// 4. Inscribe a al menos 20 pacientes activos en la plantilla de 83 semanas (<c>default-83w</c>).
/// 5. Asigna a cada paciente sus planes nutricionales y rutinas de ejercicio por día de la semana.
/// 6. Simula progreso realista distribuido en 4 tiers:
///    - Tier 1: Veteranos de alta adherencia (Semana 5-6, 35 días de racha, Nivel 5-7, 5,000-9,000 XP).
///    - Tier 2: Progreso constante (Semana 3-4, 18-21 días de racha, Nivel 3-4, 2,200-4,500 XP).
///    - Tier 3: Recuperados con congelamiento de racha (Semana 3, freeze consumido, Nivel 2-3).
///    - Tier 4: Recién inscritos (Semana 1, 1-4 días de completación, Nivel 1).
///
/// Idempotencia:
/// - Re-ejecutable sin duplicaciones, sin colisiones de claves únicas ni alteración destructiva.
/// - Se registra DESPUÉS de <see cref="ProgramProgressSeeder"/> y <see cref="ExerciseRoutineSeeder"/>
///   para que la plantilla base y las rutinas ya existan en la BD.
/// </summary>
public sealed class DevProgramSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DevProgramSeeder> logger) : IHostedService
{
    private const string DefaultTimezone = "America/Bogota";
    private readonly string _defaultTemplateCode =
        configuration["Program:DefaultTemplate:Code"] ?? "program-coppaddresd-83-days";

    private sealed record PatientSeedDef(
        string FirstName,
        string LastName,
        string Gender,
        DateOnly Dob,
        string DocumentNumber,
        string Email,
        string Phone,
        string CityName);

    private static readonly IReadOnlyList<PatientSeedDef> DemoPatientSeeds =
    [
        new("Valentina", "Ríos", "Femenino", new DateOnly(1992, 4, 15), "1000000001", "valentina.rios@demo.antares.co", "3102458891", "Bogotá"),
        new("Andrés", "Cárdenas", "Masculino", new DateOnly(1985, 8, 22), "1000000002", "andres.cardenas@demo.antares.co", "3204561234", "Medellín"),
        new("Carolina", "Mendoza", "Femenino", new DateOnly(1990, 11, 5), "1000000003", "carolina.mendoza@demo.antares.co", "3005672345", "Cali"),
        new("Jorge", "Herrera", "Masculino", new DateOnly(1978, 6, 30), "1000000004", "jorge.herrera@demo.antares.co", "3116783456", "Barranquilla"),
        new("Luisa", "Fernández", "Femenino", new DateOnly(1995, 2, 18), "1000000005", "luisa.fernandez@demo.antares.co", "3157894567", "Bogotá"),
        new("Miguel Ángel", "Peña", "Masculino", new DateOnly(1982, 9, 12), "1000000006", "miguel.pena@demo.antares.co", "3168905678", "Medellín"),
        new("Diana", "Ospina", "Femenino", new DateOnly(1988, 12, 3), "1000000007", "diana.ospina@demo.antares.co", "3129016789", "Cali"),
        new("Camilo", "Restrepo", "Masculino", new DateOnly(1975, 3, 27), "1000000008", "camilo.restrepo@demo.antares.co", "3180127890", "Bogotá"),
        new("Paola", "Salazar", "Femenino", new DateOnly(1993, 7, 19), "1000000009", "paola.salazar@demo.antares.co", "3191238901", "Barranquilla"),
        new("Santiago", "Pineda", "Masculino", new DateOnly(1987, 1, 14), "1000000010", "santiago.pineda@demo.antares.co", "3012349012", "Medellín"),
        new("María", "Castillo", "Femenino", new DateOnly(1991, 10, 8), "1000000011", "maria.castillo@demo.antares.co", "3023450123", "Bogotá"),
        new("David", "Quiroga", "Masculino", new DateOnly(1980, 5, 25), "1000000012", "david.quiroga@demo.antares.co", "3034561234", "Cali"),
        new("Laura", "Serna", "Femenino", new DateOnly(1994, 9, 16), "1000000013", "laura.serna@demo.antares.co", "3045672345", "Bogotá"),
        new("Felipe", "Montoya", "Masculino", new DateOnly(1983, 4, 2), "1000000014", "felipe.montoya@demo.antares.co", "3056783456", "Medellín"),
        new("Mariana", "Patiño", "Femenino", new DateOnly(1996, 6, 21), "1000000015", "mariana.patino@demo.antares.co", "3067894567", "Cali"),
        new("Ricardo", "Bermúdez", "Masculino", new DateOnly(1977, 8, 14), "1000000016", "ricardo.bermudez@demo.antares.co", "3078905678", "Bogotá"),
        new("Sofía", "Toro", "Femenino", new DateOnly(1989, 3, 29), "1000000017", "sofia.toro@demo.antares.co", "3089016789", "Barranquilla"),
        new("Mateo", "Zapata", "Masculino", new DateOnly(1997, 11, 11), "1000000018", "mateo.zapata@demo.antares.co", "3090127890", "Medellín"),
        new("Isabella", "Rojas", "Femenino", new DateOnly(1984, 2, 7), "1000000019", "isabella.rojas@demo.antares.co", "3101238901", "Bogotá"),
        new("Sebastián", "Molina", "Masculino", new DateOnly(1992, 5, 17), "1000000020", "sebastian.molina@demo.antares.co", "3112349012", "Cali"),
        new("Camila", "Herrera", "Femenino", new DateOnly(1986, 12, 24), "1000000021", "camila.herrera@demo.antares.co", "3123450123", "Medellín"),
        new("Tomás", "Vargas", "Masculino", new DateOnly(1981, 7, 9), "1000000022", "tomas.vargas@demo.antares.co", "3134561234", "Bogotá"),
        new("Ximena", "Pérez", "Femenino", new DateOnly(1979, 10, 31), "1000000023", "ximena.perez@demo.antares.co", "3145672345", "Barranquilla"),
        new("Diego", "Moreno", "Masculino", new DateOnly(1988, 4, 18), "1000000024", "diego.moreno@demo.antares.co", "3156783456", "Cali"),
    ];

    private static readonly (string Title, string Description, string Author, int DurationSecs, int SortOrder)[] PodcastSeeds =
    [
        ("Ep. 1: Fundamentos del Biohacking y Ritmos Circadianos", "Introducción a la optimización biológica y sincronización del reloj interno para potenciar energía y metabolismo.", "Dr. Alejandro Gómez", 720, 1),
        ("Ep. 2: Flexibilidad Metabólica y Nutrición Celular", "Cómo entrenar al cuerpo para alternar eficientemente entre carbohidratos y grasas como fuente de energía.", "Dra. Sofía Morales", 840, 2),
        ("Ep. 3: Microbiota Intestinal y el Eje Intestino-Cerebro", "El impacto de la salud intestinal en la inflamación sistémica, inmunidad y bienestar emocional.", "Dr. Carlos Valencia", 660, 3),
        ("Ep. 4: Regulación del Cortisol y Manejo del Estrés", "Estrategias prácticas de respiración y hábitos para mitigar el estrés crónico y proteger el sistema cardiovascular.", "Dra. Elena Ruiz", 600, 4),
        ("Ep. 5: Calidad de Sueño y Recuperación Profunda", "Técnicas de higiene del sueño, arquitectura de ondas lentas y optimización de la reparación celular nocturna.", "Dr. Alejandro Gómez", 780, 5),
        ("Ep. 6: Entrenamiento de Fuerza y Longevidad Mitocondrial", "La importancia de la masa muscular como órgano endocrino y su rol en la sensibilidad a la insulina.", "Lic. Mateo Ríos", 900, 6),
        ("Ep. 7: Ayuno Intermitente y Autofagia Estratégica", "Mecanismos de reciclaje celular y longevidad a través de ventanas controladas de alimentación.", "Dra. Sofía Morales", 750, 7),
    ];

    private static readonly (TaskCode Code, int Points)[] StandardTasks =
    [
        (TaskCode.podcast, 80),
        (TaskCode.vitals, 120),
        (TaskCode.nut, 150),
        (TaskCode.ejercicio, 150),
        (TaskCode.nutraceutico, 80),
        (TaskCode.emocional, 120),
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
            logger.LogInformation("Seed masivo de Progreso del Programa cancelado.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el seed masivo de Progreso del Programa.");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        logger.LogInformation("Iniciando seed masivo de Progreso del Programa...");

        // Paso 1: Asegurar podcasts/medios publicados en app.media_items
        var mediaIds = await EnsureMediaItemsAsync(ct);

        // Paso 2: Asegurar planes de alimentación ricos
        var planIds = await EnsureNutritionPlansAsync(ct);

        // Paso 3: Asegurar que existan al menos 24 pacientes activos en app.patient_profiles
        await EnsurePatientProfilesAsync(ct);

        // Paso 4: Obtener la plantilla de programa de 83 semanas
        var template = await GetProgramTemplateAsync(ct);
        if (template is null)
        {
            logger.LogWarning("Plantilla de programa {Code} no encontrada. Abortando seed de inscripciones.", _defaultTemplateCode);
            return;
        }

        // Paso 5: Mapear rutinas de ejercicio por día de semana
        var routineMap = await GetExerciseRoutineMapAsync(ct);

        // Paso 6: Catálogos auxiliares de hábitos y métricas clínicas
        var habitTemplateMap = await GetHabitTemplatesMapAsync(ct);
        var clinicalMetricMap = await GetClinicalMetricsMapAsync(ct);

        // Paso 7: Cargar los 24 pacientes demo/activos para el programa
        var demoEmails = DemoPatientSeeds.Select(d => d.Email).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var demoDocs = DemoPatientSeeds.Select(d => d.DocumentNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var activePatients = await WithContext(
            db => db.PatientProfiles
                .Where(p => p.Status == "Activo" && p.DeletedAt == null)
                .Where(p => demoEmails.Contains(p.Email!) || demoDocs.Contains(p.DocumentNumber!))
                .OrderBy(p => p.DocumentNumber)
                .Select(p => new { p.Id, p.FirstName, p.LastName, p.Email, p.DocumentNumber })
                .ToListAsync(ct), ct);

        if (activePatients.Count < 20)
        {
            activePatients = await WithContext(
                db => db.PatientProfiles
                    .Where(p => p.Status == "Activo" && p.DeletedAt == null)
                    .OrderBy(p => p.CreatedAt)
                    .Take(24)
                    .Select(p => new { p.Id, p.FirstName, p.LastName, p.Email, p.DocumentNumber })
                    .ToListAsync(ct), ct);
        }

        logger.LogInformation("Pacientes activos seleccionados para el programa: {Count}", activePatients.Count);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thisMonday = GetMondayOfDate(today);

        var enrolledCount = 0;
        var progressCount = 0;

        var clinicianUserId = await GetDefaultClinicianUserIdAsync(ct);

        for (var i = 0; i < activePatients.Count; i++)
        {
            var patient = activePatients[i];
            var planId = planIds[i % planIds.Count];

            // Determinar tier y semanas de inicio
            // Tier 1: 0..5 (Semana 5, 35 días de historial)
            // Tier 2: 6..12 (Semana 3, 21 días de historial)
            // Tier 3: 13..16 (Semana 3, con freeze consumido)
            // Tier 4: 17..23 (Semana 1, reciente)
            int tier;
            int weeksAgo;
            if (i < 6)
            {
                tier = 1;
                weeksAgo = 5;
            }
            else if (i < 13)
            {
                tier = 2;
                weeksAgo = 3;
            }
            else if (i < 17)
            {
                tier = 3;
                weeksAgo = 3;
            }
            else
            {
                tier = 4;
                weeksAgo = 0;
            }

            var startLocalDate = thisMonday.AddDays(-weeksAgo * 7);
            var currentWeekNumber = Math.Min(template.TotalWeeks, weeksAgo + 1);

            var enrollmentId = await EnsureEnrollmentAsync(
                patient.Id,
                template,
                startLocalDate,
                currentWeekNumber,
                ct);

            if (enrollmentId == Guid.Empty)
            {
                continue;
            }

            enrolledCount++;

            // Asignar plan de alimentación para las semanas activas
            await EnsureNutritionPlanAssignmentAsync(patient.Id, planId, startLocalDate, template.TotalWeeks, ct);

            // Asignar rutinas de ejercicio por día de semana
            await EnsureRoutineAssignmentsAsync(patient.Id, routineMap, startLocalDate, template.TotalWeeks, ct);

            // Si el tier tiene progreso histórico, simularlo
            var seededProgress = await SeedPatientProgressAsync(
                patient.Id,
                enrollmentId,
                template,
                planId,
                routineMap,
                mediaIds,
                habitTemplateMap,
                clinicalMetricMap,
                startLocalDate,
                today,
                tier,
                i,
                clinicianUserId,
                ct);

            if (seededProgress)
            {
                progressCount++;
            }
        }

        logger.LogInformation(
            "Seed masivo de Progreso del Programa completado: {Enrolled} pacientes inscritos, {Progress} con progreso gamificado simulado.",
            enrolledCount, progressCount);
    }

    // =========================================================================
    // 1. MEDIOS MULTIMEDIA (PODCASTS)
    // =========================================================================

    private async Task<List<Guid>> EnsureMediaItemsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingMedia = await db.MediaItems.ToListAsync(ct);
        var mediaMap = existingMedia
            .GroupBy(m => m.Title, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var resultIds = new List<Guid>();

        foreach (var p in PodcastSeeds)
        {
            if (mediaMap.TryGetValue(p.Title, out var existingId))
            {
                resultIds.Add(existingId);
                continue;
            }

            var media = new MediaItem
            {
                Id = Guid.NewGuid(),
                Title = p.Title,
                Description = p.Description,
                Author = p.Author,
                MediaType = MediaType.Podcast,
                Category = MediaCategory.Nutricion,
                StorageKey = $"media/podcasts/{Guid.NewGuid()}.mp3",
                ThumbnailKey = "media/thumbnails/podcast-default.jpg",
                ContentType = "audio/mpeg",
                DurationSecs = p.DurationSecs,
                Status = MediaStatus.Published,
                SortOrder = p.SortOrder,
                Day = p.SortOrder,
                Month = 1,
                PublishedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            db.MediaItems.Add(media);
            resultIds.Add(media.Id);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Podcasts sembrados en app.media_items: {Count}", PodcastSeeds.Length);
        }

        return resultIds;
    }

    // =========================================================================
    // 2. PLANES DE NUTRICIÓN ENRIQUECIDOS
    // =========================================================================

    private async Task<List<Guid>> EnsureNutritionPlansAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var planDefs = GetNutritionPlanSeeds();
        var resultIds = new List<Guid>();

        foreach (var def in planDefs)
        {
            var existing = await db.NutritionPlans
                .Include(p => p.Days)
                .FirstOrDefaultAsync(p => p.Name == def.Name, ct);

            if (existing is not null)
            {
                resultIds.Add(existing.Id);
                if (existing.Days.Count == 0)
                {
                    var days = BuildPlanDays(existing.Id, def);
                    db.NutritionPlanDays.AddRange(days);
                    await db.SaveChangesAsync(ct);
                }
                continue;
            }

            var plan = new NutritionPlan
            {
                Id = Guid.NewGuid(),
                Name = def.Name,
                Description = def.Description,
                TargetCondition = def.TargetCondition,
                DurationDays = 7,
                DailyCalorieTarget = def.DailyCalorieTarget,
                DailyProteinTarget = def.DailyProteinTarget,
                DailyCarbsTarget = def.DailyCarbsTarget,
                DailyFatTarget = def.DailyFatTarget,
                DailyFiberTarget = def.DailyFiberTarget,
                IsTemplate = true,
                Status = NutritionPlanStatus.Active,
                CreatedAt = DateTime.UtcNow,
            };

            db.NutritionPlans.Add(plan);
            var planDays = BuildPlanDays(plan.Id, def);
            db.NutritionPlanDays.AddRange(planDays);
            await db.SaveChangesAsync(ct);

            resultIds.Add(plan.Id);
            logger.LogInformation("Plan nutricional sembrado: '{Plan}' ({Days} comidas)", plan.Name, planDays.Count);
        }

        return resultIds;
    }

    private sealed record NutritionPlanSeedDef(
        string Name,
        string Description,
        string TargetCondition,
        int DailyCalorieTarget,
        decimal DailyProteinTarget,
        decimal DailyCarbsTarget,
        decimal DailyFatTarget,
        decimal DailyFiberTarget,
        IReadOnlyList<(MealType Type, string Desc, string Foods, int Cal, int Prot, int Carbs, int Fat, int Fiber, string Notes)> DailyPattern);

    private static List<NutritionPlanSeedDef> GetNutritionPlanSeeds() =>
    [
        new(
            "Plan Keto Biohacking",
            "Plan cetogénico antiinflamatorio alto en grasas saludables (aguacate, aceite de oliva, MCT) para optimizar energía y autofagia.",
            "Obesidad / Resistencia a la insulina",
            1800, 95m, 25m, 145m, 28m,
            [
                (MealType.Desayuno, "Omelette de espinacas tiernas, queso feta y aguacate con té verde", "3 huevos camperos, 60g espinaca tierna, 40g queso feta, 1/2 aguacate hass, aceite de oliva virgen extra", 460, 26, 4, 38, 6, "Cocinar a fuego medio para preservar los nutrientes de las espinacas."),
                (MealType.Almuerzo, "Bowl keto de pechuga marinada al limón con espárragos y rúcula", "220g pechuga de pollo, 120g espárragos, 80g rúcula, 1/2 aguacate, 2 cdas aceite de oliva", 680, 48, 7, 50, 8, "Marinar con orégano y ajo antes de sellar a la plancha."),
                (MealType.Cena, "Filete de salmón salvaje al horno con brócoli al vapor y mantequilla ghee", "200g filete de salmón, 180g brócoli, 15g mantequilla ghee, 30g queso parmesano rallado", 580, 38, 6, 44, 6, "Cenar al menos 2.5 horas antes de acostarse."),
            ]),

        new(
            "Plan Mediterráneo Antiinflamatorio",
            "Patrón mediterráneo rico en polifenoles, ácidos grasos Omega-3, legumbres y vegetales frescos para longevidad cardiovascular.",
            "Riesgo cardiovascular / Hipertensión",
            1900, 110m, 160m, 80m, 35m,
            [
                (MealType.Desayuno, "Tostada integral de masa madre con tomate rallado, aguacate y huevo pochado", "2 rebanadas pan masa madre, 1 tomate rallado, 1/2 aguacate, 2 huevos pochados, aceite de oliva virgen extra", 480, 22, 42, 26, 7, "Acompañar con infusión de romero o té blanco."),
                (MealType.Almuerzo, "Lomo de lubina a la plancha con quinoa tricolor y pisto de verduras", "200g lubina fresca, 80g quinoa, calabacín, berenjena, pimiento rojo, aceite de oliva", 640, 44, 52, 28, 9, "Cocinar las verduras a fuego lento para potenciar sus antioxidantes."),
                (MealType.Cena, "Crema templada de calabaza y jengibre con dados de tofu marinado y semillas de calabaza", "250g calabaza, 150g tofu firme, 20g semillas de calabaza, cebollino, caldo vegetal", 490, 24, 38, 22, 8, "Cena ligera ideal para optimizar el descanso nocturno."),
            ]),

        new(
            "Plan DASH Control Cardiovascular",
            "Enfoque dietético para frenar la hipertensión: bajo en sodio, alto en potasio, magnesio y fibra soluble.",
            "Hipertensión / Salud Renal",
            1750, 100m, 180m, 60m, 38m,
            [
                (MealType.Desayuno, "Porridge de avena integral con frutos rojos, semillas de chía y leche de almendras", "60g avena integral, 80g arándanos frescos, 15g chía, 200ml leche almendras sin azúcar, canela ceylán", 420, 16, 58, 14, 12, "La canela ayuda a modular la glucemia matutina."),
                (MealType.Almuerzo, "Pechuga de pavo al romero con batata asada y ensalada de espinacas", "200g pechuga de pavo, 150g batata asada, 100g espinaca baby, nueces, vinagreta de limón", 620, 48, 54, 20, 8, "Sin sal añadida; realzar sabor con hierbas aromáticas."),
                (MealType.Cena, "Merluza al vapor con judías verdes, zanahorias baby y patata al vapor", "220g lomo de merluza, 150g judías verdes, 100g zanahorias, 1 patata pequeña, aceite de oliva", 460, 38, 36, 16, 7, "Cocción al vapor suave para conservar minerales."),
            ]),

        new(
            "Plan Control Glucémico Diabetes",
            "Plan con bajo índice glucémico, distribución estratégica de carbohidratos complejos y balance proteico para estabilizar la glucemia.",
            "Diabetes tipo 2 / Prediabetes",
            1650, 105m, 120m, 70m, 40m,
            [
                (MealType.Desayuno, "Revuelto de claras y huevo entero con champiñones Portobello y espárragos trigueros", "1 huevo entero + 3 claras, 100g champiñones, 80g espárragos, 30g queso bajo en grasa, té verde", 380, 32, 12, 18, 6, "Excelente densidad proteica con mínimo impacto glucémico."),
                (MealType.Almuerzo, "Solomillo de ternera magra con ensalada tibia de lentejas pardinas y rúcula", "180g solomillo de ternera, 120g lentejas cocidas, 60g rúcula, tomate cherry, aceite de oliva virgen extra", 610, 46, 44, 22, 11, "Las legumbres aportan fibra prebiótica de lenta absorción."),
                (MealType.Cena, "Pechuga de pollo a la plancha con brócoli salteado con almendras laminadas", "200g pechuga de pollo, 180g brócoli, 20g almendras laminadas, ajo tierno, aceite de oliva", 470, 42, 14, 24, 7, "Cena alta en magnesio y antioxidantes protectores."),
            ]),

        new(
            "Plan Longevidad y Autofagia",
            "Densidad nutricional máxima, alimentos fermentados, crucíferas y polifenoles bioactivos para potenciar la salud mitocondrial.",
            "Longevidad / Bienestar Integral",
            1850, 100m, 140m, 90m, 36m,
            [
                (MealType.Desayuno, "Pudding de chía y kéfir artesanal con frambuesas y nueces de brasil", "150g kéfir de cabra, 25g semillas de chía, 60g frambuesas, 2 nueces de brasil (selenio), cacao puro", 440, 20, 28, 24, 11, "Aporte probiótico y prebiótico óptimo para la microbiota."),
                (MealType.Almuerzo, "Bowl de salmón salvaje con arroz negro venere, aguacate y chucrut artesanal", "180g salmón salvaje, 70g arroz venere, 1/2 aguacate, 40g chucrut no pasteurizado, semillas de sésamo", 670, 40, 46, 34, 8, "Rico en antocianinas y ácidos grasos esenciales."),
                (MealType.Cena, "Crema de calabacín y puerro con huevo poché y lascas de trufa o AOVE picual", "250g calabacín, 80g puerro, 2 huevos camperos pochados, 15ml AOVE cosecha temprana", 480, 22, 22, 32, 6, "Favorece la producción de melatonina endógena."),
            ]),
    ];

    private static List<NutritionPlanDay> BuildPlanDays(Guid planId, NutritionPlanSeedDef def)
    {
        var days = new List<NutritionPlanDay>();
        for (var dayNum = 1; dayNum <= 7; dayNum++)
        {
            var sort = 1;
            foreach (var (type, desc, foods, cal, prot, carbs, fat, fiber, notes) in def.DailyPattern)
            {
                days.Add(new NutritionPlanDay
                {
                    PlanId = planId,
                    DayNumber = dayNum,
                    MealType = type,
                    Description = desc,
                    Foods = foods,
                    Calories = cal,
                    ProteinG = prot,
                    CarbsG = carbs,
                    FatG = fat,
                    FiberG = fiber,
                    WaterMl = 500,
                    Notes = notes,
                    SortOrder = sort++,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }
        return days;
    }

    // =========================================================================
    // 3. PACIENTES EN app.patient_profiles
    // =========================================================================

    private async Task EnsurePatientProfilesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingDocs = await db.PatientProfiles
            .Select(p => p.DocumentNumber)
            .Where(d => d != null)
            .ToListAsync(ct);

        var existingEmails = await db.PatientProfiles
            .Select(p => p.Email)
            .Where(e => e != null)
            .ToListAsync(ct);

        var existingDocSet = new HashSet<string>(existingDocs!, StringComparer.OrdinalIgnoreCase);
        var existingEmailSet = new HashSet<string>(existingEmails!, StringComparer.OrdinalIgnoreCase);

        var cities = await db.Cities.Select(c => new { c.Id, c.Name }).ToListAsync(ct);
        var cityMap = cities
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var defaultCityId = cities.FirstOrDefault()?.Id;

        var insurers = await db.Insurers.Select(i => i.Id).ToListAsync(ct);
        var defaultInsurerId = insurers.FirstOrDefault();

        var inserted = 0;
        foreach (var p in DemoPatientSeeds)
        {
            if (existingDocSet.Contains(p.DocumentNumber) || existingEmailSet.Contains(p.Email))
            {
                continue;
            }

            var cityId = cityMap.TryGetValue(p.CityName, out var cid) ? cid : defaultCityId;

            var profile = new PatientProfile
            {
                Id = Guid.NewGuid(),
                FirstName = p.FirstName,
                LastName = p.LastName,
                Gender = p.Gender,
                DateOfBirth = p.Dob.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                DocumentNumber = p.DocumentNumber,
                Email = p.Email,
                PhoneNumber = p.Phone,
                PhoneCountryCode = "57",
                Address = $"Calle 100 # 15-{inserted + 10}, {p.CityName}",
                CityId = cityId,
                InsurerId = defaultInsurerId,
                MemberId = $"ANT-{p.DocumentNumber[..6]}",
                MedicalRecordNumber = $"MRN-{p.DocumentNumber}",
                Status = "Activo",
                CreatedAt = DateTime.UtcNow.AddMonths(-6),
            };

            db.PatientProfiles.Add(profile);
            inserted++;
        }

        if (inserted > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Pacientes demo creados en app.patient_profiles: {Count}", inserted);
        }
    }

    // =========================================================================
    // 4. PLANTILLA DE PROGRAMA Y RUTINAS
    // =========================================================================

    private async Task<ProgramTemplate?> GetProgramTemplateAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var template = await db.ProgramTemplates
            .Include(t => t.DayTemplates)
            .FirstOrDefaultAsync(t => t.Code == _defaultTemplateCode, ct);

        if (template is not null)
        {
            return template;
        }

        return await db.ProgramTemplates
            .Include(t => t.DayTemplates)
            .FirstOrDefaultAsync(t => t.Code == "default-83w" || t.Status == TemplateStatus.Active, ct);
    }

    private async Task<Dictionary<int, Guid>> GetExerciseRoutineMapAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var routines = await db.ExerciseRoutines.AsNoTracking().ToListAsync(ct);
        var routineByName = routines
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        var firstId = routines.FirstOrDefault()?.Id ?? Guid.Empty;

        // Mapeo 0=Lunes, 1=Martes, 2=Miércoles, 3=Jueves, 4=Viernes
        var map = new Dictionary<int, Guid>();
        if (routineByName.TryGetValue("Cardio Básico", out var r0)) map[0] = r0; else map[0] = firstId;
        if (routineByName.TryGetValue("Pierna", out var r1)) map[1] = r1; else map[1] = firstId;
        if (routineByName.TryGetValue("Cardio HIIT", out var r2)) map[2] = r2; else map[2] = firstId;
        if (routineByName.TryGetValue("Espalda", out var r3)) map[3] = r3; else map[3] = firstId;
        if (routineByName.TryGetValue("Movilidad y Core", out var r4)) map[4] = r4;
        else if (routineByName.TryGetValue("Full Body", out var r4b)) map[4] = r4b;
        else map[4] = firstId;

        return map;
    }

    private async Task<Dictionary<string, Guid>> GetHabitTemplatesMapAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var habits = await db.HabitTemplates.AsNoTracking().ToListAsync(ct);
        return habits
            .GroupBy(h => h.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, (Guid MetricId, Guid DefaultUnitId)>> GetClinicalMetricsMapAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var metrics = await db.MeasurementMetrics
            .AsNoTracking()
            .Select(m => new { m.Code, m.Id, m.DefaultUnitId })
            .ToListAsync(ct);

        return metrics
            .GroupBy(m => m.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (g.First().Id, g.First().DefaultUnitId), StringComparer.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 5. INSCRIPCIÓN Y SEMANAS (IDEMPOTENTE)
    // =========================================================================

    private async Task<Guid> EnsureEnrollmentAsync(
        Guid patientId,
        ProgramTemplate template,
        DateOnly startLocalDate,
        int currentWeekNumber,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.ProgramEnrollments
            .FirstOrDefaultAsync(e => e.PatientId == patientId && e.Status == ProgramEnrollmentStatus.Active, ct);

        if (existing is not null)
        {
            return existing.Id;
        }

        var enrollment = new ProgramEnrollment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            TemplateId = template.Id,
            Timezone = DefaultTimezone,
            Status = ProgramEnrollmentStatus.Active,
            StartedAt = startLocalDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            StartLocalDate = startLocalDate,
            CurrentWeekNumber = currentWeekNumber,
            CreatedAt = DateTime.UtcNow,
        };

        db.ProgramEnrollments.Add(enrollment);

        db.StreakStates.Add(new StreakState
        {
            EnrollmentId = enrollment.Id,
            CurrentStreak = 0,
            LongestStreak = 0,
            FreezesRemaining = 0,
            FreezesUsedTotal = 0,
            MultiplierActive = 1.0m,
        });

        // Crear las 83 semanas
        var snapshotJson = BuildTasksSnapshot(template.DayTemplates);
        for (var w = 1; w <= template.TotalWeeks; w++)
        {
            var weekStart = startLocalDate.AddDays((w - 1) * 7);
            var status = w < currentWeekNumber
                ? ProgramWeekStatus.Completed
                : (w == currentWeekNumber ? ProgramWeekStatus.Active : ProgramWeekStatus.Locked);

            db.ProgramWeeks.Add(new ProgramWeek
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollment.Id,
                WeekNumber = w,
                Status = status,
                WeekStartDateLocal = weekStart,
                WeekEndDateLocal = weekStart.AddDays(6),
                TasksSnapshot = w <= currentWeekNumber ? snapshotJson : BuildEmptySnapshot(),
                TemplateVersionAtStart = template.Version,
                ActivatedAt = w <= currentWeekNumber ? weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null,
                CompletedAt = w < currentWeekNumber ? weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : null,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        return enrollment.Id;
    }

    private static JsonElement BuildTasksSnapshot(IEnumerable<WeeklyDayTemplate> dayTemplates)
    {
        var list = dayTemplates
            .OrderBy(d => d.Weekday)
            .ThenBy(d => d.SortOrder)
            .Select(d => new
            {
                weekday = d.Weekday,
                task_code = d.TaskCode.ToString(),
                points = d.Points,
                sort_order = d.SortOrder,
                media_id = d.MediaId,
            })
            .ToList();

        return JsonSerializer.SerializeToElement(list);
    }

    private static JsonElement BuildEmptySnapshot() =>
        JsonSerializer.SerializeToElement(Array.Empty<object>());

    // =========================================================================
    // 6. ASIGNACIONES DE NUTRICIÓN Y EJERCICIO
    // =========================================================================

    private async Task EnsureNutritionPlanAssignmentAsync(
        Guid patientId,
        Guid planId,
        DateOnly startLocalDate,
        int totalWeeks,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await db.NutritionPlanAssignments
            .AnyAsync(a => a.PatientId == patientId && a.PlanId == planId, ct);

        if (exists)
        {
            return;
        }

        var start = startLocalDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = startLocalDate.AddDays(totalWeeks * 7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        db.NutritionPlanAssignments.Add(new NutritionPlanAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            PlanId = planId,
            StartDate = start,
            EndDate = end,
            Status = AssignmentStatus.Active,
            Notes = "Asignación del programa integral de 83 semanas.",
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureRoutineAssignmentsAsync(
        Guid patientId,
        Dictionary<int, Guid> routineMap,
        DateOnly startLocalDate,
        int totalWeeks,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existingCount = await db.RoutineAssignments
            .CountAsync(a => a.PatientId == patientId && a.Status == AssignmentStatus.Active, ct);

        if (existingCount >= 5)
        {
            return;
        }

        var start = startLocalDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = startLocalDate.AddDays(totalWeeks * 7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        for (var dow = 0; dow < 5; dow++)
        {
            if (!routineMap.TryGetValue(dow, out var routineId) || routineId == Guid.Empty)
            {
                continue;
            }

            db.RoutineAssignments.Add(new RoutineAssignment
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                RoutineId = routineId,
                StartDate = start,
                EndDate = end,
                Frequency = AssignmentFrequency.Personalizada,
                Status = AssignmentStatus.Active,
                Notes = $"Rutina para el día {dow + 1} de la semana.",
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    // =========================================================================
    // 7. SIMULACIÓN DE PROGRESO GAMIFICADO Y EXPERIENCIA (XP)
    // =========================================================================

    private async Task<bool> SeedPatientProgressAsync(
        Guid patientId,
        Guid enrollmentId,
        ProgramTemplate template,
        Guid nutritionPlanId,
        Dictionary<int, Guid> routineMap,
        List<Guid> mediaIds,
        Dictionary<string, Guid> habitTemplateMap,
        Dictionary<string, (Guid MetricId, Guid DefaultUnitId)> clinicalMetricMap,
        DateOnly startLocalDate,
        DateOnly today,
        int tier,
        int patientIndex,
        Guid clinicianUserId,
        CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Idempotencia: si ya existen task_completions para este enrollment, no re-sembrar progreso
        var existingCompletions = await db.TaskCompletions.CountAsync(t => t.EnrollmentId == enrollmentId, ct);
        if (existingCompletions > 0)
        {
            return false;
        }

        var weeks = await db.ProgramWeeks
            .Where(w => w.EnrollmentId == enrollmentId)
            .OrderBy(w => w.WeekNumber)
            .ToListAsync(ct);

        var weekByDate = weeks.ToDictionary(w => w.WeekStartDateLocal, w => w);

        var totalDays = Math.Max(1, today.DayNumber - startLocalDate.DayNumber + 1);
        if (tier == 4)
        {
            totalDays = Math.Min(totalDays, 4); // Tier 4: max 4 días recientes
        }

        var runningXp = 0;
        var currentStreak = 0;
        var longestStreak = 0;
        var nbCurrentStreak = 0;
        var nbLongestStreak = 0;
        DateOnly? lastActiveDate = null;
        DateOnly? nbLastDate = null;
        var perfectDaysCount = 0;
        var freezesRemaining = 0;
        var freezesUsedTotal = 0;

        var moodOptions = new short[] { 4, 5, 4, 5, 3, 5, 4 };

        for (var d = 0; d < totalDays; d++)
        {
            var date = startLocalDate.AddDays(d);
            if (date > today)
            {
                break;
            }

            var weekday = (short)(((int)date.DayOfWeek + 6) % 7 + 1); // 1=Lunes..7=Domingo
            var dow0 = weekday - 1;

            var weekStart = GetMondayOfDate(date);
            var week = weeks.FirstOrDefault(w => w.WeekStartDateLocal == weekStart) ?? weeks.First();

            // Simulación de día perdido en Tier 3 (para mostrar el rescate de racha con congelamiento)
            var isMissedDayTier3 = tier == 3 && d == 10; // día 11 perdido

            if (isMissedDayTier3)
            {
                // Usar congelamiento si estaba disponible
                freezesUsedTotal++;
                freezesRemaining = Math.Max(0, freezesRemaining - 1);
                db.StreakFreezes.Add(new StreakFreeze
                {
                    Id = Guid.NewGuid(),
                    EnrollmentId = enrollmentId,
                    Kind = StreakFreezeKind.Consumed,
                    UsedOnLocalDate = date,
                    GrantedReason = "StreakRescue",
                    CreatedAt = DateTime.UtcNow,
                });
                continue;
            }

            var isPerfectDay = true;
            var dayPoints = 0;
            var mood = moodOptions[(d + patientIndex) % moodOptions.Length];

            // Crear DailyCheckIn
            var checkIn = new DailyCheckIn
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                ProgramWeekId = week.Id,
                LocalDate = date,
                Weekday = weekday,
                MoodScore = mood,
                TotalPoints = 0,
                BonusAwarded = 0,
                IsPerfectDay = false,
                CreatedAt = date.ToDateTime(new TimeOnly(20, 0), DateTimeKind.Utc),
            };

            db.DailyCheckIns.Add(checkIn);

            // Registro emocional independiente
            var emotionalRecord = new EmotionalRecord
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                ProgramEnrollmentId = enrollmentId,
                RecordedLocalDate = date,
                MoodScore = mood,
                Notes = mood >= 4 ? "Excelente energía y cumplimiento de metas del día." : "Buen día en general, algo de fatiga laboral.",
                CreatedAt = date.ToDateTime(new TimeOnly(20, 0), DateTimeKind.Utc),
            };
            db.EmotionalRecords.Add(emotionalRecord);

            // Completar las 6 tareas del día
            foreach (var (taskCode, basePoints) in StandardTasks)
            {
                Guid? routineId = null;
                if (taskCode == TaskCode.ejercicio && routineMap.TryGetValue(dow0, out var rId))
                {
                    routineId = rId;
                }

                Guid? mediaId = null;
                if (taskCode == TaskCode.podcast && mediaIds.Count > 0)
                {
                    mediaId = mediaIds[(d + (int)taskCode) % mediaIds.Count];
                }

                var taskCompletion = new TaskCompletion
                {
                    Id = Guid.NewGuid(),
                    EnrollmentId = enrollmentId,
                    ProgramWeekId = week.Id,
                    DailyCheckinId = checkIn.Id,
                    LocalDate = date,
                    Weekday = weekday,
                    TaskCode = taskCode,
                    PointsAwarded = basePoints,
                    SourceRefType = taskCode switch
                    {
                        TaskCode.ejercicio => "auto_exercise",
                        TaskCode.nut => "auto_nutrition",
                        TaskCode.podcast => "auto_media",
                        TaskCode.emocional => "manual",
                        _ => "manual",
                    },
                    NutritionPlanId = taskCode == TaskCode.nut ? nutritionPlanId : null,
                    NutritionPlanDayNumber = taskCode == TaskCode.nut ? weekday : null,
                    ExerciseRoutineId = routineId,
                    MediaId = mediaId,
                    EmotionalRecordId = taskCode == TaskCode.emocional ? emotionalRecord.Id : null,
                    CompletedAt = date.ToDateTime(new TimeOnly(8 + (int)taskCode * 2, 0), DateTimeKind.Utc),
                };

                db.TaskCompletions.Add(taskCompletion);
                dayPoints += basePoints;

                // XP Ledger por completación de tarea
                runningXp += basePoints;
                db.XpLedgerEntries.Add(new XpLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    EnrollmentId = enrollmentId,
                    Amount = basePoints,
                    Reason = XpReason.TaskCompletion,
                    SourceRefType = "task_completion",
                    SourceRefId = taskCompletion.Id,
                    RuleCode = taskCode switch
                    {
                        TaskCode.podcast => XpRuleCodes.TaskPodcast,
                        TaskCode.vitals => XpRuleCodes.TaskVitals,
                        TaskCode.nut => XpRuleCodes.TaskNut,
                        TaskCode.ejercicio => XpRuleCodes.TaskEjercicio,
                        TaskCode.nutraceutico => XpRuleCodes.TaskNutraceutico,
                        TaskCode.emocional => XpRuleCodes.TaskEmocional,
                        _ => null,
                    },
                    BalanceAfter = runningXp,
                    AwardedAt = taskCompletion.CompletedAt,
                    MultiplierUsed = 1.0m,
                });
            }

            // Bonus de día perfecto (+50 XP)
            const int dayBonus = 50;
            checkIn.TotalPoints = dayPoints;
            checkIn.BonusAwarded = dayBonus;
            checkIn.IsPerfectDay = isPerfectDay;

            runningXp += dayBonus;
            db.XpLedgerEntries.Add(new XpLedgerEntry
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                Amount = dayBonus,
                Reason = XpReason.DailyBonus,
                SourceRefType = "daily_bonus",
                SourceRefId = checkIn.Id,
                RuleCode = XpRuleCodes.DayBonus,
                BalanceAfter = runningXp,
                AwardedAt = date.ToDateTime(new TimeOnly(21, 0), DateTimeKind.Utc),
                MultiplierUsed = 1.0m,
            });

            // Actualizar contadores de racha
            currentStreak++;
            longestStreak = Math.Max(longestStreak, currentStreak);
            nbCurrentStreak++;
            nbLongestStreak = Math.Max(nbLongestStreak, nbCurrentStreak);
            lastActiveDate = date;
            nbLastDate = date;
            perfectDaysCount++;

            // Ganar congelamiento de racha cada 7 días perfectos (máx 3)
            if (perfectDaysCount % 7 == 0 && freezesRemaining < 3)
            {
                freezesRemaining++;
                db.StreakFreezes.Add(new StreakFreeze
                {
                    Id = Guid.NewGuid(),
                    EnrollmentId = enrollmentId,
                    Kind = StreakFreezeKind.Granted,
                    GrantedAt = date.ToDateTime(new TimeOnly(21, 30), DateTimeKind.Utc),
                    GrantedReason = "PerfectWeekBonus",
                    CreatedAt = DateTime.UtcNow,
                });
            }

            // Hitos de racha general
            AwardStreakMilestoneIfNeeded(db, enrollmentId, currentStreak, ref runningXp, date);

            // Hitos de racha nutracéutico
            AwardNbMilestoneIfNeeded(db, enrollmentId, nbCurrentStreak, ref runningXp, date);

            // Hábitos granulares de nutrición en app.habit_checks
            foreach (var habitCode in new[] { "des", "alm", "mer", "cen", "agua" })
            {
                if (habitTemplateMap.TryGetValue(habitCode, out var hId))
                {
                    db.HabitChecks.Add(new HabitCheck
                    {
                        Id = Guid.NewGuid(),
                        PatientId = patientId,
                        HabitTemplateId = hId,
                        LocalDate = date,
                        IsDone = true,
                        CreatedAt = date.ToDateTime(new TimeOnly(19, 0), DateTimeKind.Utc),
                    });
                }
            }
        }

        // Actualizar StreakState final
        var streakState = await db.StreakStates.FirstAsync(s => s.EnrollmentId == enrollmentId, ct);
        streakState.CurrentStreak = currentStreak;
        streakState.LongestStreak = longestStreak;
        streakState.LastActiveDate = lastActiveDate;
        streakState.FreezesRemaining = freezesRemaining;
        streakState.FreezesUsedTotal = freezesUsedTotal;
        streakState.NbCurrentStreak = (short)nbCurrentStreak;
        streakState.NbLongestStreak = (short)nbLongestStreak;
        streakState.NbLastCompletedDate = nbLastDate;
        streakState.MultiplierActive = (currentStreak >= 11 && currentStreak < 14) || (currentStreak >= 22 && currentStreak < 26) ? 2.0m : 1.0m;
        streakState.UpdatedAt = DateTime.UtcNow;

        // Sembrar HealthScores y TransformationScores para semanas completadas
        var completedWeeksCount = totalDays / 7;
        for (var w = 1; w <= completedWeeksCount; w++)
        {
            var pStart = startLocalDate.AddDays((w - 1) * 7);
            var pEnd = pStart.AddDays(6);

            var baseScore = tier switch
            {
                1 => 82 + w * 2,  // 84, 86, 88, 90, 92...
                2 => 70 + w * 3,  // 73, 76, 79...
                3 => 63 + w * 2,  // 65, 67...
                _ => 60,
            };
            baseScore = Math.Clamp(baseScore, 50, 96);
            var prevScore = Math.Clamp(baseScore - 3, 45, 90);

            db.HealthScores.Add(new HealthScore
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                Score = (short)baseScore,
                ScorePrevious = (short)prevScore,
                ScoreAdherence = (short)Math.Min(100, baseScore + 4),
                ScoreClinical = (short)Math.Min(100, baseScore - 2),
                ScoreNutrition = (short)Math.Min(100, baseScore + 2),
                ScorePsychology = (short)Math.Min(100, baseScore + 1),
                ScoreExercise = (short)Math.Min(100, baseScore),
                Trend = ScoreTrend.up,
                PeriodStart = pStart,
                PeriodEnd = pEnd,
                CalculatedAt = pEnd.ToDateTime(new TimeOnly(23, 59), DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow,
            });

            db.TransformationScores.Add(new TransformationScore
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                Score = (short)Math.Clamp(baseScore - 2, 40, 95),
                ScorePrevious = (short)Math.Clamp(prevScore - 2, 35, 90),
                WeekNumber = w,
                OverallTrend = ScoreTrend.up,
                Detail = JsonSerializer.SerializeToElement(new Dictionary<string, object>
                {
                    ["weight"] = new
                    {
                        baseline = 90.0m,
                        current = Math.Max(76.0m, 90.0m - (1.2m * w)),
                        unit = "kg",
                        delta = -1.2m * w,
                        delta_pct = Math.Round(-((1.2m * w) / 90.0m) * 100m, 1),
                        favorable = true,
                        score = Math.Min(100, 70 + (w * 3)),
                    },
                    ["glucose_fasting"] = new
                    {
                        baseline = 118.0m,
                        current = Math.Max(88.0m, 118.0m - (2.1m * w)),
                        unit = "mg/dL",
                        delta = -2.1m * w,
                        delta_pct = Math.Round(-((2.1m * w) / 118.0m) * 100m, 1),
                        favorable = true,
                        score = Math.Min(100, 68 + (w * 4)),
                    },
                    ["adherence"] = new
                    {
                        baseline = 50.0m,
                        current = 92.0m,
                        unit = "%",
                        delta = 42.0m,
                        delta_pct = 84.0m,
                        favorable = true,
                        score = 92,
                    },
                }),
                CalculatedAt = pEnd.ToDateTime(new TimeOnly(23, 59), DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow,
            });
        }

        // Sembrar líneas base clínicas (ClinicalBaselines) para el paciente
        await EnsureClinicalBaselinesAsync(db, patientId, startLocalDate, clinicalMetricMap, tier, clinicianUserId);

        await db.SaveChangesAsync(ct);
        return true;
    }

    private static void AwardStreakMilestoneIfNeeded(
        AppDbContext db,
        Guid enrollmentId,
        int streak,
        ref int runningXp,
        DateOnly date)
    {
        var (points, reason, ruleCode) = streak switch
        {
            7 => (100, XpReason.STREAK_7, XpRuleCodes.Streak7),
            11 => (200, XpReason.STREAK_11, XpRuleCodes.Streak11),
            14 => (300, XpReason.TaskCompletion, XpRuleCodes.Streak14),
            22 => (500, XpReason.STREAK_22, XpRuleCodes.Streak22),
            30 => (800, XpReason.TaskCompletion, XpRuleCodes.Streak30),
            50 => (1500, XpReason.STREAK_50, XpRuleCodes.Streak50),
            _ => (0, XpReason.TaskCompletion, null),
        };

        if (points > 0 && ruleCode is not null)
        {
            runningXp += points;
            db.XpLedgerEntries.Add(new XpLedgerEntry
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                Amount = points,
                Reason = reason,
                SourceRefType = "streak_milestone",
                RuleCode = ruleCode,
                BalanceAfter = runningXp,
                AwardedAt = date.ToDateTime(new TimeOnly(21, 15), DateTimeKind.Utc),
                MultiplierUsed = 1.0m,
            });
        }
    }

    private static void AwardNbMilestoneIfNeeded(
        AppDbContext db,
        Guid enrollmentId,
        int nbStreak,
        ref int runningXp,
        DateOnly date)
    {
        var (points, reason, ruleCode) = nbStreak switch
        {
            7 => (50, XpReason.NB_STREAK_7, XpRuleCodes.NbStreak7),
            14 => (100, XpReason.NB_STREAK_14, XpRuleCodes.NbStreak14),
            30 => (250, XpReason.NB_STREAK_30, XpRuleCodes.NbStreak30),
            60 => (500, XpReason.NB_STREAK_60, XpRuleCodes.NbStreak60),
            90 => (1000, XpReason.NB_STREAK_90, XpRuleCodes.NbStreak90),
            _ => (0, XpReason.TaskCompletion, null),
        };

        if (points > 0 && ruleCode is not null)
        {
            runningXp += points;
            db.XpLedgerEntries.Add(new XpLedgerEntry
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                Amount = points,
                Reason = reason,
                SourceRefType = "nb_milestone",
                RuleCode = ruleCode,
                BalanceAfter = runningXp,
                AwardedAt = date.ToDateTime(new TimeOnly(21, 20), DateTimeKind.Utc),
                MultiplierUsed = 1.0m,
            });
        }
    }

    private static async Task EnsureClinicalBaselinesAsync(
        AppDbContext db,
        Guid patientId,
        DateOnly measuredAt,
        Dictionary<string, (Guid MetricId, Guid DefaultUnitId)> metricMap,
        int tier,
        Guid clinicianUserId)
    {
        if (clinicianUserId == Guid.Empty)
        {
            return;
        }

        var baselines = new (string MetricCode, decimal BaselineVal, decimal TargetVal)[]
        {
            ("weight", tier == 1 ? 84.5m : 92.0m, 76.0m),
            ("glucose_fasting", tier == 1 ? 104.0m : 122.0m, 88.0m),
            ("hba1c", tier == 1 ? 5.9m : 6.8m, 5.3m),
            ("systolic_bp", tier == 1 ? 128.0m : 138.0m, 118.0m),
            ("diastolic_bp", tier == 1 ? 82.0m : 88.0m, 76.0m),
        };

        foreach (var (code, baseVal, targetVal) in baselines)
        {
            if (!metricMap.TryGetValue(code, out var metricInfo))
            {
                continue;
            }

            var exists = await db.ClinicalBaselines
                .AnyAsync(b => b.PatientId == patientId && b.MetricId == metricInfo.MetricId);

            if (exists)
            {
                continue;
            }

            db.ClinicalBaselines.Add(new ClinicalBaseline
            {
                Id = Guid.NewGuid(),
                PatientId = patientId,
                MetricId = metricInfo.MetricId,
                UnitId = metricInfo.DefaultUnitId,
                Value = baseVal,
                TargetValue = targetVal,
                FavorableDirection = FavorableDirection.LowerIsBetter,
                MeasuredAt = measuredAt,
                SetBy = clinicianUserId,
                CreatedAt = DateTime.UtcNow,
            });
        }
    }

    // =========================================================================
    // UTILIDADES
    // =========================================================================

    private async Task<Guid> GetDefaultClinicianUserIdAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var id = await db.Database.SqlQueryRaw<Guid>(
            @"SELECT ""Id"" AS ""Value"" FROM auth.""Users"" WHERE ""Email"" = 'admin@coppaddresd.com' OR ""Email"" LIKE '%admin%' LIMIT 1"
        ).FirstOrDefaultAsync(ct);

        if (id == Guid.Empty)
        {
            id = await db.Database.SqlQueryRaw<Guid>(
                @"SELECT ""Id"" AS ""Value"" FROM auth.""Users"" LIMIT 1"
            ).FirstOrDefaultAsync(ct);
        }

        return id;
    }

    private static DateOnly GetMondayOfDate(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-daysSinceMonday);
    }

    private async Task<T> WithContext<T>(Func<AppDbContext, Task<T>> action, CancellationToken ct)
    {
        using var ctxScope = scopeFactory.CreateScope();
        var db = ctxScope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }
}
