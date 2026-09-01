using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Domain.Enums.ProgramProgress;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seeder de desarrollo: al arrancar la API inscribe automáticamente a la
/// paciente dev (configurada vía <c>DevPatient:Email</c>) en el programa por
/// defecto <c>default-83w</c>, crea un plan de alimentación de ejemplo
/// ("Plan Keto de ejemplo") y asigna contenido para las primeras 4 semanas:
/// el plan de alimentación semana a semana y una rutina de ejercicio por día
/// hábil (lunes a viernes), dejando sábado/domingo como descanso.
///
/// Idempotente en dos niveles:
/// 1. Si <c>DevPatient:Email</c> no está configurado → no hace nada (no-op).
/// 2. Si la paciente ya tiene una inscripción activa, se omite TODO el seed.
/// Además, la creación del plan de alimentación es idempotente por nombre, y las
/// rutinas de ejercicio ya son sembradas por <see cref="ExerciseRoutineSeeder"/>.
///
/// Se registra DESPUÉS de <see cref="ProgramProgressSeeder"/> y
/// <see cref="ExerciseRoutineSeeder"/> en <c>Program.cs</c>, de modo que la
/// plantilla <c>default-83w</c> y el catálogo de rutinas existan antes de
/// ejecutarlo.
/// </summary>
public sealed class DevProgramSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DevProgramSeeder> logger) : IHostedService
{
    private const string KetoPlanName = "Plan Keto de ejemplo";
    private const string DefaultTemplateCode = "default-83w";
    private const string PatientTimezone = "America/Bogota";

