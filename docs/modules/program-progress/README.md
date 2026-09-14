# Programa del Paciente — Gamificación y Progreso (ANTARES / COPP-ADRESD)

Documento de contexto para el módulo de **Progreso del Programa** del paciente: el recorrido gamificado de 83 semanas que acompaña a los pacientes de obesidad, diabetes y riesgo cardiovascular de la aseguradora. Aquí encontrarás **qué hace, por qué lo hace y qué reglas lo rigen** desde la perspectiva del producto y del negocio. Los detalles técnicos (modelo de datos, endpoints, migraciones, tareas) viven en [`PLAN.md`](./PLAN.md), [`SPEC.md`](./SPEC.md) y [`TASKS.md`](./TASKS.md).

---

## 1. Contexto y propósito

### 1.1 Qué es el programa del paciente

El programa ANTARES (sub-marca COPP-ADRESD) es un protocolo clínico de **83 semanas** que estructura el día a día de un paciente con obesidad, diabetes o riesgo cardiovascular. Cada semana tiene una plantilla de misiones (tareas) que el paciente cumple desde su app móvil (`antares-paciente`) y que un equipo clínico (médicos, nutricionistas, endocrinólogos, preparadores) supervisa desde el ERP. Las misiones diarias incluyen escuchar un podcast educativo, registrar signos vitales, seguir el plan nutricional, hacer ejercicio, tomar el nutracéutico ADRED y completar un check-in emocional.

Lo que el paciente ve como "tarea" está conectado con un programa backend (`coppAddresdBack`) que registra cada cumplimiento, calcula puntos y racha, y produce dos tipos de indicadores: **gamificación** (XP, nivel, racha, multiplicador) e **indicadores clínicos reales** (Índice de Salud e Índice de Transformación). El ERP y la app móvil hoy conviven: la app sigue funcionando con mocks mientras el backend se conecta en fases.

### 1.2 Por qué gamificación, en términos humanos

La adherencia a planes crónicos cae sin refuerzo emocional. Las apps de idiomas (Duolingo) demostraron que rachas, XP, niveles y pequeños hitos producen hábito. Adaptamos esa intuición al contexto clínico: el paciente necesita ver su esfuerzo recompensado en el corto plazo, sin que el sistema lo castigue cuando su cuerpo — no su voluntad — no responde igual de bien una semana.

### 1.3 Reglas de oro (en palabras simples)

| #   | Regla                                                                  | En la práctica                                                                                                                                                           |
| --- | ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 1   | Los puntos premian **acción**, no apertura de la app                   | Solo se gana XP por tareas registradas: podcast escuchado, plan nutricional cumplido, ejercicio hecho, etc. Abrir la app no suma puntos.                                 |
| 2   | Un resultado clínico adverso **nunca** castiga                         | Si el peso sube o la glucosa empeora, el paciente no pierde XP ni rompe su racha. La gamificación reconoce el esfuerzo; el indicador clínico mide otra cosa.             |
| 3   | La gamificación es el **vehículo**, el resultado clínico es el destino | XP y nivel motivan a volver cada día; los Índices de Salud y Transformación son lo que importa clínicamente. La UI los etiqueta distinto y nunca mezcla uno con otro.    |
| 4   | **Nada de castigo**                                                    | No hay "vidas", no hay XP negativa, no hay copy tipo "perdiste salud". Los congelamientos de racha son tokens **positivos**: se ganan por días perfectos, no se pierden. |
| 5   | Las mejorías grandes requieren validación profesional                  | Si un indicador clínico mejora de forma significativa, la XP correspondiente queda en revisión hasta que un clínico la apruebe. No se otorgan "premios inventados".      |

### 1.4 Qué problema de negocio resuelve

Para la aseguradora y las clínicas, el programa ataca tres problemas a la vez:

- **Retención**: un paciente enganchado vuelve a la app y a sus controles.
- **Engagement**: el paciente registra datos diarios (alimentación, vitales, ánimo) sin fricción clínica.
- **Adherencia**: el equipo clínico ve adherencia real, no autorreportada, y puede intervenir a tiempo.

---

### 1.5 Modelo de Entidades y Jerarquía Semántica (Modelo ERP)

El módulo de **Progreso del Programa** está estructurado bajo un **modelo ERP relacional y semántico** que divide claramente la **Definición Maestra / Catálogo** (plantilla reutilizable) de la **Inscripción y Ejecución del Paciente** (runtime en ejecución).

```
                      +----------------------------------+
                      |         ProgramTemplate          |
                      |   (Plantilla de 83 semanas)      |
                      +----------------------------------+
                                        | 1:N
                                        v
                      +----------------------------------+
                      |        WeeklyDayTemplate         |
                      |   (Tareas programadas por día)   |
                      +----------------------------------+
                                        | (Instanciación / Copia)
                                        v
+-----------------------------------------------------------------------------------+
| INSCRIPCIÓN Y EJECUCIÓN DEL PACIENTE (RUNTIME)                                    |
|                                                                                   |
|  +--------------------------------+                                               |
|  |       PatientProfile           |                                               |
|  +--------------------------------+                                               |
|                  | 1:1 / 1:N                                                      |
|                  v                                                                |
|  +--------------------------------+                                               |
|  |       ProgramEnrollment        | <--- (Agregado Raíz del Programa del Paciente)|
|  +--------------------------------+                                               |
|                  | 1:N                                                            |
|                  v                                                                |
|  +--------------------------------+                                               |
|  |          ProgramWeek           | <--- (Semana 1..83 concreta con jsonb snapshot)|
|  +--------------------------------+                                               |
|                  | 1:N                                                            |
|                  v                                                                |
|  +--------------------------------+                                               |
|  |          DailyCheckIn          | <--- (Día local del paciente: resumen y ánimo)|
|  +--------------------------------+                                               |
|                  | 1:N                                                            |
|                  v                                                                |
|  +--------------------------------+                                               |
|  |         TaskCompletion         | <--- (Misión/Tarea diaria completada)         |
|  +--------------------------------+                                               |
|                  | 1:1 (Vinculación opcional en runtime)                          |
|                  +----------------------+--------------------+--------------------+
|                  |                      |                    |                    |
|                  v                      v                    v                    v
|        +-------------------+  +-------------------+  +---------------+  +------------------+
|        |   NutritionPlan   |  |  ExerciseRoutine  |  |   MediaItem   |  |   VitalSign /    |
|        | (Plan Nutricional)|  | (Rutina Ejercicio)|  |   (Podcast)   |  | Product / Ánimo  |
|        +-------------------+  +-------------------+  +---------------+  +------------------+
+-----------------------------------------------------------------------------------+
```

#### Jerarquía Operativa Principal (Programa → Semanas → Días → Misiones)

```
[Programa] ProgramEnrollment (Agregado Raíz)
   └── (1:N) [Semana] ProgramWeek (Semanas 1 a 83)
          └── (1:N) [Día] DailyCheckIn (Días locales 1 a 7 de la semana)
                 └── (1:N) [Misión] TaskCompletion (Misiones diarias completadas)
                        └── (1:1 opcional) Contenido resuelto (Plan Nutricional / Rutina / Podcast / Vitales)
```

