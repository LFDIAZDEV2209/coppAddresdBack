using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Api.Seeders;

/// <summary>
/// Seeder idempotente de ~12 rutinas de ejercicio base en
/// <c>app.exercise_routines</c> + <c>app.routine_exercises</c>. Catálogo
/// plano para que el configurador de contenido del ERP pueda asignarlas
/// por día de semana. Re-runs nunca duplican: si una rutina con ese
/// <c>Name</c> (case-insensitive) ya existe, se omite.
/// </summary>
public sealed class ExerciseRoutineSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<ExerciseRoutineSeeder> logger) : IHostedService
{
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Seed de rutinas de ejercicio cancelado");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo el seed de rutinas de ejercicio");
        }
    }

    private async Task SeedAsync(CancellationToken ct)
    {
        var routines = GetRoutineSeeds();
        var seeded = 0;

        foreach (var routine in routines)
        {
            var exists = await WithContext(
                db => db.ExerciseRoutines.AnyAsync(
                    x => x.Name.ToLower() == routine.Name.ToLower(), ct), ct);

            if (exists)
            {
                continue;
            }

            var entity = new ExerciseRoutine
            {
                Name = routine.Name,
                Description = routine.Description,
                Difficulty = routine.Difficulty,
                EstimatedMinutes = routine.EstimatedMinutes,
                Category = routine.Category,
                Status = NutritionPlanStatus.Active,
                TargetMuscles = routine.TargetMuscles,
                Equipment = routine.Equipment,
                WarmupNotes = routine.WarmupNotes,
                CooldownNotes = routine.CooldownNotes,
            };

            foreach (var ex in routine.Exercises)
            {
                entity.Exercises.Add(new RoutineExercise
                {
                    Name = ex.Name,
                    Description = ex.Description,
                    Sets = ex.Sets,
                    Repetitions = ex.Repetitions,
                    DurationSecs = ex.DurationSecs,
                    RestSeconds = ex.RestSeconds,
                    TargetMuscle = ex.TargetMuscle,
                    Equipment = ex.Equipment,
                    Tempo = ex.Tempo,
                    Rpe = ex.Rpe,
                    Tips = ex.Tips,
                    SortOrder = ex.SortOrder,
                });
            }

            await WithContext(async db =>
            {
                db.ExerciseRoutines.Add(entity);
                await db.SaveChangesAsync(ct);
                return true;
            }, ct);

            seeded++;
            logger.LogInformation(
                "Rutina de ejercicio sembrada: {Name} ({ExerciseCount} ejercicios)",
                routine.Name, routine.Exercises.Count);
        }

        logger.LogInformation(
            "Seed de rutinas de ejercicio completado: {Seeded}/{Total} nuevas",
            seeded, routines.Count);
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

    // -----------------------------------------------------------------------
    // Catálogo de rutinas por defecto
    // -----------------------------------------------------------------------

    private static IReadOnlyList<RoutineSeed> GetRoutineSeeds()
    {
        return
        [
            // 1. Cardio Básico
            new RoutineSeed(
                Name: "Cardio Básico",
                Description: "Rutina de cardio de intensidad moderada ideal para principiantes o calentamiento. Enfocada en mejorar la resistencia cardiovascular con ejercicios de bajo impacto.",
                Category: RoutineCategory.Cardio,
                Difficulty: RoutineDifficulty.Moderado,
                EstimatedMinutes: 30,
                Equipment: "Ninguno",
                TargetMuscles: "Cardiovascular, Piernas",
                WarmupNotes: "5 minutos de marcha suave antes de comenzar.",
                CooldownNotes: "5 minutos de caminata lenta y estiramientos generales.",
                Exercises:
                [
                    new ExerciseSeed("Caminata rápida", "Caminar a ritmo constante elevando ligeramente el ritmo cardíaco. Mantener la espalda recta y los brazos en movimiento natural.", 1, 30, 60, null, "Cardiovascular", null, "3-1-3", null, null, 1),
                    new ExerciseSeed("Jumping jacks", "Abrir piernas y brazos simultáneamente saltando, cerrando al caer. Mantener el core activado durante todo el movimiento.", 3, 30, null, 45, "Cardiovascular, Hombros", null, "1-1-1", null, null, 2),
                    new ExerciseSeed("High knees", "Correr en el lugar elevando las rodillas al nivel de la cadera. Mantener el ritmo constante y el torso erguido.", 3, 30, null, 45, "Cuádriceps, Core", null, "1-0-1", null, null, 3),
                    new ExerciseSeed("Burpees", "Desde posición de pie, agacharse, colocar manos en el suelo, saltar pies hacia atrás en plancha, volver a posición de pie y saltar. Variantes: omitir el salto para principiantes.", 3, 10, null, 60, "Cardiovascular, Core, Cuádriceps", null, null, null, "Si es tu primera vez, omite el salto final.", 4),
                    new ExerciseSeed("Trote en el lugar", "Trotar suavemente en el sitio manteniendo un ritmo estable. Los brazos se mueven de forma relajada. Servicio de vuelta a la calma.", null, null, 300, 60, "Cardiovascular, Gemelos", null, "2-1-2", null, null, 5),
                ]),
            // 2. Cardio HIIT
            new RoutineSeed(
                Name: "Cardio HIIT",
                Description: "Entrenamiento de intervalos de alta intensidad (HIIT) para quemar grasa y mejorar el rendimiento cardiovascular. Requiere experiencia previa.",
                Category: RoutineCategory.Cardio,
                Difficulty: RoutineDifficulty.Dificil,
                EstimatedMinutes: 20,
                Equipment: "Ninguno",
                TargetMuscles: "Cardiovascular, Full body",
                WarmupNotes: "5 minutos de movilidad articular y trote suave.",
                CooldownNotes: "5 minutos de caminata y estiramientos profundos.",
                Exercises:
                [
                    new ExerciseSeed("Sprints", "Sprint máximo esprintando en el sitio o en un espacio corto. Mantener la intensidad máxima durante cada intervalo.", 8, null, 30, 90, "Cuádriceps, Cardiovascular", null, "1-0-1", 9, null, 1),
                    new ExerciseSeed("Burpees", "Burpee completo con salto explosivo. Mantener el ritmo alto durante las 15 repeticiones. Descanso entre series completo.", 4, 15, null, 60, "Full body, Core", null, "1-0-1", 8, null, 2),
                    new ExerciseSeed("Mountain climbers", "En posición de plancha, alternar rodillas hacia el pecho rápidamente. Mantener la cadera estable y el core contraído.", 4, null, 40, 60, "Core, Cuádriceps, Cardiovascular", null, "1-0-1", 8, null, 3),
                    new ExerciseSeed("Sentadilla con salto", "Sentadilla profunda seguida de un salto explosivo hacia arriba. Aterrizar suavemente con las rodillas ligeramente flexionadas.", 4, 15, null, 60, "Cuádriceps, Glúteos", null, "1-0-1", 8, null, 4),
                    new ExerciseSeed("Descanso activo", "Caminata suave permitiendo la recuperación entre intervalos. No detenerse completamente, mantener el cuerpo en movimiento.", 3, null, 60, 30, "Cardiovascular", null, "3-1-3", null, "Respirar profundo durante el descanso.", 5),
                ]),
            // 3. Caminata Activa
            new RoutineSeed(
                Name: "Caminata Activa",
                Description: "Rutina de bajo impacto ideal para todos los niveles. Combina marcha con ejercicios suaves de movilidad.",
                Category: RoutineCategory.Cardio,
                Difficulty: RoutineDifficulty.Facil,
                EstimatedMinutes: 30,
                Equipment: "Ninguno",
                TargetMuscles: "Cardiovascular, Piernas",
                WarmupNotes: "Comenzar con 2 minutos de marcha muy suave.",
                CooldownNotes: "3 minutos de caminata lenta y estiramientos de piernas.",
                Exercises:
                [
                    new ExerciseSeed("Marcha en el lugar", "Marchar en el sitio elevando las rodillas a altura moderada. Mover los brazos de forma natural.", null, null, 600, 30, "Cuádriceps, Cardiovascular", null, "2-1-2", null, null, 1),
                    new ExerciseSeed("Caminata a paso ligero", "Caminar a ritmo moderado-alto. Mantener el torso erguido y los pasos cortos y rápidos.", null, null, 900, 30, "Cardiovascular, Gemelos", null, "2-1-2", null, null, 2),
                    new ExerciseSeed("Estocadas caminando", "Dar pasos largos flexionando ambas rodillas a 90°. Alternar piernas. Mantener el torso erguido y el core activado.", 3, 20, null, 60, "Cuádriceps, Glúteos", null, "2-1-2", null, "Si el equilibrio es difícil, reduce la amplitud del paso.", 3),
                    new ExerciseSeed("Elevación de rodillas", "En posición de pie, elevar una rodilla al nivel de la cadera alternadamente. Mantener el ritmo controlado.", 3, null, 30, 45, "Cuádriceps, Core", null, "2-0-2", null, null, 4),
                ]),
            // 4. Pierna
            new RoutineSeed(
                Name: "Pierna",
                Description: "Rutina de fuerza enfocada en tren inferior: cuádriceps, glúteos, isquiotibiales y gemelos. Usa pesas para mayor intensidad.",
                Category: RoutineCategory.Fuerza,
                Difficulty: RoutineDifficulty.Moderado,
                EstimatedMinutes: 45,
                Equipment: "Pesas",
                TargetMuscles: "Cuádriceps, Glúteos, Isquiotibiales, Gemelos",
                WarmupNotes: "5 minutos de bicicleta estática o trote suave + movilidad de cadera.",
                CooldownNotes: "5 minutos de estiramientos de piernas (cuádriceps, isquiotibiales, gemelos).",
                Exercises:
                [
                    new ExerciseSeed("Sentadilla", "Sentadilla con barra o mancuernas. Bajar hasta que los muslos estén paralelos al suelo, manteniendo la espalda recta y las rodillas alineadas con los pies.", 4, 12, null, 90, "Cuádriceps, Glúteos", "Pesas", "3-1-3", 7, null, 1),
                    new ExerciseSeed("Peso muerto rumano", "Con mancuernas o barra, flexionar la cadera hacia atrás manteniendo la espalda recta. Sentir el estiramiento en isquiotibiales antes de volver a posición de pie.", 4, 10, null, 90, "Isquiotibiales, Glúteos, Erector espinal", "Pesas", "3-1-3", 7, null, 2),
                    new ExerciseSeed("Zancadas", "Dar un paso largo hacia adelante flexionando ambas rodillas a 90°. Alternar piernas. Mantener el torso erguido y el core activado.", 3, 12, null, 60, "Cuádriceps, Glúteos", null, "2-1-2", 7, "Las zancadas traseras son más estables para principiantes.", 3),
                    new ExerciseSeed("Prensa de piernas", "En máquina de prensa, bajar el peso controladamente hasta flexionar las rodillas a 90°, empujando con los talones para subir.", 3, 12, null, 90, "Cuádriceps, Glúteos", "Pesas", "3-1-3", 7, null, 4),
                    new ExerciseSeed("Elevación de gemelos", "De pie en un escalón o superficie elevada, subir sobre las puntas de los pies y bajar controladamente. Usar peso adicional si es posible.", 4, 15, null, 60, "Gemelos", null, "2-1-2", null, "Mantener el movimiento controlado, sin rebotar.", 5),
                ]),
            // 5. Pecho
            new RoutineSeed(
                Name: "Pecho",
                Description: "Rutina de fuerza para pectorales y músculos accesorios. Combinación de ejercicios con barra y mancuernas.",
                Category: RoutineCategory.Fuerza,
                Difficulty: RoutineDifficulty.Moderado,
                EstimatedMinutes: 40,
                Equipment: "Barra/Manos",
                TargetMuscles: "Pectoral, Deltoides anterior, Tríceps",
                WarmupNotes: "5 minutos de movilidad de hombros + press ligero para activar.",
                CooldownNotes: "5 minutos de estiramientos de pecho y hombros.",
                Exercises:
                [
                    new ExerciseSeed("Press banca", "Acostado en banco plano, bajar la barra al pecho y empujar hacia arriba. Los pies firmes en el suelo y los omóplatos retraídos.", 4, 10, null, 90, "Pectoral, Tríceps, Deltoides anterior", "Barra", "3-1-3", 7, null, 1),
                    new ExerciseSeed("Flexiones", "En posición de plancha, bajar el pecho hacia el suelo flexionando los codos y empujando hacia arriba. Mantener el cuerpo recto como una tabla.", 4, 15, null, 60, "Pectoral, Tríceps, Core", null, "2-1-2", 7, "Ajusta la inclinación (manos en silla) para facilitar.", 2),
                    new ExerciseSeed("Aperturas con mancuerna", "Acostado en banco, bajar los brazos abiertos hasta sentir el estiramiento del pecho, subir contrayendo los pectorales. Codos ligeramente flexionados.", 3, 12, null, 60, "Pectoral", "Mancuernas", "3-1-3", 7, null, 3),
                    new ExerciseSeed("Fondos", "En paralelas o banco, bajar el cuerpo flexionando los codos hasta 90° y empujar hacia arriba. Inclinarse hacia adelante para enfocar el pecho.", 3, 10, null, 90, "Pectoral, Tríceps", null, "2-1-2", 8, "Si no puedes hacer 10, reduce el rango de movimiento.", 4),
                    new ExerciseSeed("Press inclinado", "Acostado en banco inclinado a 30-45°, bajar la barra o mancuernas al pecho superior y empujar hacia arriba.", 3, 10, null, 90, "Pectoral superior, Deltoides anterior", "Barra/Mancuernas", "3-1-3", 7, null, 5),
                ]),
            // 6. Espalda
            new RoutineSeed(
                Name: "Espalda",
                Description: "Rutina de fuerza para dorsales, trapecios y erectores espinales. Fundamental para postura y prevención de lesiones.",
                Category: RoutineCategory.Fuerza,
                Difficulty: RoutineDifficulty.Moderado,
                EstimatedMinutes: 40,
                Equipment: "Barra/Manos",
                TargetMuscles: "Dorsal, Trapecio, Bíceps, Erector espinal",
                WarmupNotes: "5 minutos de remo ligero o band pull-aparts para activar.",
                CooldownNotes: "5 minutos de estiramientos de espalda y bíceps.",
                Exercises:
                [
                    new ExerciseSeed("Dominadas", "Colgado de una barra, subir el pecho hasta la barra contrayendo los dorsales. Bajar controladamente. Usar banda asistida si es necesario.", 4, 8, null, 90, "Dorsal, Bíceps", "Barra", "2-1-3", 8, null, 1),
                    new ExerciseSeed("Remo con barra", "Inclinado hacia adelante con la espalda recta, remar la barra hacia el abdomen bajo. Contraer los omóplatos al final del movimiento.", 4, 10, null, 90, "Dorsal, Trapecio", "Barra", "3-1-3", 7, null, 2),
                    new ExerciseSeed("Jalón al pecho", "En polea alta, jalar la barra hacia el pecho superior contrayendo los dorsales. Inclinarse ligeramente hacia atrás al final.", 3, 12, null, 60, "Dorsal, Bíceps", "Polea", "2-1-3", 7, null, 3),
                    new ExerciseSeed("Peso muerto", "Desde el suelo, levantar la barra con espalda recta hasta la cadera. Mantener la barra pegada al cuerpo. Fundamental para toda la cadena posterior.", 3, 8, null, 120, "Erector espinal, Glúteos, Isquiotibiales", "Barra", "3-1-3", 8, "Prioriza la técnica sobre el peso. Esquema lumbar neutro.", 4),
                    new ExerciseSeed("Remo con mancuerna", "Apoyando una mano y una rodilla en banco, remar la mancuerna hacia la cadera con la otra mano. Contraer el dorsal al final.", 3, 10, null, 60, "Dorsal", "Mancuerna", "2-1-3", 7, null, 5),
                ]),
            // 7. Bíceps y Tríceps
            new RoutineSeed(
                Name: "Bíceps y Tríceps",
                Description: "Rutina de fuerza para brazos. Combinación de ejercicios de bíceps y tríceps para desarrollo armónico.",
                Category: RoutineCategory.Fuerza,
                Difficulty: RoutineDifficulty.Moderado,
                EstimatedMinutes: 30,
                Equipment: "Mancuernas/Polea",
                TargetMuscles: "Bíceps, Tríceps",
                WarmupNotes: "3 minutos de movilidad de codos y hombros.",
                CooldownNotes: "3 minutos de estiramientos de bíceps y tríceps.",
                Exercises:
                [
                    new ExerciseSeed("Curl bíceps", "De pie con mancuernas, flexionar los codos elevando el peso hacia los hombros. Mantener los codos pegados al cuerpo. No balancear el torso.", 4, 12, null, 60, "Bíceps", "Mancuernas", "2-1-2", 7, null, 1),
                    new ExerciseSeed("Curl martillo", "Similar al curl normal pero con las palmas mirándose entre sí. Trabaja el braquiorradial y la porción externa del bíceps.", 3, 12, null, 60, "Bíceps, Braquiorradial", "Mancuernas", "2-1-2", 7, null, 2),
                    new ExerciseSeed("Press francés", "Acostado en banco, bajar la barra o mancuernas hacia la frente flexionando los codos, extendiendo hacia arriba. Mantener los codos fijos.", 4, 10, null, 60, "Tríceps", "Mancuernas/Barra", "2-1-3", 7, null, 3),
                    new ExerciseSeed("Extensión en polea", "De pie frente a polea alta, empujar la barra hacia abajo extendiendo los codos. Contraer los tríceps al final del movimiento.", 3, 12, null, 60, "Tríceps", "Polea", "2-1-3", 7, null, 4),
                    new ExerciseSeed("Flexión diamante", "En posición de plancha, manos juntas formando un diamante con los pulgares e índices. Bajar el pecho hacia las manos y empujar arriba.", 3, 12, null, 60, "Tríceps, Pectoral", null, "2-1-3", 8, "Si es demasiado difícil, manos más separadas.", 5),
                ]),
            // 8. Hombros
            new RoutineSeed(
                Name: "Hombros",
                Description: "Rutina de fuerza para deltoides y trapecios superiores. Clave para la estabilidad de la articulación del hombro.",
                Category: RoutineCategory.Fuerza,
                Difficulty: RoutineDifficulty.Moderado,
                EstimatedMinutes: 30,
                Equipment: "Mancuernas",
                TargetMuscles: "Deltoides, Trapecio",
                WarmupNotes: "3 minutos de rotaciones de hombro y band pull-aparts.",
                CooldownNotes: "3 minutos de estiramientos de hombros y trapecios.",
                Exercises:
                [
                    new ExerciseSeed("Press militar", "De pie o sentado, presionar mancuernas o barra desde los hombros hacia arriba hasta extensión completa. Controlar la bajada.", 4, 10, null, 90, "Deltoides, Tríceps", "Mancuernas/Barra", "2-1-3", 7, null, 1),
                    new ExerciseSeed("Elevación lateral", "De pie, elevar los brazos lateralmente hasta la altura de los hombros con codos ligeramente flexionados. Bajar controladamente.", 4, 12, null, 60, "Deltoides lateral", "Mancuernas", "2-1-2", 7, "No subas más alto de los hombros.", 2),
                    new ExerciseSeed("Elevación frontal", "De pie, elevar los brazos al frente hasta la altura de los hombros, alternando o simultáneo. Palmas mirando hacia abajo.", 3, 12, null, 60, "Deltoides anterior", "Mancuernas", "2-1-2", 7, null, 3),
                    new ExerciseSeed("Pájaro (rear delt fly)", "Inclinado hacia adelante, elevar los brazos lateralmente contrayendo los deltoides posteriores. Codos ligeramente flexionados.", 3, 12, null, 60, "Deltoides posterior", "Mancuernas", "2-1-2", 7, null, 4),
                    new ExerciseSeed("Encogimientos", "De pie con mancuernas, encoger los houldros hacia las orejas manteniendo un segundo arriba. Bajar controladamente.", 3, 15, null, 45, "Trapecio", "Mancuernas", "2-1-2", null, null, 5),
                ]),
            // 9. Glúteos
            new RoutineSeed(
                Name: "Glúteos",
                Description: "Rutina de fuerza enfocada en glúteos y cadera. Ideal para mejorar la postura y el rendimiento deportivo.",
                Category: RoutineCategory.Fuerza,
                Difficulty: RoutineDifficulty.Moderado,
                EstimatedMinutes: 30,
                Equipment: "Ninguno/Pesas",
                TargetMuscles: "Glúteo, Cuádriceps, Isquiotibiales",
                WarmupNotes: "3 minutos de puente de glúteos suave + movilidad de cadera.",
                CooldownNotes: "3 minutos de estiramientos de glúteos y piriforme.",
                Exercises:
                [
                    new ExerciseSeed("Hip thrust", "Espalda apoyada en un banco, elevar la cadera con el peso en la zona pélvica hasta extensión completa. Contraer los glúteos arriba.", 4, 12, null, 90, "Glúteo", "Banco/Pesa", "2-1-3", 7, null, 1),
                    new ExerciseSeed("Puente de glúteos", "Acostado boca arriba con rodillas flexionadas, elevar la cadera apretando los glúteos. Pausa de 2 segundos arriba.", 3, 15, null, 60, "Glúteo, Isquiotibiales", null, "2-2-2", null, null, 2),
                    new ExerciseSeed("Patada de glúteo", "En cuatro puntos, elevar una pierna hacia atrás con la rodilla flexionada 90°. Contraer el glúteo en la parte alta.", 3, 12, null, 45, "Glúteo", null, "2-1-2", null, "Mantener la cadera estable, sin girar.", 3),
                    new ExerciseSeed("Abducción de cadera", "Acostado de lado, elevar la pierna superior manteniendo la rodilla extendida. Controlar la bajada sin dejar caer la pierna.", 3, 15, null, 45, "Glúteo medio, TFL", null, "2-1-2", null, null, 4),
                    new ExerciseSeed("Sentadilla sumo", "Sentadilla con piñas separadas y puntas hacia afuera. Bajar hasta que los muslos estén paralelos. Enfoca glúteos e internos.", 3, 12, null, 90, "Glúteo, Cuádriceps, Aductores", null, "3-1-3", 7, null, 5),
                ]),
            // 10. Core y Abdominales
            new RoutineSeed(
                Name: "Core y Abdominales",
                Description: "Rutina de fuerza para el core: planchas, crunches y ejercicios de estabilidad. Fundamental para la prevención de dolor lumbar.",
                Category: RoutineCategory.Fuerza,
                Difficulty: RoutineDifficulty.Facil,
                EstimatedMinutes: 20,
                Equipment: "Colchoneta",
                TargetMuscles: "Core, Recto abdominal, Oblicuos",
                WarmupNotes: "2 minutos de respiración diafragmática en el suelo.",
                CooldownNotes: "3 minutos de estiramientos de espalda baja y cadera.",
                Exercises:
                [
                    new ExerciseSeed("Plancha", "Mantener posición de plancha con el cuerpo recto desde la cabeza hasta los talones. Activar el core y no hundir la cadera.", 4, null, 45, 30, "Core, Recto abdominal", "Colchoneta", null, null, "Respirar normalmente, no aguantar la respiración.", 1),
                    new ExerciseSeed("Crunch", "Acostado boca arriba con rodillas flexionadas, elevar los hombros del suelo contrayendo el abdomen. No tirar del cuello.", 3, 20, null, 30, "Recto abdominal", "Colchoneta", "2-1-2", null, null, 2),
                    new ExerciseSeed("Russian twist", "Sentado con las piernas ligeramente elevadas, girar el torso de lado a lado tocando el suelo con las manos. Controlar el movimiento.", 3, 20, null, 30, "Oblicuos, Core", "Colchoneta", "1-1-1", null, "Mantener la espalda recta, no redondear los hombros.", 3),
                    new ExerciseSeed("Elevación de piernas", "Acostado boca arriba, elevar las piernas rectas hasta 90° y bajar controladamente sin tocar el suelo.", 3, 15, null, 30, "Recto abdominal inferior", "Colchoneta", "2-1-3", null, "Si hay dolor lumbar, dobla las rodillas parcialmente.", 4),
                    new ExerciseSeed("Plancha lateral", "En posición de plancha lateral, mantener el cuerpo recto apoyado en un antebrazo. Activar oblicuos y no hundir la cadera.", 3, null, 30, 30, "Oblicuos, Core", "Colchoneta", null, null, "Cambiar de lado después de cada serie.", 5),
                ]),
            // 11. Full Body
            new RoutineSeed(
                Name: "Full Body",
                Description: "Rutina mixta que trabaja todos los grupos musculares principales en una sola sesión. Ideal para mantener el fitness general.",
                Category: RoutineCategory.Mixta,
                Difficulty: RoutineDifficulty.Moderado,
                EstimatedMinutes: 45,
                Equipment: "Mancuernas",
                TargetMuscles: "Full body",
                WarmupNotes: "5 minutos de trote suave + movilidad articular general.",
                CooldownNotes: "5 minutos de estiramientos generales de todo el cuerpo.",
                Exercises:
                [
                    new ExerciseSeed("Sentadilla", "Sentadilla con mancuernas a los hombros. Bajar hasta paralelo, manteniendo la espalda recta y las rodillas alineadas.", 4, 12, null, 90, "Cuádriceps, Glúteos", "Mancuernas", "3-1-3", 7, null, 1),
                    new ExerciseSeed("Press banca", "Acostado en banco, presionar mancuernas desde el pecho hacia arriba. Controlar la bajada hasta sentir el estiramiento del pecho.", 4, 10, null, 90, "Pectoral, Tríceps", "Mancuernas/Banco", "3-1-3", 7, null, 2),
                    new ExerciseSeed("Remo", "Inclinado hacia adelante, remar mancuernas hacia la cadera contrayendo los dorsales. Mantener la espalda recta.", 4, 10, null, 60, "Dorsal, Bíceps", "Mancuernas", "2-1-3", 7, null, 3),
                    new ExerciseSeed("Plancha", "Mantener posición de plancha con el cuerpo recto. Activar el core y respirar normalmente.", 3, null, 45, 45, "Core", null, null, null, null, 4),
                    new ExerciseSeed("Burpees", "Burpee completo: sentadilla, plancha, sentadilla, salto. Ritmo constante sin pausas excesivas.", 3, 12, null, 60, "Full body, Cardiovascular", null, "1-0-1", 8, null, 5),
                    new ExerciseSeed("Zancadas", "Dar pasos alternos flexionando ambas rodillas a 90°. Mantener el torso erguido y el core activado.", 3, 12, null, 60, "Cuádriceps, Glúteos", null, "2-1-2", 7, null, 6),
                ]),
            // 12. Flexibilidad y Movilidad
            new RoutineSeed(
                Name: "Flexibilidad y Movilidad",
                Description: "Rutina de estiramientos y movilidad articular. Ideal para días de recuperación o como complemento a rutinas de fuerza.",
                Category: RoutineCategory.Flexibilidad,
                Difficulty: RoutineDifficulty.Facil,
                EstimatedMinutes: 15,
                Equipment: "Ninguno",
                TargetMuscles: "Isquiotibiales, Pectoral, Cadera, Cuádriceps, Hombros",
                WarmupNotes: "2 minutos de marcha suave para elevar la temperatura corporal.",
                CooldownNotes: "La rutina en sí es la fase de enfriamiento.",
                Exercises:
                [
                    new ExerciseSeed("Estiramiento de isquiotibiales", "Acostado boca arriba, elevar una pierna estirada y sujetarla detrás del muslo. Mantener 30 segundos sin rebotes.", 3, null, 30, 15, "Isquiotibiales", null, null, null, "No rebotes. Sentir el estiramiento sin dolor.", 1),
                    new ExerciseSeed("Apertura de pecho", "De pie en una esquina, colocar los antebrazos en la pared y girar el torso hacia atrás. Abrir el pecho y mantener.", 3, null, 30, 15, "Pectoral, Deltoides anterior", null, null, null, null, 2),
                    new ExerciseSeed("Rotación de cadera", "Acostado boca arriba con rodillas flexionadas, dejar caer ambas rodillas de lado alternadamente. Mantener los hombros pegados al suelo.", 3, null, 30, 15, "Cadera, Oblicuos", null, null, null, "Mantener los hombros en el suelo todo el tiempo.", 3),
                    new ExerciseSeed("Estiramiento de cuádriceps", "De pie, llevar un talón al glúteo sujetando el pie con la mano. Mantener las rodillas juntas y el torso erguido.", 3, null, 30, 15, "Cuádriceps", null, null, null, null, 4),
                    new ExerciseSeed("Movilidad de hombros", "De pie, girar los brazos en círculos amplios primero hacia adelante y luego hacia atrás. Aumentar el rango gradualmente.", 3, null, 30, 15, "Deltoides, Trapecio", null, null, null, null, 5),
                ]),
        ];
    }

    // -----------------------------------------------------------------------
    // Descriptores semilla
    // -----------------------------------------------------------------------

    /// <summary>Descriptor de una rutina de ejercicio completa con sus ejercicios.</summary>
    private sealed record RoutineSeed(
        string Name,
        string Description,
        RoutineCategory Category,
        RoutineDifficulty Difficulty,
        int? EstimatedMinutes,
        string? Equipment,
        string? TargetMuscles,
        string? WarmupNotes,
        string? CooldownNotes,
        IReadOnlyList<ExerciseSeed> Exercises);

    /// <summary>Descriptor de un ejercicio individual dentro de una rutina.</summary>
    private sealed record ExerciseSeed(
        string Name,
        string Description,
        int? Sets,
        int? Repetitions,
        int? DurationSecs,
        int? RestSeconds,
        string? TargetMuscle,
        string? Equipment,
        string? Tempo,
        int? Rpe,
        string? Tips,
        int SortOrder);
}