    /// <summary>Rutinas que deben existir (se siembran en ExerciseRoutineSeeder).</summary>
    private static readonly string[] RoutineNames = ["Cardio Básico", "Pierna", "Cardio HIIT", "Espalda"];

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Seed del programa de desarrollo cancelado");
        }
        catch (Exception ex)
        {
            // El seeder es best-effort: un fallo no debe impedir que arranque la API.
            logger.LogError(ex, "Fallo el seed del programa de desarrollo");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        var email = configuration["DevPatient:Email"];
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogInformation(
                "DevPatient:Email no configurado: el seeder de programa de desarrollo no hace nada (no-op).");
            return;
        }

        // Los repositorios de aplicación son scoped: se resuelven en un scope propio.
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var programRepo = sp.GetRequiredService<IProgramRepository>();
        var wellnessRepo = sp.GetRequiredService<IWellnessRepository>();

        // Paso 1: buscar la paciente dev por email (status='Activo').
        var patient = await WithContext(
            db => db.PatientProfiles
                .Where(p => p.Email == email && p.Status == "Activo")
                .Select(p => new { p.Id, p.UserId })
                .AsNoTracking()
                .FirstOrDefaultAsync(ct), ct);

        if (patient is null)
        {
            logger.LogWarning(
                "Paciente dev con email {Email} no encontrado (status='Activo'): seed omitido.", email);
            return;
        }

        // Paso 2: si ya existe una inscripción activa, omitir todo.
        var existingEnrollmentId = await WithContext(
            db => db.ProgramEnrollments
                .Where(e => e.PatientId == patient.Id && e.Status == ProgramEnrollmentStatus.Active)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(ct), ct);

        if (existingEnrollmentId != Guid.Empty)
        {
            logger.LogInformation(
                "Dev patient already enrolled, skipping (enrollment={EnrollmentId})", existingEnrollmentId);
            return;
        }

        // Paso 3: crear la inscripción en default-83w usando el mismo método del endpoint.
        var template = await programRepo.GetTemplateByCodeAsync(DefaultTemplateCode, ct);
        if (template is null)
        {
            logger.LogWarning(
                "Plantilla {Code} no encontrada: no se puede inscribir a la paciente dev. Seed omitido.",
                DefaultTemplateCode);
            return;
        }

        var startLocalDate = TodayMonday();
        var enrollment = await programRepo.EnrollAsync(
            patient.Id, template.Id, PatientTimezone, startLocalDate, null, ct);

        // Paso 4: plan de alimentación de ejemplo (idempotente por nombre).
        var planId = await GetOrCreateKetoPlanAsync(wellnessRepo, patient.Id, ct);

        // Paso 5: asignar el plan de alimentación a las semanas 1-4.
        for (var week = 1; week <= 4; week++)
        {
            var weekStart = enrollment.StartLocalDate.AddDays((week - 1) * 7);
            await wellnessRepo.AddPlanAssignmentAsync(new NutritionPlanAssignment
            {
                PatientId = patient.Id,
                PlanId = planId,
                StartDate = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                // Fin exclusivo: cubre toda la semana (lunes 00:00 -> lunes siguiente 00:00).
                EndDate = weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Status = AssignmentStatus.Active,
            }, ct);
        }

        // Paso 6: asignar una rutina por día hábil (lunes a viernes) para las semanas 1-4.
        var routineByName = await WithContext(
            db => db.ExerciseRoutines
                .Where(r => RoutineNames.Contains(r.Name))
                .Select(r => new { r.Id, r.Name })
                .AsNoTracking()
                .ToListAsync(ct), ct);

        var routineMap = routineByName.ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var routineAssignmentCount = 0;
        for (var week = 1; week <= 4; week++)
        {
            var weekStart = enrollment.StartLocalDate.AddDays((week - 1) * 7);
            for (var dow = 0; dow < 7; dow++)
            {
                var routineName = RoutineForWeekday(dow);
                if (routineName is null)
                {
                    continue; // sábado (5) / domingo (6) = descanso, sin asignación.
                }

                if (!routineMap.TryGetValue(routineName, out var routineId))
                {
                    logger.LogWarning(
                        "Rutina '{Routine}' no encontrada: se omite la asignación del día.", routineName);
                    continue;
                }

                var day = weekStart.AddDays(dow);
                await wellnessRepo.AddAssignmentAsync(new RoutineAssignment
                {
                    PatientId = patient.Id,
                    RoutineId = routineId,
                    StartDate = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    // Fin exclusivo: cubre todo el día de la rutina.
                    EndDate = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    Status = AssignmentStatus.Active,
                }, ct);
                routineAssignmentCount++;
            }
        }

        // Paso 7: resumen.
        logger.LogInformation(
            "Dev program seeded: patient={Email}, enrollment={EnrollmentId}, nutritionPlan={PlanId}, routineAssignments={Count}",
            email, enrollment.Id, planId, routineAssignmentCount);
    }

    /// <summary>
    /// Crea el plan de alimentación "Plan Keto de ejemplo" (7 días, 3 comidas
    /// cada uno) con recetas ricas y detalladas si no existe, o actualiza sus días
    /// si ya existía. Idempotente por nombre.
    /// </summary>
    private async Task<Guid> GetOrCreateKetoPlanAsync(
        IWellnessRepository wellnessRepo, Guid patientId, CancellationToken ct)
    {
        var existingPlan = await WithContext(
            db => db.NutritionPlans
                .Include(p => p.Days)
                .Where(p => p.Name == KetoPlanName)
                .FirstOrDefaultAsync(ct), ct);

        if (existingPlan is not null)
        {
            // Si ya existe pero no tiene los 21 días o queremos refrescarlos con las descripciones ricas:
            if (existingPlan.Days.Count < 21)
            {
                await WithContext(async db =>
                {
                    db.NutritionPlanDays.RemoveRange(db.NutritionPlanDays.Where(d => d.PlanId == existingPlan.Id));
                    await db.SaveChangesAsync(ct);
                    return true;
                }, ct);

                var richDays = BuildKetoPlanDays(existingPlan.Id);
                await wellnessRepo.AddPlanDaysRangeAsync(richDays, ct);
                logger.LogInformation(
                    "Plan de alimentación '{Plan}' actualizado con {Days} comidas detalladas.",
                    KetoPlanName, richDays.Count);
            }
            else
            {
                logger.LogInformation(
                    "Plan de alimentación '{Plan}' ya existe con {Days} días (Id={PlanId}).",
                    KetoPlanName, existingPlan.Days.Count, existingPlan.Id);
            }

            return existingPlan.Id;
        }

        var plan = new NutritionPlan
        {
            Name = KetoPlanName,
            Status = NutritionPlanStatus.Active,
            DurationDays = 7,
            DailyCalorieTarget = 1800,
            DailyProteinTarget = 90,
            DailyCarbsTarget = 30,
            DailyFatTarget = 140,
            DailyFiberTarget = 25,
            IsTemplate = false,
            PatientId = patientId,
        };
        plan = await wellnessRepo.AddPlanAsync(plan, ct);

        var days = BuildKetoPlanDays(plan.Id);
        await wellnessRepo.AddPlanDaysRangeAsync(days, ct);

        logger.LogInformation(
            "Plan de alimentación '{Plan}' creado (Id={PlanId}, {Days} comidas).", KetoPlanName, plan.Id, days.Count);
        return plan.Id;
    }

    private static List<NutritionPlanDay> BuildKetoPlanDays(Guid planId) =>
    [
        // Día 1: Lunes
        new()
        {
            PlanId = planId,
            DayNumber = 1,
            MealType = MealType.Desayuno,
            SortOrder = 1,
            Description = "Omelette de espinacas tiernas, queso feta y aguacate con infusión de té verde",
            Foods = "3 huevos camperos, 60g espinaca tierna, 40g queso feta, 1/2 aguacate hass, 1 cdta aceite de oliva extra virgen, té verde",
            Calories = 460,
            ProteinG = 25,
            CarbsG = 5,
            FatG = 38,
            FiberG = 7,
            WaterMl = 500,
            Notes = "Cocinar a fuego medio para preservar los nutrientes de la espinaca y no quemar las grasas saludables.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 1,
            MealType = MealType.Almuerzo,
            SortOrder = 2,
            Description = "Bowl keto de pechuga de pollo marinada al limón con espárragos trigueros y rúcula",
            Foods = "220g pechuga de pollo, 120g espárragos trigueros, 80g rúcula, 1/2 aguacate, 2 cdas aceite de oliva extra virgen, semillas de chía",
            Calories = 680,
            ProteinG = 48,
            CarbsG = 7,
            FatG = 50,
            FiberG = 8,
            WaterMl = 750,
            Notes = "Marinar el pollo con limón, orégano y ajo antes de sellar a la plancha.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 1,
            MealType = MealType.Cena,
            SortOrder = 3,
            Description = "Filete de salmón salvaje al horno con floretes de brócoli al vapor y mantequilla clarificada (ghee)",
            Foods = "200g filete de salmón, 180g brócoli, 15g mantequilla ghee, 30g queso parmesano rallado, sal rosa del Himalaya",
            Calories = 580,
            ProteinG = 38,
            CarbsG = 6,
            FatG = 44,
            FiberG = 6,
            WaterMl = 500,
            Notes = "Cenar al menos 2.5 horas antes de acostarse para optimizar la autofagia y el descanso nocturno.",
        },

        // Día 2: Martes
        new()
        {
            PlanId = planId,
            DayNumber = 2,
            MealType = MealType.Desayuno,
            SortOrder = 1,
            Description = "Huevos pochados sobre rodajas de aguacate y jamón serrano con café negro",
            Foods = "3 huevos pochados, 1/2 aguacate, 50g jamón serrano, 1 cdta aceite MCT, café espresso sin endulzar",
            Calories = 480,
            ProteinG = 28,
            CarbsG = 4,
            FatG = 40,
            FiberG = 6,
            WaterMl = 500,
            Notes = "El aceite MCT en el café aporta energía rápida y favorece la producción de cuerpos cetónicos.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 2,
            MealType = MealType.Almuerzo,
            SortOrder = 2,
            Description = "Medallones de lomo de res a la parrilla con ensalada césar keto y lascas de parmesano",
            Foods = "220g lomo de res, lechuga romana fresca, 35g queso parmesano en lascas, aderezo césar casero (aceite de oliva, yema, anchoas y mostaza dijon)",
            Calories = 710,
            ProteinG = 52,
            CarbsG = 5,
            FatG = 54,
            FiberG = 4,
            WaterMl = 750,
            Notes = "Preparar el aderezo sin azúcares añadidos ni aceites vegetales refinados.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 2,
            MealType = MealType.Cena,
            SortOrder = 3,
            Description = "Pechuga de pavo al romero con salteado de calabacín y champiñones al ajillo",
            Foods = "200g pechuga de pavo, 150g calabacín en rodajas, 100g champiñones laminados, 2 dientes de ajo, 2 cdas aceite de oliva",
            Calories = 530,
            ProteinG = 40,
            CarbsG = 7,
            FatG = 38,
            FiberG = 6,
            WaterMl = 500,
            Notes = "Saltear a fuego vivo para que las verduras queden al dente y conserven su textura crujiente.",
        },

        // Día 3: Miércoles
        new()
        {
            PlanId = planId,
            DayNumber = 3,
            MealType = MealType.Desayuno,
            SortOrder = 1,
            Description = "Revuelto keto de huevos con champiñones Portobello, espinacas y queso gouda",
            Foods = "3 huevos, 80g champiñones portobello, 50g espinaca fresca, 40g queso gouda curado, 1 cda mantequilla de pastoreo",
            Calories = 470,
            ProteinG = 26,
            CarbsG = 5,
            FatG = 39,
            FiberG = 5,
            WaterMl = 500,
            Notes = "Acompañar con infusión de jengibre y limón para estimular el sistema digestivo.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 3,
            MealType = MealType.Almuerzo,
            SortOrder = 2,
            Description = "Atún fresco sellado en costra de sésamo con ensalada de col morada y aguacate",
            Foods = "200g lomo de atún rojo, 15g semillas de sésamo, 100g col morada en juliana, 1/2 aguacate, 2 cdas aceite de sésamo y lima",
            Calories = 670,
            ProteinG = 47,
            CarbsG = 8,
            FatG = 49,
            FiberG = 7,
            WaterMl = 750,
            Notes = "Sellar el atún únicamente 1 minuto por lado para mantenerlo jugoso y preservar sus ácidos grasos Omega-3.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 3,
            MealType = MealType.Cena,
            SortOrder = 3,
            Description = "Contramuslos de pollo crujientes en air fryer con coliflor gratinada con mozzarella",
            Foods = "220g contramuslo de pollo con piel, 160g coliflor al vapor, 50g queso mozzarella rallado, pimentón dulce, orégano",
            Calories = 590,
            ProteinG = 42,
            CarbsG = 6,
            FatG = 43,
            FiberG = 6,
            WaterMl = 500,
            Notes = "Cocinar en air fryer a 190°C durante 22 minutos hasta lograr un dorado uniforme.",
        },

        // Día 4: Jueves
        new()
        {
            PlanId = planId,
            DayNumber = 4,
            MealType = MealType.Desayuno,
            SortOrder = 1,
            Description = "Frittata horneada de calabacín, queso de cabra y nueces picadas",
            Foods = "3 huevos, 100g calabacín rallado, 35g rulo de queso de cabra, 20g nueces de nogal picadas, aceite de oliva",
            Calories = 490,
            ProteinG = 24,
            CarbsG = 6,
            FatG = 41,
            FiberG = 5,
            WaterMl = 500,
            Notes = "Hornear a 180°C durante 15 minutos hasta que el centro esté firme.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 4,
            MealType = MealType.Almuerzo,
            SortOrder = 2,
            Description = "Costillas de cerdo braseadas con ensalada de aguacate, tomates secos y albahaca",
            Foods = "250g costillar de cerdo magro, 1 aguacate mediano, 30g tomates secos en aceite, hojas de albahaca fresca, sal gruesa",
            Calories = 720,
            ProteinG = 44,
            CarbsG = 7,
            FatG = 57,
            FiberG = 7,
            WaterMl = 750,
            Notes = "Brasear a fuego lento con romero y pimienta negra en grano.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 4,
            MealType = MealType.Cena,
            SortOrder = 3,
            Description = "Lomos de merluza en salsa verde keto con espárragos trigueros y huevo duro picado",
            Foods = "220g lomo de merluza fresca, 120g espárragos, 1 huevo duro, caldo de pescado casero, perejil picado, 2 cdas aceite de oliva",
            Calories = 520,
            ProteinG = 39,
            CarbsG = 5,
            FatG = 37,
            FiberG = 5,
            WaterMl = 500,
            Notes = "Reducir la salsa verde a fuego bajo para emulsionar sin harinas.",
        },

        // Día 5: Viernes
        new()
        {
            PlanId = planId,
            DayNumber = 5,
            MealType = MealType.Desayuno,
            SortOrder = 1,
            Description = "Huevos al plato con virutas de jamón ibérico, aceite de oliva virgen y espinacas tiernas",
            Foods = "3 huevos, 45g jamón ibérico de bellota, 60g espinacas salteadas, 1 cda aceite de oliva, café con leche de almendras sin azúcar",
            Calories = 480,
            ProteinG = 29,
            CarbsG = 3,
            FatG = 40,
            FiberG = 5,
            WaterMl = 500,
            Notes = "Servir caliente y añadir el jamón al final para que no se reseque.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 5,
            MealType = MealType.Almuerzo,
            SortOrder = 2,
            Description = "Hamburguesa de ternera de pastoreo (sin pan) con queso cheddar fundido, bacon crujiente y guacamole",
            Foods = "220g carne picada de ternera 100%, 40g queso cheddar curado, 2 tiras de bacon artesanal, 60g guacamole casero, rodajas de pepinillo",
            Calories = 740,
            ProteinG = 50,
            CarbsG = 6,
            FatG = 58,
            FiberG = 6,
            WaterMl = 750,
            Notes = "Acompañar con hojas de lechuga fresca como envoltorio o base.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 5,
            MealType = MealType.Cena,
            SortOrder = 3,
            Description = "Crema keto de espárragos y puerro con dados de queso azul y nueces tostadas",
            Foods = "200g espárragos verdes, 50g puerro, 50ml nata fresca para cocinar (35% MG), 35g queso azul Gorgonzola, 20g nueces tostadas",
            Calories = 510,
            ProteinG = 21,
            CarbsG = 8,
            FatG = 43,
            FiberG = 6,
            WaterMl = 500,
            Notes = "Triturar finamente hasta obtener una textura suave y sedosa.",
        },

        // Día 6: Sábado
        new()
        {
            PlanId = planId,
            DayNumber = 6,
            MealType = MealType.Desayuno,
            SortOrder = 1,
            Description = "Tortilla francesa esponjosa de 3 huevos con queso brie francés y aguacate laminado",
            Foods = "3 huevos camperos, 40g queso brie, 1/2 aguacate en láminas finas, 1 cda mantequilla, té negro Earl Grey",
            Calories = 470,
            ProteinG = 25,
            CarbsG = 4,
            FatG = 39,
            FiberG = 6,
            WaterMl = 500,
            Notes = "Batir enérgicamente los huevos con una pizca de sal para incorporar aire.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 6,
            MealType = MealType.Almuerzo,
            SortOrder = 2,
            Description = "Chuletillas de cordero lechal a la plancha con pimientos del padrón y ensalada verde",
            Foods = "240g chuletillas de cordero, 100g pimientos de padrón fritos en aceite de oliva, 80g mezcla de hojas verdes, escamas de sal marina",
            Calories = 700,
            ProteinG = 44,
            CarbsG = 5,
            FatG = 56,
            FiberG = 5,
            WaterMl = 750,
            Notes = "Dorar las chuletillas a fuego fuerte por fuera dejando el interior tierno.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 6,
            MealType = MealType.Cena,
            SortOrder = 3,
            Description = "Ceviche keto de corvina salvaje con aguacate en cubos, cebolla morada y cilantro fresco",
            Foods = "220g corvina fresca en dados, 1/2 aguacate en cubos, zumo de 3 limas frescas, 30g cebolla morada fina, cilantro picado, 1 cda aceite de oliva",
            Calories = 490,
            ProteinG = 38,
            CarbsG = 7,
            FatG = 34,
            FiberG = 6,
            WaterMl = 500,
            Notes = "Marinar el pescado en la lima fría durante no más de 8 minutos para conservar su tersura.",
        },

        // Día 7: Domingo
        new()
        {
            PlanId = planId,
            DayNumber = 7,
            MealType = MealType.Desayuno,
            SortOrder = 1,
            Description = "Shakshuka keto: huevos escalfados en salsa de tomate suave, pimiento asado, comino y queso feta",
            Foods = "3 huevos, 120g salsa de tomate triturado natural, 60g pimiento rojo asado, 35g queso feta desmenuzado, comino molido, cilantro",
            Calories = 460,
            ProteinG = 25,
            CarbsG = 8,
            FatG = 36,
            FiberG = 6,
            WaterMl = 500,
            Notes = "Cocinar tapado en sartén de hierro a fuego suave hasta que la clara cuaje y la yema quede líquida.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 7,
            MealType = MealType.Almuerzo,
            SortOrder = 2,
            Description = "Suprema de pollo al curry suave con leche de coco y falso arroz de coliflor salteado",
            Foods = "220g pechuga de pollo en tiras, 100ml leche de coco entera, 180g coliflor triturada estilo arroz, 1 cda curry en polvo, 1 cda aceite de coco",
            Calories = 690,
            ProteinG = 48,
            CarbsG = 9,
            FatG = 51,
            FiberG = 8,
            WaterMl = 750,
            Notes = "Saltear la coliflor rápidamente en sartén seca para que no suelte agua.",
        },
        new()
        {
            PlanId = planId,
            DayNumber = 7,
            MealType = MealType.Cena,
            SortOrder = 3,
            Description = "Carpaccio fino de solomillo de ternera con virutas de parmesano reggiano, rúcula y alcaparras",
            Foods = "180g solomillo de ternera en láminas finísimas, 40g parmesano reggiano 24 meses, 50g rúcula fresca, 15g alcaparras, 2 cdas aceite de oliva virgen extra, limón",
            Calories = 540,
            ProteinG = 36,
            CarbsG = 4,
            FatG = 42,
            FiberG = 4,
            WaterMl = 500,
            Notes = "Aliñar justo antes de servir con sal en escamas y pimienta negra recién molida.",
        },
    ];

    /// <summary>
    /// Mapa día-de-semana (0=lunes) → nombre de rutina. Sábado (5) y domingo (6)
    /// devuelven <c>null</c> (día de descanso, sin asignación).
    /// </summary>
    private static string? RoutineForWeekday(int dow) => dow switch
    {
        0 => "Cardio Básico", // lunes
        1 => "Pierna",        // martes
        2 => "Cardio HIIT",   // miércoles
        3 => "Espalda",       // jueves
        4 => "Cardio Básico", // viernes
        _ => null,            // sábado (5), domingo (6)
    };

    /// <summary>Lunes de la semana actual en la fecha de hoy (UTC).</summary>
    private static DateOnly TodayMonday()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7; // lunes = 0
        return today.AddDays(-daysSinceMonday);
    }

    /// <summary>
    /// Ejecuta una operación con un scope propio: cada llamada resuelve un
    /// DbContext distinto, evitando conflictos de tracking EF entre operaciones.
    /// </summary>
    private async Task<T> WithContext<T>(Func<AppDbContext, Task<T>> action, CancellationToken ct)
    {
        using var ctxScope = scopeFactory.CreateScope();
        var db = ctxScope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }
}