1. **Programa (`ProgramEnrollment`)**
   - Es el **Agregado Raíz** que representa la inscripción activa del paciente a un protocolo clínico (ej. 83 semanas).
   - Posee el balance acumulado de XP, nivel, zona horaria IANA del paciente y estado general (`Active`, `Paused`, `Completed`, `Withdrawn`).
   - Contiene **muchas Semanas (`ProgramWeek`)** (relación `1:N`).

2. **Semana (`ProgramWeek`)**
   - Representa **una semana concreta** (de la 1 a la 83) dentro del ciclo del paciente, delimitada de lunes (`WeekStartDateLocal`) a domingo (`WeekEndDateLocal`).
   - Posee un `TasksSnapshot` (`jsonb`) que congela la definición de misiones de esa semana al iniciarla, garantizando que futuras ediciones en la plantilla no alteren semanas en curso.
   - Contiene **7 Días (`DailyCheckIn`)** (relación `1:N`).

3. **Día (`DailyCheckIn`)**
   - Representa **un día específico** en la zona horaria local del paciente (`LocalDate`).
   - Consolida el desempeño diario: estado del día (`IsPerfectDay`), bonus de día perfecto (`BonusAwarded`), total de puntos del día, nivel de ánimo (`MoodScore`) y barreras reportadas.
   - Contiene **varias Misiones / Tareas Completadas (`TaskCompletion`)** (relación `1:N`).

4. **Misión / Tarea Completada (`TaskCompletion`)**
   - Representa la **ejecución y cumplimiento de 1 misión programada** para el día (`TaskCode`: `podcast`, `vitals`, `nut`, `ejercicio`, `nutraceutico`, `emocional`).
   - Mantiene la clave de idempotencia del cliente (`ClientRequestId`) para evitar duplicación de puntos por reintentos de red.
   - Se vincula semánticamente en runtime con la entidad de contenido correspondiente (relación opcional `1:1` según el tipo de tarea):
     - `nut` ➔ `NutritionPlan` / `NutritionPlanDay`
     - `ejercicio` ➔ `ExerciseRoutine`
     - `podcast` ➔ `MediaItem`
     - `vitals` ➔ `VitalSignsBatch` (`VitalSign`)
     - `nutraceutico` ➔ `Product`
     - `emocional` ➔ `EmotionalRecord`

---

#### Entidades Satélites y Sub-sistemas del Dominio

Además de la jerarquía operativa principal, el módulo organiza 4 sub-sistemas satélites vinculados al `ProgramEnrollment`:

| Sub-sistema              | Entidades                                                | Relación y Función Semántica                                                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ------------------------ | -------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Gamificación & Racha** | `StreakState`, `StreakFreeze`, `XpLedgerEntry`, `XpRule` | • **`StreakState`** (`1:1` con `ProgramEnrollment`): Mantiene la racha actual de días consecutivos, días récord, multiplicador x2 activo y su vencimiento.<br>• **`StreakFreeze`** (`1:N` con `ProgramEnrollment`): Inventario y auditoría de tokens de congelamiento de racha (máx. 3).<br>• **`XpLedgerEntry`** (`1:N` con `ProgramEnrollment`): Libro mayor contable de otorgamientos de XP.<br>• **`XpRule`** (Catálogo global): Reglas de otorgamiento y topes anti-fraude. |
| **Hábitos Granulares**   | `HabitTemplate`, `HabitCheck`                            | • **`HabitTemplate`** (`1:N` con `ProgramTemplate`): Plantillas de hábitos configurables.<br>• **`HabitCheck`** (`1:N` con `ProgramEnrollment`): Registros diarios detallados de hábitos nutricionales (desayuno, almuerzo, merienda, cena, agua).                                                                                                                                                                                                                               |
| **Indicadores Clínicos** | `ClinicalBaseline`, `HealthScore`, `TransformationScore` | • **`ClinicalBaseline`** (`1:N` por paciente): Línea base de valores clínicos fijada obligatoriamente por profesionales.<br>• **`HealthScore`** (`1:N` con `ProgramEnrollment`): Historial de cálculos del Índice de Salud (0–100 en 5 dimensiones).<br>• **`TransformationScore`** (`1:N` con `ProgramEnrollment`): Historial de cálculos del Índice de Transformación (cambio % vs. línea base).                                                                               |
| **Adaptación & Salud**   | `Weakness`, `Intervention`, `AdaptationRecommendation`   | • **`Weakness`** / **`Intervention`** (`1:N` con `ProgramEnrollment`): Debilidades detectadas por el motor determinista e intervenciones asociadas.<br>• **`AdaptationRecommendation`** (`1:N` con `ProgramEnrollment`): Recomendaciones clínicas de ajuste de programa o dificultad.                                                                                                                                                                                            |

---

## 2. Reglas de negocio completas

### 2.1 Misiones diarias

La plantilla semanal del paciente (`default-83w`) programa **6 tipos de tarea** sobre 7 días. Los puntos base están alineados con la app móvil actual:

| Tipo           | Significado                          | Puntos base (plantilla) |
| -------------- | ------------------------------------ | ----------------------- |
| `podcast`      | Escuchar podcast educativo           | 80                      |
| `vitals`       | Medir signos vitales                 | 120                     |
| `nut`          | Cumplir plan nutricional del día     | 150                     |
| `ejercicio`    | Hacer ejercicio del día              | 150                     |
| `nutraceutico` | Tomar nutracéutico ADRED             | 80                      |
| `emocional`    | Check-in emocional (ánimo, barreras) | 120                     |

**Día perfecto**: cuando el paciente completa todas las tareas programadas de su día, recibe un **bonus de +50 XP** (base por defecto). El día perfecto es la unidad de cadencia para la concesión de congelamientos de racha (ver §2.4). El monto real del bonus sale del catálogo (`DAY_BONUS.base_xp`, §2.2; 50 de fallback si la regla no existe/está inactiva): el snapshot expone `dailyBonusAmount` (base real, sin multiplicador) y `todayPointsMax` lo usa (`base del día + dailyBonusAmount`); el otorgamiento efectivo puede diferir por el multiplicador del paciente (§2.5).

### 2.2 Catálogo de reglas de XP (`xp_rules`)

Los puntos y topes ya no son código: viven en una tabla catálogo (`app.xp_rules`) que un administrador puede ajustar sin migración. Cada regla tiene:

- **Código** único (ej. `TASK_PODCAST`, `DAY_BONUS`, `STREAK_11`, `CLINICAL_IMPROVE`, `NUTRITION_MEAL_COMPLETE`).
- **Categoría**: adherencia (tareas y bonus de día), racha (hitos), clínica (mejoría real), nutrición (logs granulares).
- **Puntos base** (puede ser NULL = "tomar los puntos de la plantilla").
- **Multiplicador** propio de la regla.
- **Topes por día y por semana** (anti-fraude): cuántas veces puede otorgarse por paciente local.

| Categoría  | Reglas sembradas                                                         | Puntos base                | Topes                                               |
| ---------- | ------------------------------------------------------------------------ | -------------------------- | --------------------------------------------------- |
| Adherencia | `TASK_PODCAST/VITALS/NUT/EJERCICIO/nutraceutico/EMOCIONAL` + `DAY_BONUS` | NULL (tareas) / 50 (bonus) | 1/día · 7/semana (tareas); 1/día · 7/semana (bonus) |
| Racha      | `STREAK_7/11/22/50`                                                      | 100 / 200 / 500 / 1500     | 1/día · 1/semana                                    |
| Clínica    | `CLINICAL_IMPROVE/STABLE/WEEKLY_ALL_UP/SIGNIFICANT`                      | 50 / 20 / 150 / 100        | Una vez por período (sin topes diarios)             |
| Nutrición  | `NUTRITION_MEAL_COMPLETE/HYDRATION/WEEK_85/RECOVERY`                     | 10 / 5 / 75 / 50           | 4/día (comida) · 1/día (agua)                       |

**Edición prospectiva**: editar una regla cambia los otorgamientos futuros; nunca reescribe el historial del libro mayor (`xp_ledger`). Si una regla está inactiva o vencida, los puntos vuelven al comportamiento por defecto (plantilla) sin topes — la fila del libro mayor queda marcada con `rule_code = NULL` para que se sepa que esa XP no pasó por el catálogo.

### 2.3 Niveles

El XP acumulado define un nivel motivacional (texto y barra de progreso). Los rangos actuales vienen del mock móvil (`antares-paciente/src/data/program.ts`) y se ajustarán cuando la integración móvil los pida:

| Nivel          | Rango de XP        |
| -------------- | ------------------ |
| Explorador     | 0 – 499            |
| Iniciado       | 500 – 1 499        |
| Constante      | 1 500 – 2 999      |
| Disciplinado   | 3 000 – 4 999      |
| Transformación | 5 000 – 7 999      |
| Bienestar      | 8 000 – 11 999     |
| Maestro        | 12 000 en adelante |

Cruzar el umbral de **5 000 XP** (nivel Transformación) dispara una recomendación clínica de **incremento de dificultad** que requiere aprobación del profesional antes de aplicarse.

### 2.4 Racha, congelamientos, rescate y multiplicador

La racha cuenta **días consecutivos que cumplen el umbral del programa**. Configuración actual (plantilla `default-83w`):

- **Umbral de mantenimiento**: `streak_min_tasks = 1` — basta una tarea por día local para mantener la racha (configurable por plantilla).
- **Tareas esenciales** (`essential_task_codes`): `nut`, `ejercicio`, `nutraceutico` — son las que cuentan para "rescatar" un día perdido con un congelamiento.
- **Concesión de congelamientos**: 1 cada 7 días perfectos consecutivos, tope de 3 en inventario. Un salto por rescate (p. ej. 5→7) puede saltarse múltiplos intermedios (6→8 no otorga el 7) — aceptado.
- **Rescate con congelamiento**: si el paciente no llega al umbral un día y tiene un congelamiento, **solo lo consume** si ese día cumplió al menos una tarea esencial. Sin tarea esencial → la racha se reinicia y el congelamiento queda en inventario (no se consume). La ventana evaluada es **solo ayer** (política v1: un hueco mayor rescata únicamente el último día perdido).
- **Modelo de corridas (enmienda 2026-09-07, paridad con el reconciliador)**: el reconciliador nocturno es la fuente de verdad de `(current, longest, lastActive)`. Día calificado = cumple el umbral **o** fue rescatado con congelamiento. El **día de vuelta tras un quiebre cuenta**: la racha **reinicia en 1** (nunca en 0). Con rescate: hueco de exactamente 1 día → `current = almacenado + 2` (el día rescatado extiende la corrida y hoy la continúa); hueco mayor → `current = 2` (corrida nueva). Un reinicio 5→1 no dispara hitos ni concesiones (`grewToday = current > almacenado`).
- **Racha efectiva en lectura (decay, 2026-09-14)**: los caminos de lectura (`GET /me/snapshot`, detalle/listado de inscripciones del ERP y KPIs del dashboard) reportan la racha **efectiva**: el escalar `current_streak` solo se considera válido si `last_active_date` es **hoy o ayer local** (la misma regla que `ComputeStreakRuns`). Días perdidos posteriores → `current = 0` **sin esperar** al job nocturno. `longest_streak`, congelamientos y multiplicador **no** se decaen; `nextMilestoneDays` se deriva de la racha efectiva.

**Hitos de racha y multiplicador x2**: al alcanzar ciertos días consecutivos, el paciente recibe XP del catálogo **una sola vez por inscripción** y, en los hitos 11/22/50, activa un multiplicador x2 temporal:

| Día | XP otorgada | Multiplicador x2 |
| --- | ----------- | ---------------- |
| 7   | +100        | —                |
| 11  | +200        | x2 por 24 h      |
| 22  | +500        | x2 por 48 h      |
| 50  | +1 500      | x2 por 72 h      |

El x2 **se sobrescribe** si el paciente alcanza un nuevo hito antes de que venza la ventana (se extiende desde ahora), y aplica a **toda** la XP mientras está vigente (tareas, bonus, hitos, clínica). Cuando vence, se resetea a 1.0 de forma perezosa en el próximo otorgamiento.

### 2.5 Bonus de día perfecto

Cuando el paciente completa todas las tareas programadas del día, recibe **+50 XP** automáticos. El otorgamiento es idempotente: si la última completación del día cierra el "perfect day", el bonus se otorga una sola vez. La XP nunca se descuenta si después se invalida un check-in.

### 2.6 Índice de Salud e Índice de Transformación

Son **indicadores clínicos reales** (no gamificación), calculados a partir de mediciones y adherencia. La UI los etiqueta como "Índice" para distinguirlos de XP/Nivel.

**Índice de Salud** es un 0–100 ponderado por 5 dimensiones. Pesos por defecto (configurables, suman 1.0000):

| Dimensión        | Peso | Lectura                                                               |
| ---------------- | ---- | --------------------------------------------------------------------- |
| Adherencia       | 0.30 | % de días perfectos / parciales / rescatados / perdidos en la ventana |
| Clínica          | 0.30 | Comparación de cada medición vs línea base clínica                    |
| Nutrición        | 0.20 | Adherencia al plan nutricional (logs por comida)                      |
| Psicología       | 0.10 | Promedio del ánimo (1–5) escalado a 0–100                             |
| Actividad física | 0.10 | % de días con `ejercicio` completado                                  |

Valores neutros cuando no hay datos: `clinical = 50`, `nutrition = 0`, `psychology = 60`, `exercise = 0`, `adherence = 0`.

**Índice de Transformación** es un 0–100 que promedia el cambio porcentual por indicador contra línea base (peso, IMC, glucosa, etc.). Genera un `trend` (sube / estable / baja) y un detalle por indicador con baseline, valor actual, delta, delta%, unidad y score. Si no hay indicadores, devuelve `0` y `detail = {}`.

**Cómo se calculan**: al pedirse (`GET /api/v1/program/scores`) se recalculan si están vencidos y se persisten como historial. **Las líneas base son autoría clínica obligatoria**: un paciente nunca puede fijarse su propia línea base (debe hacerlo un profesional con rol clínico).

### 2.7 XP clínica

Solo se otorga cuando un clínico ejecuta `POST /scores/calculate`. Por cada indicador del período:

| Situación                          | Acción                                                                            | XP                                       |
| ---------------------------------- | --------------------------------------------------------------------------------- | ---------------------------------------- |
| Mejora ≥ 5 % (umbral configurable) | **Revisión profesional pendiente** — la XP no cuenta hasta que un clínico apruebe | +100 al aprobar (`CLINICAL_SIGNIFICANT`) |
| Mejora 1 % – 5 %                   | Auto-otorgada                                                                     | +50 (`CLINICAL_IMPROVE`)                 |
| Estable (`                         | Δ%                                                                                | ` < 1 %)                                 | Auto-otorgada | +20 (`CLINICAL_STABLE`) |
| Desfavorable                       | **Nunca penaliza**                                                                | 0                                        |
| Todos los indicadores favorables   | Auto-otorgada al final del período                                                | +150 (`CLINICAL_WEEKLY_ALL_UP`)          |

Las pendientes **no cuentan** en los totales de XP hasta que el clínico las aprueba. La aprobación y el rechazo dejan rastro de auditoría.

### 2.8 Nutrición granular

El paciente registra comida por comida (`des`, `alm`, `mer`, `cen`) e hidratación (`agua`) desde la pantalla de Nutrición. Por cada registro:

| Registro                            | XP inmediata | Tope      |
| ----------------------------------- | ------------ | --------- |
| Comida (`NUTRITION_MEAL_COMPLETE`)  | +10          | 4 por día |
| Hidratación (`NUTRITION_HYDRATION`) | +5           | 1 por día |

Los registros duplicados del mismo día devuelven `409 HABIT_ALREADY_LOGGED` (sin doble XP) para las **comidas**. La **hidratación** (`agua`) es acumulable: repetirla el mismo día responde `200` con XP 0 y actualiza el total de ml acumulado del día (tarjeta de 8 vasos del móvil: cada tap envía el total, p. ej. 250 → 500 → 750, y el total solo sube); la XP de hidratación (+5) se otorga una sola vez por día, en el primer registro. **Adicional, no sustituto**: la tarea diaria `nut` del programa sigue dando sus 150 puntos de plantilla; los registros granulares premian el detalle. El doble premio es visible y se puede ajustar bajando `base_xp` o subiendo topes en `xp_rules` sin migrar.

Al calcularse el Índice de Salud del período:

| Regla semanal        | Condición                      | XP  |
| -------------------- | ------------------------------ | --- |
| `NUTRITION_WEEK_85`  | Adherencia nutricional ≥ 85 %  | +75 |
| `NUTRITION_RECOVERY` | +20 puntos vs período anterior | +50 |

Si no hay registros en el período → adherencia 0, sin premio, sin castigo.

### 2.9 Anti-fraude

| Mecanismo                        | Cómo se aplica                                                                                                                                                          |
| -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Idempotencia por DB**          | Único por `(enrollment_id, local_date, task_code)` en tareas y `(paciente, plantilla, fecha)` en nutrición. Un reintento del móvil con la misma clave nunca duplica XP. |
| **Idempotencia por libro mayor** | Único parcial `(source_ref_type, source_ref_id, reason)` en `xp_ledger` — un otorgamiento por regla y origen.                                                           |
| **Clave de cliente**             | `clientRequestId` del móvil se persiste para deduplicar reintentos de red. Reusar la misma clave con otra fecha → 409.                                                  |
| **Servidor como autoridad**      | Toda la XP se calcula en el backend; la UI solo refleja.                                                                                                                |
| **Hora local del paciente**      | Streak y ventanas usan la zona horaria del paciente (`program_enrollments.timezone`). No se rompe la racha al cruzar zonas horarias.                                    |
| **Topes por catálogo**           | `max_per_day` y `max_per_week` por regla, aplicados dentro de la transacción de otorgamiento (no se pueden burlar con concurrencia).                                    |

### 2.10 Autorización

| Permiso                 | Quién lo usa                                                        | Para qué                                                                  |
| ----------------------- | ------------------------------------------------------------------- | ------------------------------------------------------------------------- |
| `Program.View`          | Paciente (sobre sí mismo) y clínico (sobre sus pacientes asignados) | Leer snapshot, calendario, sendero, scores, reglas XP                     |
| `Program.Edit`          | Clínico / admin                                                     | Editar plantillas, recalcular scores, editar catálogo `xp_rules`          |
| `Program.Enroll`        | Clínico / admin                                                     | Inscribir, pausar, reanudar, retirar pacientes                            |
| `Program.Adapt`         | Clínico / admin                                                     | Aprobar/rechazar recomendaciones y revisiones de XP clínica significativa |
| `Program.ForceComplete` | Clínico                                                             | Forzar completación con override de fingerprint                           |

**Anti-IDOR**: cualquier lectura cruzada entre pacientes devuelve `404`. Los clínicos ven solo pacientes asignados vía `app.patient_professionals`.

### 2.11 Liga del paciente (LEAGUE v1)

Comparación social **opt-in** entre pacientes del programa, con privacidad por diseño. Levanta el descope de SPEC "Social leagues / cross-patient comparisons" (ver enmienda en `SPEC.md` §Out of scope y fila de riesgo §1139).

**Contrato** (paciente autenticado, `patientId` siempre del JWT — anti-IDOR):

| Endpoint | Método | Descripción |
| -------- | ------ | ----------- |
| `/api/v1/program/me/league` | GET | Cohortes (estado o nacional) + ranking propio en 4 categorías |
| `/api/v1/program/me/league-preferences` | PUT | `{ nickname: string\|null, optIn: boolean }` → estado guardado (nickname `null` = limpia el almacenado) |

```jsonc
{
  "cohort": { "scope": "state" | "national", "stateCode": "CA" | null, "participants": 12, "computedAt": "2026-09-07T12:00:00Z" },
  "me": { "optedIn": true, "nickname": "Pantera" },
  "categories": {
    "racha": { "entries": [ { "position": 1, "display": "Pantera", "isMe": true, "value": 150 } ], "myRank": 1, "myValue": 150, "totalParticipants": 12 },
    "evo":   { "entries": [], "myRank": null, "myValue": null, "totalParticipants": 0 },
    "adh":   { "entries": [], "myRank": null, "myValue": null, "totalParticipants": 0 },
    "clin":  { "entries": [], "myRank": null, "myValue": null, "totalParticipants": 0 }
  }
}
```

**Categorías y fuentes de datos** (valores PERSISTIDOS únicamente — el endpoint nunca dispara recálculo de puntajes por participante):

| Categoría | Fuente |
| --------- | ------ |
| `racha` | `streak_states.current_streak` de la inscripción activa |
| `evo` | `transformation_scores.score` más reciente (por `calculated_at`) |
| `adh` | `health_scores.score_adherence` del período más reciente |
| `clin` | `health_scores.score_clinical` del período más reciente |

Un paciente sin valor en una categoría queda **excluido de esa categoría** (nunca 0). `entries` = top 10 por valor DESC (desempate `display` ASC); si el paciente está opt-in y fuera del top 10, se anexa su fila real con su posición exacta. `myRank`/`myValue` son null sin opt-in o sin valor.

**Privacidad por diseño**:

- **Opt-in default OFF** (`patient_profiles.league_opt_in`, default `false`) + **nickname obligatorio** para aparecer (3–32 caracteres, charset acotado). Las preferencias viven en `patient_profiles` (no en la inscripción) para sobrevivir a re-inscripciones.
- **Seudonimización server-side**: la respuesta NUNCA contiene id, nombre real o ciudad de otro participante. `display` = nickname o código anónimo estable (2 letras + 4 dígitos derivados de SHA-256 del id). Colisiones de nickname permitidas (display únicamente). Logs sin PHI (solo alcance, tamaño del cohorte y `computedAt`, espejo de la regla §13.6).
- **k-rule**: un estado con **<10 opt-in** con inscripción activa cae al cohorte **nacional**; `scope` refleja cuál se usó. Sin estado resoluble (sin ciudad/estado) → nacional. El cohorte incluye solo opt-in con inscripción ACTIVA (pausadas/retiradas quedan fuera).
- **Nickname obligatorio al optar**: `optIn = true` exige nickname válido (FluentValidation → 400, incluidos los tokens reservados de staff/marca); `optIn = false` deja el nickname opcional (`null` SIEMPRE limpia el valor almacenado).

**Cache** (ver `docs/modules/cache/README.md`): el cohorte se cachea por alcance con **TTL 5 min** — clave `league:{stateCode|ALL}:v1` (prefijo del servicio: `erp:league:CA:v1`), compartida por todos los pacientes del mismo alcance. El payload cacheado NO lleva datos por-petición (sin `isMe`, sin bloque `me`): el bloque `me` y los flags `isMe` se resuelven por request contra el `patientId` del JWT. Al cambiar las preferencias del paciente se invalidan SUS claves (`league:{estado}` + `league:ALL`) — la revocación del opt-in es inmediata (privacidad); los cambios de otros pacientes se absorben por el TTL (sin invalidaciones fan-out). Fallo de caché → fail-open a PostgreSQL. El cohorte nacional puede existir bajo varias claves (`league:ALL:v1` + `league:{ST}:v1` de estados chicos) con `computedAt` independientes — inofensivo, cada clave expira sola.

### 2.12 Historial de puntajes (scores-history)

Serie semanal para la pestaña **Evolución** del móvil: los Índices de Salud y de Transformación **persistidos**, semana por semana, en orden ascendente. **Solo lectura**: nunca dispara recálculo ni al motor de puntajes (la frescura de la semana actual sigue siendo trabajo de `GET /scores`, §13.3).

**Contrato** (paciente autenticado, `patientId` siempre del JWT — anti-IDOR; solo `[Authorize]`, convención `me/*` — los JWT de paciente no llevan claims de permiso):

| Endpoint | Método | Descripción |
| -------- | ------ | ----------- |
| `/api/v1/program/me/scores-history?weeks=12` | GET | Serie ascendente de puntajes; `weeks` opcional (default 12, clamp 1..83) |

```jsonc
{
  "points": [
    { "weekNumber": 1, "periodStart": "2026-08-31", "periodEnd": "2026-09-06",
      "healthScore": 21, "healthPrevious": null, "transformationScore": 0 }
  ]
}
```

**Regla de alineamiento**: `weekNumber` sale de `transformation_scores.week_number`. Cada fila de `health_scores` se asigna a la semana de la inscripción cuyo rango `[WeekStartDateLocal..WeekEndDateLocal]` contiene su `period_end`; si esa semana ya tiene punto de transformación, la fila de salud se fusiona en él (período = rango de la semana del programa); si no, la fila de salud emite su propio punto (período = el persistido de la fila). Cuando hay varias filas de salud en la misma semana gana la de `period_end` más reciente. `healthScore`/`transformationScore` son null cuando esa tabla no tiene fila para la semana (nunca 0 inventado); `healthPrevious` es el `score_previous` persistido.

**Solo-persistido**: se emiten únicamente semanas con al menos una fila (nunca semanas vacías ni huecos). Sin inscripción activa → `404 NO_ACTIVE_ENROLLMENT`; sin perfil de paciente → 404 (anti-IDOR AC-11).

**Cache**: clave por paciente `scores-history:{patientId}:v1` (prefijo del servicio: `erp:`), TTL 5 min, fail-open (ver `docs/modules/cache/README.md`). El payload cacheado es la serie completa del paciente — nunca compartida entre pacientes (a diferencia del cohorte de la liga, aquí el scoping lo garantiza la propia clave). El recorte a las últimas N semanas se aplica por request, nunca se cachea por-petición. El dato solo cambia al calcularse una semana: el TTL corto absorbe el staleness sin invalidaciones.

**Historial por programa (enmienda 2026-09-07)**: las filas de puntajes son por paciente y se **purgaron** al re-inscribir (`EnrollAsync`, en la misma transacción) — la historia pertenece a la corrida en curso: las semanas viejas de una corrida anterior colisionarían con las nuevas (filas de semanas 3..N mapearían a semanas futuras con puntajes viejos). La re-inscripción invalida además el caché `scores-history:{patientId}:v1` post-commit. El fetch se acota a la ventana de la corrida actual (inicio de la semana 1 → fin de la semana actual local).

### 2.13 Historial de métricas clínicas (metrics-history)

Series REALES de métricas clínicas para la **Home** del móvil (IMC, HbA1c, % grasa...) reemplazando la historia fabricada del frontend. Todo sale de `app.clinical_measurements` (catálogo sembrado en `ClinicalMeasurementsSeeder`) con rangos de `app.measurement_reference_ranges`. Solo lectura: nunca dispara el motor de puntajes.

**Contrato** (paciente autenticado, `patientId` siempre del JWT — anti-IDOR; solo `[Authorize]`, convención `me/*`):

| Endpoint | Método | Descripción |
| -------- | ------ | ----------- |
| `/api/v1/program/me/metrics-history?codes=bmi,hba1c,body_fat&days=180` | GET | Series por fecha local; `codes` CSV (default `bmi,hba1c,body_fat`), `days` (default 180, clamp 7..365) |

```jsonc
{
  "heightCm": 168.0,
  "metrics": [
    { "code": "bmi", "unit": "kg_m2", "target": { "lo": 18.5, "hi": 24.9 }, "favorableDirection": "down",
      "points": [ { "date": "2026-06-01", "value": 27.6 }, { "date": "2026-09-07", "value": 26.4 } ] }
  ]
}
```

**Semántica**:

- **Whitelist**: códigos inexistentes en el catálogo → **400** (`METRICS_UNKNOWN`) con la lista COMPLETA de válidos (el catálogo activo completo está cacheado: la lista nunca queda vacía; el 400 se aplica en CADA request, también con caché caliente). Códigos duplicados → una sola entrada (case-insensitive); el `code` emitido es SIEMPRE el del catálogo (nunca el casing del request). Códigos válidos sin filas en la ventana → **omitidos** (requires-data).
- **Serie**: filas de `clinical_measurements` del paciente filtradas por código + ventana (últimos N días locales, convertida a UTC DST-aware), orden ASC por `ObservedAt`, **un punto por fecha local** (el más reciente del día gana; empate → mayor `Id`). **Unit-consistencia**: solo se conservan filas en la **unidad por defecto del catálogo** de la métrica (el peso en libras no se mezcla con kg); otras unidades se omiten (conversión = trabajo futuro).
- **`target`**: rango de referencia ACTIVO de mayor prioridad para la métrica (`lo`/`hi` null cuando no hay rango o el extremo es abierto); empate de prioridad → desempate determinista por `Id`.
- **`favorableDirection`**: `'down' | 'up' | null`. La **línea base clínica** del paciente (`clinical_baselines.favorable_direction`, autoría clínica — SPEC §13.1.2) manda. Sin línea base, se deriva **'down'** solo para el set bajo-es-mejor con rango presente: `bmi`, `hba1c`, `body_fat`, `glucose_fasting`. El resto → null (desconocible).
- **Fallback de IMC (única métrica computada, documentado)**: si `bmi` no tiene filas en la ventana pero existen filas de `weight` (en su unidad por defecto `kg` — el guard de unidades aplica también al fallback) y el perfil tiene `height_cm`, se computa `bmi = peso/(talla/100)²` por fecha. Ninguna otra métrica se fabrica.
- **`heightCm`**: `patient_profiles.height_cm` (columna huérfana del modelo EF — se lee por SQL directo; null si no está cargada).
- Sin inscripción activa → `404 NO_ACTIVE_ENROLLMENT` (verificado en CADA request, fuera del caché).

**Cache** (ver `docs/modules/cache/README.md`): el **contexto COMPLETO** del paciente se cachea por clave `metrics-history:{patientId}:v1` (prefijo del servicio: `erp:`), TTL 5 min, fail-open — la serie de TODAS las métricas activas del catálogo sobre la ventana MÁXIMA (365d), talla, targets y direcciones, **sin codes/days en la clave** (precedente scores-history). El recorte por-request (whitelist → 400, clamp de días, re-filtro de ventana, fallback de IMC) se aplica DESPUÉS del caché y **nunca se cachea**. Sin invalidación explícita: el TTL absorbe el lag de las completaciones de signos vitales (≤ 5 min) — mismo tradeoff que scores-history.

### 2.14 Controles por hitos del programa (UC-001 v2)

En los días 7/14/21/45/60/90 del programa (`program_enrollments.start_local_date`), el paciente recibe un **mensaje proactivo** (push FCM + inyección en el chat, costo LLM cero) que ahora además **abre un Control**: un ciclo de vida auditable en `app.program_controls` en el que el agente pregunta cómo se siente y —si el paciente responde— conduce una conversación guiada para que suba sus exámenes de laboratorio por el adjunto del chat (panel completo, la misma petición en todos los controles). El examen subido queda **asociado al control** (`exam_batch_id`, sin FK), base de futuras métricas de adherencia. El detalle funcional completo vive en `USE-CASES.md` (UC-001, monorepo).

**Ciclo de vida** (estados de fase 2 sobre la columna `status`; `Sent` ya no es terminal):

```
Sent → Responded → Completed | ClosedWithoutExam
Sent → FollowedUp → Missed
```

| Estado | Cuándo |
| --- | --- |
| `Responded` | El paciente escribió en el control abierto (cualquier mensaje; los hooks de chat `ProgramControlChatHooks` lo marcan) |
| `FollowedUp` | Silencio 48 h → **único** follow-up (plantilla `FollowupTemplate` con `{day}`, misma ventana 9–21 local del paciente) |
| `Completed` | Subió un examen de laboratorio con el control abierto (`UploadLabExamCommandHandler` asocia el batch, best-effort) |
| `ClosedWithoutExam` | Negativa explícita (`closed_reason='declined'`) o sin subida tras `NoUploadCloseDays` (7 días desde `Responded`, `no_upload_timeout`) |
| `Missed` | Sin respuesta tras el follow-up (`MissedAfterFollowupHours`, 48 h) |

**Temporizadores** (configurables en `Program:Controls`): `FollowupHours` (48 h, un solo follow-up), `MissedAfterFollowupHours` (48 h → `Missed`), `NoUploadCloseDays` (7 días → cierre sin examen). El job `ProgramControlJob` evalúa los vencidos en cada pasada (fase 2 solo con `ControlsEnabled`).

**Negativa explícita**: el agente **no insiste** — llama la tool `mark_control_declined` del ai-service, que emite la señal `control_signal="declined"` (campo sync o evento SSE `control_signal`); el backend la consume (`TryConsumeSignalAsync`) y cierra el control con `closed_reason='declined'`. La negativa ambigua/evasión **no cierra**: el control permanece abierto hasta el backstop de 7 días.

**Ruta de la señal** (frontend/móvil nunca habla directo con ai-service): chat del paciente → backend (.NET) → request de chat con `control_context` (`send_id`, `milestone_day`, `status`, `exam_pending`) → ai-service inyecta el guiado en el prompt del turno → ante negativa explícita, tool `mark_control_declined` → `control_signal="declined"` → backend cierra el control. Sin `control_context` el agente se comporta como UC-001 puro.

**Killswitch**: `Program:Controls → ControlsEnabled=false` (default) = comportamiento UC-001 puro (solo envío proactivo + estados de fase 1 `Pending/Sent/Failed/Skipped`; sin hooks de chat, sin follow-ups, sin cierres ni asociación de exámenes). Rollback en producción = apagar el flag, sin redeploy.

---

## 3. Cómo funciona el flujo en la práctica

1. **Inscripción** — Un clínico inscribe al paciente en el programa (`POST /program/enrollments`). Se crea el enrollment con la plantilla `default-83w`, la zona horaria del paciente y la fecha local de inicio.
2. **Semana activa** — El paciente abre la app y ve su semana. La pantalla principal muestra las tareas del día con sus puntos, el XP acumulado, el nivel, la racha actual, los congelamientos disponibles y el multiplicador vigente. Todo viene del snapshot (`GET /program/me/snapshot`).
3. **Cumplimiento de misiones** — Al completar una tarea, la app llama a `POST /program/tasks/complete` con `clientRequestId`. El backend registra la tarea, otorga XP (vía catálogo), actualiza el día, recalcula racha y, si aplica, otorga bonus de día perfecto o XP de hito. La respuesta devuelve el estado nuevo (XP, racha, multiplicador). La app actualiza la UI optimistamente y reconcilia.
4. **Premios de hitos** — Cuando la racha cruza 7, 11, 22 o 50 días consecutivos, el paciente recibe XP del catálogo y (en 11/22/50) un multiplicador x2 temporal que potencia toda su XP siguiente.
5. **Cálculo semanal de indicadores** — El clínico dispara `POST /program/scores/calculate` para el paciente. El backend recalcula los Índices de Salud y Transformación, otorga XP clínica (auto + revisiones pendientes para mejorías grandes) y otorga premios semanales de nutrición si aplica.
6. **Revisión profesional** — El clínico abre la cola de revisiones pendientes (`GET /program/xp-rules/clinical-pending`) y aprueba o rechaza cada una. Solo al aprobar, esa XP significativa entra al total del paciente.
7. **Recomendaciones de adaptación** — El motor de adaptación detecta patrones (2+ días perdidos, ánimo bajo sostenido, cruce de nivel) y crea recomendaciones. Cambios de rutina o nutrición se aplican automáticamente; cambios de dificultad o nivel requieren aprobación del clínico.
8. **Ajustes de contenido** — El clínico puede cambiar el plan nutricional, la rutina de ejercicio o el podcast asignado sin re-inscribir al paciente. El snapshot de la semana actual no cambia (las ediciones aplican a la semana siguiente).

### 3.9 Configuración de contenido desde el ERP

El clínico configura, desde el ERP, **qué plan de nutrición y qué rutina de ejercicio corre cada semana** del programa del paciente. La pantalla muestra una vista por semana sobre las asignaciones de Wellness existentes (`app.nutrition_plan_assignments` / `app.routine_assignments`):

1. **Leer** — `GET /api/v1/program/enrollments/{id}/content` devuelve el timeline completo: array de semanas con ventana de fechas (lunes–domingo, local al paciente) y el plan/rutina asignado (o `null` si no hay asignación).
2. **Escribir** — `PUT /api/v1/program/enrollments/{id}/content/week/{weekNumber}` con `{ nutritionPlanId, exerciseRoutineId }` reemplaza las asignaciones de Wellness para la ventana de esa semana. `null` en un campo desasigna esa dimensión. Re-PUT con el mismo o distinto ID reemplaza sin duplicar.
3. **Resolución en runtime** — El módulo programa resuelve el contenido por fecha cuando el paciente completa una tarea (SPEC §4.2/§4.3/§6.10): la tarea `nut` busca el `NutritionPlanAssignment` activo para la fecha, la tarea `ejercicio` busca el `RoutineAssignment` activo.
4. **Sin asignación** — Cuando no hay plan/rutina asignado para la semana, la tarea se registra con `content = null` y la UI muestra el badge **"Sin asignar"**. La experiencia del paciente (XP, racha, gamificación) **no depende del contenido** — la acción se registra igual.

Este flujo se complementa con la configuración de plantillas (§7.6) y las recomendaciones de adaptación (§7.7). El ERP nunca llama directamente al AI Service — todo el tráfico pasa por el backend (.NET) que actúa como proxy autenticado (regla de seguridad del monorepo).

---

## 4. Estado actual

### 4.1 Backend — completado en código (P1.5)

| Paso                                                 | Alcance                                                                                                                                                                           | Estado del código                   |
| ---------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------- |
| 1 — Núcleo P1                                        | Plantilla semanal, inscripción, completación de tareas, racha, congelamientos, bonus de día                                                                                       | Implementado                        |
| 2 — Índices de Salud y Transformación                | 4 tablas nuevas, calculadores, `GET /scores` y `POST /scores/calculate`                                                                                                           | Implementado                        |
| 3 — Catálogo `xp_rules`                              | Tabla de reglas, resolución con precedencia y topes, admin `GET/PUT /xp-rules`                                                                                                    | Implementado                        |
| 4 — XP clínica con validación profesional            | Revisiones pendientes, aprobación/rechazo, exclusión de pendientes del total                                                                                                      | Implementado                        |
| 5 — Multiplicador x2 por hito                        | Hitos 7/11/22/50, ventana 24/48/72 h, snapshot expone estado                                                                                                                      | Implementado                        |
| 6 — Umbral de racha configurable + tareas esenciales | Columnas en plantilla, rescate solo con esencial                                                                                                                                  | Implementado                        |
| 6 — Nutrición granular                               | `POST /nutrition/log`, premios semanales en `/scores/calculate`                                                                                                                   | Implementado                        |
| 7 — Configuración de contenido por semana (ERP)      | `GET/PUT /program/enrollments/{id}/content`, `GET/PUT .../content/week/{weekNumber}` — endpoints de configuración de plan nutricional y rutina por semana (SPEC §7.8, T-74..T-78) | Implementado (contrato documentado) |

### 4.2 Backend — pendiente

- **Migraciones**: todas las migraciones del módulo están **aplicadas en la BD de desarrollo** (historial `public.__EFMigrationsHistory` = 45, verificado 2026-08-28). Incluye `AddWeeklyDayTemplateContentLinks` (T-77: columnas `routine_id`/`nutrition_plan_id` en `app.weekly_day_templates` — el modelo las traía desde T-77 pero la migración faltaba y rompía el enroll con 42703). El seeder sembró 31 reglas XP, 5 habit templates y los pesos de scores.
- **Validación manual** de los criterios de aceptación P1.5 (AC-19 a AC-36): los batches B5-R, B5-C, B5-M, B5-T, B5-N y B5-WK corren pruebas manuales por convención del flujo actual, no automatizadas.
- **Pantallas ERP** (B7): gestión de plantillas, lista de inscripciones, cola de revisiones pendientes y de adaptaciones. Las pantallas de configuración de contenido por semana (SPEC §7.8) están implementadas. **Pendiente**: cola de adaptaciones (T-26).
- **Motor de adaptación** (P2): `ProgramAdaptationEngine` **implementado** (T-23) — motor de reglas determinista (Application/Services/ProgramProgress) evaluado tras cada `CompleteTask` en la misma transacción: 2+ días imperfectos en 7 días → `RoutineContentRefresh` auto-aplicada (Applied + audit `AdaptationApplied`); ánimo ≤ 2 por 7 días → variante suave; cruce de 5000 XP → `DifficultyChange` con aprobación (cola ERP, supersede de Pending previas). Dedupe temporal de 7 días por (kind, target). Tests: `AdaptationEngineTests` (10). La cola ERP de decisión (T-26) ya está en el frontend (`/program/adaptations`).
- **Rotación de podcasts** (P2): tabla `app.media_progressions` y resolutor por fecha (T-25). ⚠️ Requiere sincronizar la rama con `dev` antes de generar la migración (la BD local contiene migraciones de `dev` —health-tests, server catalogs— ausentes del código de la rama actual; `dotnet ef migrations add` generaría DROPs).
- **Scoping de clínico** (T-81), **transacción en SetWeekContent** (T-82) y **optimización de GET content** (T-83): ✔ **implementados (2026-09-02, B18)** — scoping `patient_professionals` vía `IProgramActorContext` en content/enrollments/week-detail (bypass Admin/OrganizationAdmin/ClinicAdmin + paciente propio), `SetWeekContent` en una transacción (`IWellnessRepository.ExecuteInTransactionAsync`) y GET content con una sola carga de asignaciones (`ResolveRangeAsync`). Contrato: SPEC §7.9.1.
- **Bitácora de actividad ERP** (2026-09-02): `GET /program/activity-log` (SPEC §7.9.2) lee `audit.activity_logs` filtrado a las tablas del módulo; la pantalla ERP "Bitácora de actividad" (`/program/activity-log`) reemplazó a la cola de validación de XP (los endpoints de decisión siguen vivos).
- **Reconciliación nocturna** (P3, B12): ✔ **implementada (2026-09-02)** — `ReconcileStreakJob` + hosted service nocturno (`Program:Reconciliation`) + disparo manual `POST /program/maintenance/reconcile-streaks` (SPEC §7.9.5).
- **Inscripción masiva y exporte CSV** (P3, B13/B14): ✔ **implementados (2026-09-02)** — `POST /program/enrollments/bulk` (tope 100/request, reporte por fila; dispatcher async diferido) y `GET /program/enrollments/export` (stream CSV, permiso `Program.Export`). UI ERP en Inscripciones (SPEC §7.9.3/§7.9.4).
- **i18n en la app** (P3): strings en inglés.
- **Trabajo futuro fuera del plan actual**: notificaciones gamificadas, detección de debilidades por IA, integración con intervenciones y telemedicina para XP de teleconsulta — estos ítems no están diseñados en `SPEC.md`/`PLAN.md` y requerirían una nueva fase.

### 4.3 App móvil (`antares-paciente`)

La app **sigue funcionando con mocks** (`src/data/program.ts`). El plan de integración es por capas (Slice 1 → 5): primero reemplazar las constantes mock por el snapshot del backend, luego conectar la completación de tareas, calendario, sendero y notificaciones de adaptación. Mientras el backend no esté conectado, la app conserva los datos simulados como fallback (la UX nunca se rompe).

### 4.4 ERP (`coppaddresd-front`)

Pantallas del módulo implementadas en `/program/*`: dashboard, hoy, adherencia, cofres, plantillas, inscripciones (con dialog de inscripción individual + **inscripción masiva** + **exporte CSV**), contenido por semana, reglas XP, scores (calcula Índice de Salud/Transformación por paciente), **bitácora de actividad** (reemplaza a la cola de revisiones clínicas de XP), debilidades, adaptaciones, intervenciones y perfil 360 del paciente (`/program/patients/[id]`).

---

## 5. Decisiones de producto abiertas

Estas preguntas siguen abiertas en el plan y se recomienda resolver antes de empezar el trabajo de UI:

| #   | Decisión                                        | Estado                        | Pregunta a resolver                                                                                                                                                                                                                                 |
| --- | ----------------------------------------------- | ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | Semántica de `nextMilestoneDays` en el snapshot | Diseñado pero ambiguo         | ¿Es la distancia al próximo **hito semanal de racha** (7/11/22/50) o al próximo **"cofre" de 50 días** del mock móvil (`NEXT_CHEST_DAYS = 50`)? No hay cofre real: el campo es derivado, pero falta confirmar la cadencia que prefiere el producto. |
| 2   | Valores por defecto de XP                       | Sembrados del mock actual     | Los puntos base del catálogo (`TASK_*`, `DAY_BONUS`, `STREAK_*`) vienen de los valores que la app móvil ya muestra. ¿Se mantienen como definitivos o se ajustan antes de exponerlos a clínicos reales?                                              |
| 3   | Mapeo de permisos para paciente                 | Diseñado pero no implementado | El JWT de paciente no incluye aún el permiso `Program.View`. ¿Se resuelve desde `ICurrentContext.UserId → app.patient_profiles.user_id` (un query por request, cacheado), o se agrega el permiso al token?                                          |
| 4   | Aprobación clínica para cambios de rutina       | Decisión por defecto          | ¿Toda edición de rutina requiere aprobación o solo cambios de dificultad/nivel? El plan sugiere auto-aplicar cambios de contenido dentro de la misma categoría.                                                                                     |
| 5   | Cadencia de concesión de congelamientos         | Decisión por defecto          | 1 cada 7 días perfectos (tope 3). ¿Se mantiene o se ajusta por cohorte?                                                                                                                                                                             |
| 6   | Avance de semana                                | Decisión por defecto          | Al cruzar medianoche local del domingo, ¿o al completar la primera tarea de la nueva semana? El plan asume medianoche local.                                                                                                                        |
| 7   | Línea base: ¿solo clínicos o también admin?     | Definido parcialmente         | El campo `set_by` requiere rol clínico. ¿El rol `Admin` cuenta o se restringe a `Physician / Nutritionist / Psychologist / ClinicalDirector`?                                                                                                       |

---

## 6. Referencias

| Documento                                             | Rol                                                                                             |
| ----------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| [`PLAN.md`](./PLAN.md)                                | Plan maestro del módulo: decisiones, fases, riesgos y verificación                              |
| [`SPEC.md`](./SPEC.md)                                | Especificación funcional y técnica: modelo de datos, contratos, reglas, criterios de aceptación |
| [`TASKS.md`](./TASKS.md)                              | Backlog ordenado por dependencias con IDs estables (T-01 a T-61)                                |
| `../README.md`                                        | Índice de módulos de `coppAddresdBack/docs/modules`                                             |
| `../../../antares-paciente/AGENTS.md`                 | Convenciones de la app móvil y mock que este módulo reemplaza                                   |
| `../../../antares-paciente/src/data/program.ts`       | Mock actual de tareas, niveles, XP y `NEXT_CHEST_DAYS`                                          |
| `../../../antares-paciente/src/pages/ProgramPage.tsx` | Pantalla móvil del programa (consume `GET /program/scores` cuando se conecte)                   |
| `../../../General-context.md`                         | Contexto general de la plataforma Coppaddresd (monorepo, módulos, alcance)                      |
| `coppAddresdBack/docs/agents/skills/`                 | Skills de estándares del backend (arquitectura, EF, query performance, etc.)                    |

**Inspiración clínica**: los hitos de racha, el multiplicador x2 y el rescate con tareas esenciales siguen la **referencia ADRED** del módulo (citada en `SPEC.md` §16 y §17). La regla "rescate exige tarea esencial" y los hitos 7/11/22/50 son una adaptación ADRED a la economía de congelamientos del programa.
