# Programa del Paciente — Gamificación y Progreso (ANTARES / COPP-ADRESD)

Documento de contexto para el módulo de **Progreso del Programa** del paciente: el recorrido gamificado de 83 semanas que acompaña a los pacientes de obesidad, diabetes y riesgo cardiovascular de la aseguradora. Aquí encontrarás **qué hace, por qué lo hace y qué reglas lo rigen** desde la perspectiva del producto y del negocio. Los detalles técnicos (modelo de datos, endpoints, migraciones, tareas) viven en [`PLAN.md`](./PLAN.md), [`SPEC.md`](./SPEC.md) y [`TASKS.md`](./TASKS.md).

---

## 1. Contexto y propósito

### 1.1 Qué es el programa del paciente

El programa ANTARES (sub-marca COPP-ADRESD) es un protocolo clínico de **83 semanas** que estructura el día a día de un paciente con obesidad, diabetes o riesgo cardiovascular. Cada semana tiene una plantilla de misiones (tareas) que el paciente cumple desde su app móvil (`antares-paciente`) y que un equipo clínico (médicos, nutricionistas, endocrinólogos, preparadores) supervisa desde el ERP. Las misiones diarias incluyen escuchar un podcast educativo, registrar signos vitales, seguir el plan nutricional, hacer ejercicio, tomar el nutribiótico ADRED y completar un check-in emocional.

Lo que el paciente ve como "tarea" está conectado con un programa backend (`coppAddresdBack`) que registra cada cumplimiento, calcula puntos y racha, y produce dos tipos de indicadores: **gamificación** (XP, nivel, racha, multiplicador) e **indicadores clínicos reales** (Índice de Salud e Índice de Transformación). El ERP y la app móvil hoy conviven: la app sigue funcionando con mocks mientras el backend se conecta en fases.

### 1.2 Por qué gamificación, en términos humanos

La adherencia a planes crónicos cae sin refuerzo emocional. Las apps de idiomas (Duolingo) demostraron que rachas, XP, niveles y pequeños hitos producen hábito. Adaptamos esa intuición al contexto clínico: el paciente necesita ver su esfuerzo recompensado en el corto plazo, sin que el sistema lo castigue cuando su cuerpo — no su voluntad — no responde igual de bien una semana.

### 1.3 Reglas de oro (en palabras simples)

| # | Regla | En la práctica |
|---|-------|----------------|
| 1 | Los puntos premian **acción**, no apertura de la app | Solo se gana XP por tareas registradas: podcast escuchado, plan nutricional cumplido, ejercicio hecho, etc. Abrir la app no suma puntos. |
| 2 | Un resultado clínico adverso **nunca** castiga | Si el peso sube o la glucosa empeora, el paciente no pierde XP ni rompe su racha. La gamificación reconoce el esfuerzo; el indicador clínico mide otra cosa. |
| 3 | La gamificación es el **vehículo**, el resultado clínico es el destino | XP y nivel motivan a volver cada día; los Índices de Salud y Transformación son lo que importa clínicamente. La UI los etiqueta distinto y nunca mezcla uno con otro. |
| 4 | **Nada de castigo** | No hay "vidas", no hay XP negativa, no hay copy tipo "perdiste salud". Los congelamientos de racha son tokens **positivos**: se ganan por días perfectos, no se pierden. |
| 5 | Las mejorías grandes requieren validación profesional | Si un indicador clínico mejora de forma significativa, la XP correspondiente queda en revisión hasta que un clínico la apruebe. No se otorgan "premios inventados". |

### 1.4 Qué problema de negocio resuelve

Para la aseguradora y las clínicas, el programa ataca tres problemas a la vez:

- **Retención**: un paciente enganchado vuelve a la app y a sus controles.
- **Engagement**: el paciente registra datos diarios (alimentación, vitales, ánimo) sin fricción clínica.
- **Adherencia**: el equipo clínico ve adherencia real, no autorreportada, y puede intervenir a tiempo.

---

## 2. Reglas de negocio completas

### 2.1 Misiones diarias

La plantilla semanal del paciente (`default-83w`) programa **6 tipos de tarea** sobre 7 días. Los puntos base están alineados con la app móvil actual:

| Tipo | Significado | Puntos base (plantilla) |
|------|-------------|-------------------------|
| `podcast` | Escuchar podcast educativo | 80 |
| `vitals` | Medir signos vitales | 120 |
| `nut` | Cumplir plan nutricional del día | 150 |
| `ejercicio` | Hacer ejercicio del día | 150 |
| `nutribiotico` | Tomar nutribiótico ADRED | 80 |
| `emocional` | Check-in emocional (ánimo, barreras) | 120 |

**Día perfecto**: cuando el paciente completa todas las tareas programadas de su día, recibe un **bonus de +50 XP**. El día perfecto es la unidad de cadencia para la concesión de congelamientos de racha (ver §2.4).

### 2.2 Catálogo de reglas de XP (`xp_rules`)

Los puntos y topes ya no son código: viven en una tabla catálogo (`app.xp_rules`) que un administrador puede ajustar sin migración. Cada regla tiene:

- **Código** único (ej. `TASK_PODCAST`, `DAY_BONUS`, `STREAK_11`, `CLINICAL_IMPROVE`, `NUTRITION_MEAL_COMPLETE`).
- **Categoría**: adherencia (tareas y bonus de día), racha (hitos), clínica (mejoría real), nutrición (logs granulares).
- **Puntos base** (puede ser NULL = "tomar los puntos de la plantilla").
- **Multiplicador** propio de la regla.
- **Topes por día y por semana** (anti-fraude): cuántas veces puede otorgarse por paciente local.

| Categoría | Reglas sembradas | Puntos base | Topes |
|-----------|------------------|-------------|-------|
| Adherencia | `TASK_PODCAST/VITALS/NUT/EJERCICIO/NUTRIBIOTICO/EMOCIONAL` + `DAY_BONUS` | NULL (tareas) / 50 (bonus) | 1/día · 7/semana (tareas); 1/día · 7/semana (bonus) |
| Racha | `STREAK_7/11/22/50` | 100 / 200 / 500 / 1500 | 1/día · 1/semana |
| Clínica | `CLINICAL_IMPROVE/STABLE/WEEKLY_ALL_UP/SIGNIFICANT` | 50 / 20 / 150 / 100 | Una vez por período (sin topes diarios) |
| Nutrición | `NUTRITION_MEAL_COMPLETE/HYDRATION/WEEK_85/RECOVERY` | 10 / 5 / 75 / 50 | 4/día (comida) · 1/día (agua) |

**Edición prospectiva**: editar una regla cambia los otorgamientos futuros; nunca reescribe el historial del libro mayor (`xp_ledger`). Si una regla está inactiva o vencida, los puntos vuelven al comportamiento por defecto (plantilla) sin topes — la fila del libro mayor queda marcada con `rule_code = NULL` para que se sepa que esa XP no pasó por el catálogo.

### 2.3 Niveles

El XP acumulado define un nivel motivacional (texto y barra de progreso). Los rangos actuales vienen del mock móvil (`antares-paciente/src/data/program.ts`) y se ajustarán cuando la integración móvil los pida:

| Nivel | Rango de XP |
|-------|-------------|
| Explorador | 0 – 499 |
| Iniciado | 500 – 1 499 |
| Constante | 1 500 – 2 999 |
| Disciplinado | 3 000 – 4 999 |
| Transformación | 5 000 – 7 999 |
| Bienestar | 8 000 – 11 999 |
| Maestro | 12 000 en adelante |

Cruzar el umbral de **5 000 XP** (nivel Transformación) dispara una recomendación clínica de **incremento de dificultad** que requiere aprobación del profesional antes de aplicarse.

### 2.4 Racha, congelamientos, rescate y multiplicador

La racha cuenta **días consecutivos que cumplen el umbral del programa**. Configuración actual (plantilla `default-83w`):

- **Umbral de mantenimiento**: `streak_min_tasks = 1` — basta una tarea por día local para mantener la racha (configurable por plantilla).
- **Tareas esenciales** (`essential_task_codes`): `nut`, `ejercicio`, `nutribiotico` — son las que cuentan para "rescatar" un día perdido con un congelamiento.
- **Concesión de congelamientos**: 1 cada 7 días perfectos consecutivos, tope de 3 en inventario.
- **Rescate con congelamiento**: si el paciente no llega al umbral un día y tiene un congelamiento, **solo lo consume** si ese día cumplió al menos una tarea esencial. Sin tarea esencial → la racha se rompe y el congelamiento queda en inventario (no se consume).

**Hitos de racha y multiplicador x2**: al alcanzar ciertos días consecutivos, el paciente recibe XP del catálogo **una sola vez por inscripción** y, en los hitos 11/22/50, activa un multiplicador x2 temporal:

| Día | XP otorgada | Multiplicador x2 |
|-----|-------------|------------------|
| 7 | +100 | — |
| 11 | +200 | x2 por 24 h |
| 22 | +500 | x2 por 48 h |
| 50 | +1 500 | x2 por 72 h |

El x2 **se sobrescribe** si el paciente alcanza un nuevo hito antes de que venza la ventana (se extiende desde ahora), y aplica a **toda** la XP mientras está vigente (tareas, bonus, hitos, clínica). Cuando vence, se resetea a 1.0 de forma perezosa en el próximo otorgamiento.

### 2.5 Bonus de día perfecto

Cuando el paciente completa todas las tareas programadas del día, recibe **+50 XP** automáticos. El otorgamiento es idempotente: si la última completación del día cierra el "perfect day", el bonus se otorga una sola vez. La XP nunca se descuenta si después se invalida un check-in.

### 2.6 Índice de Salud e Índice de Transformación

Son **indicadores clínicos reales** (no gamificación), calculados a partir de mediciones y adherencia. La UI los etiqueta como "Índice" para distinguirlos de XP/Nivel.

**Índice de Salud** es un 0–100 ponderado por 5 dimensiones. Pesos por defecto (configurables, suman 1.0000):

| Dimensión | Peso | Lectura |
|-----------|------|---------|
| Adherencia | 0.30 | % de días perfectos / parciales / rescatados / perdidos en la ventana |
| Clínica | 0.30 | Comparación de cada medición vs línea base clínica |
| Nutrición | 0.20 | Adherencia al plan nutricional (logs por comida) |
| Psicología | 0.10 | Promedio del ánimo (1–5) escalado a 0–100 |
| Actividad física | 0.10 | % de días con `ejercicio` completado |

Valores neutros cuando no hay datos: `clinical = 50`, `nutrition = 0`, `psychology = 60`, `exercise = 0`, `adherence = 0`.

**Índice de Transformación** es un 0–100 que promedia el cambio porcentual por indicador contra línea base (peso, IMC, glucosa, etc.). Genera un `trend` (sube / estable / baja) y un detalle por indicador con baseline, valor actual, delta, delta%, unidad y score. Si no hay indicadores, devuelve `0` y `detail = {}`.

**Cómo se calculan**: al pedirse (`GET /api/v1/program/scores`) se recalculan si están vencidos y se persisten como historial. **Las líneas base son autoría clínica obligatoria**: un paciente nunca puede fijarse su propia línea base (debe hacerlo un profesional con rol clínico).

### 2.7 XP clínica

Solo se otorga cuando un clínico ejecuta `POST /scores/calculate`. Por cada indicador del período:

| Situación | Acción | XP |
|-----------|--------|-----|
| Mejora ≥ 5 % (umbral configurable) | **Revisión profesional pendiente** — la XP no cuenta hasta que un clínico apruebe | +100 al aprobar (`CLINICAL_SIGNIFICANT`) |
| Mejora 1 % – 5 % | Auto-otorgada | +50 (`CLINICAL_IMPROVE`) |
| Estable (`|Δ%|` < 1 %) | Auto-otorgada | +20 (`CLINICAL_STABLE`) |
| Desfavorable | **Nunca penaliza** | 0 |
| Todos los indicadores favorables | Auto-otorgada al final del período | +150 (`CLINICAL_WEEKLY_ALL_UP`) |

Las pendientes **no cuentan** en los totales de XP hasta que el clínico las aprueba. La aprobación y el rechazo dejan rastro de auditoría.

### 2.8 Nutrición granular

El paciente registra comida por comida (`des`, `alm`, `mer`, `cen`) e hidratación (`agua`) desde la pantalla de Nutrición. Por cada registro:

| Registro | XP inmediata | Tope |
|----------|--------------|------|
| Comida (`NUTRITION_MEAL_COMPLETE`) | +10 | 4 por día |
| Hidratación (`NUTRITION_HYDRATION`) | +5 | 1 por día |

Los registros duplicados del mismo día devuelven `409 HABIT_ALREADY_LOGGED` (sin doble XP). **Adicional, no sustituto**: la tarea diaria `nut` del programa sigue dando sus 150 puntos de plantilla; los registros granulares premian el detalle. El doble premio es visible y se puede ajustar bajando `base_xp` o subiendo topes en `xp_rules` sin migrar.

Al calcularse el Índice de Salud del período:

| Regla semanal | Condición | XP |
|---------------|-----------|-----|
| `NUTRITION_WEEK_85` | Adherencia nutricional ≥ 85 % | +75 |
| `NUTRITION_RECOVERY` | +20 puntos vs período anterior | +50 |

Si no hay registros en el período → adherencia 0, sin premio, sin castigo.

### 2.9 Anti-fraude

| Mecanismo | Cómo se aplica |
|-----------|----------------|
| **Idempotencia por DB** | Único por `(enrollment_id, local_date, task_code)` en tareas y `(paciente, plantilla, fecha)` en nutrición. Un reintento del móvil con la misma clave nunca duplica XP. |
| **Idempotencia por libro mayor** | Único parcial `(source_ref_type, source_ref_id, reason)` en `xp_ledger` — un otorgamiento por regla y origen. |
| **Clave de cliente** | `clientRequestId` del móvil se persiste para deduplicar reintentos de red. Reusar la misma clave con otra fecha → 409. |
| **Servidor como autoridad** | Toda la XP se calcula en el backend; la UI solo refleja. |
| **Hora local del paciente** | Streak y ventanas usan la zona horaria del paciente (`program_enrollments.timezone`). No se rompe la racha al cruzar zonas horarias. |
| **Topes por catálogo** | `max_per_day` y `max_per_week` por regla, aplicados dentro de la transacción de otorgamiento (no se pueden burlar con concurrencia). |

### 2.10 Autorización

| Permiso | Quién lo usa | Para qué |
|---------|--------------|----------|
| `Program.View` | Paciente (sobre sí mismo) y clínico (sobre sus pacientes asignados) | Leer snapshot, calendario, sendero, scores, reglas XP |
| `Program.Edit` | Clínico / admin | Editar plantillas, recalcular scores, editar catálogo `xp_rules` |
| `Program.Enroll` | Clínico / admin | Inscribir, pausar, reanudar, retirar pacientes |
| `Program.Adapt` | Clínico / admin | Aprobar/rechazar recomendaciones y revisiones de XP clínica significativa |
| `Program.ForceComplete` | Clínico | Forzar completación con override de fingerprint |

**Anti-IDOR**: cualquier lectura cruzada entre pacientes devuelve `404`. Los clínicos ven solo pacientes asignados vía `app.patient_professionals`.

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

---

## 4. Estado actual

### 4.1 Backend — completado en código (P1.5)

| Paso | Alcance | Estado del código |
|------|---------|-------------------|
| 1 — Núcleo P1 | Plantilla semanal, inscripción, completación de tareas, racha, congelamientos, bonus de día | Implementado |
| 2 — Índices de Salud y Transformación | 4 tablas nuevas, calculadores, `GET /scores` y `POST /scores/calculate` | Implementado |
| 3 — Catálogo `xp_rules` | Tabla de reglas, resolución con precedencia y topes, admin `GET/PUT /xp-rules` | Implementado |
| 4 — XP clínica con validación profesional | Revisiones pendientes, aprobación/rechazo, exclusión de pendientes del total | Implementado |
| 5 — Multiplicador x2 por hito | Hitos 7/11/22/50, ventana 24/48/72 h, snapshot expone estado | Implementado |
| 6 — Umbral de racha configurable + tareas esenciales | Columnas en plantilla, rescate solo con esencial | Implementado |
| 6 — Nutrición granular | `POST /nutrition/log`, premios semanales en `/scores/calculate` | Implementado |

### 4.2 Backend — pendiente

- **Aplicar 6 migraciones pendientes** (`AddXpRulesCatalog`, `AddProgramProgressClinicalXp`, `AddProgramProgressMultiplier`, `AddProgramProgressStreakConfig`, `AddProgramProgressNutritionXp`, `AddMediaProgressions` — esta última de P2). Las migraciones están generadas pero **no aplicadas** en la base de datos de desarrollo.
- **Reiniciar procesos de dev** (API + Auth Service) para que los seeders poblen catálogo y configuración.
- **Validación manual** de los criterios de aceptación P1.5 (AC-19 a AC-36): los batches B5-R, B5-C, B5-M, B5-T y B5-N corren pruebas manuales por convención del flujo actual, no automatizadas.
- **Pantallas ERP** (B7): gestión de plantillas, lista de inscripciones, cola de revisiones pendientes y de adaptaciones.
- **Motor de adaptación** (P2): `ProgramAdaptationEngine` y el flujo de aprobación.
- **Rotación de podcasts** (P2): tabla `app.media_progressions` y resolutor por fecha.
- **Reconciliación nocturna** (P3): job que recorre `xp_ledger` y recalcula `streak_states` por inscripción.
- **Inscripción masiva y exporte CSV** (P3): `IJobDispatcher` para >100 pacientes, endpoint de exporte.
- **i18n en la app** (P3): strings en inglés.
- **Trabajo futuro fuera del plan actual**: notificaciones gamificadas, detección de debilidades por IA, integración con intervenciones y telemedicina para XP de teleconsulta — estos ítems no están diseñados en `SPEC.md`/`PLAN.md` y requerirían una nueva fase.

### 4.3 App móvil (`antares-paciente`)

La app **sigue funcionando con mocks** (`src/data/program.ts`). El plan de integración es por capas (Slice 1 → 5): primero reemplazar las constantes mock por el snapshot del backend, luego conectar la completación de tareas, calendario, sendero y notificaciones de adaptación. Mientras el backend no esté conectado, la app conserva los datos simulados como fallback (la UX nunca se rompe).

### 4.4 ERP (`coppaddresd-front`)

Sin pantallas del módulo todavía. Las pantallas de gestión de plantillas y revisiones pendientes están diseñadas pero no implementadas.

---

## 5. Decisiones de producto abiertas

Estas preguntas siguen abiertas en el plan y se recomienda resolver antes de empezar el trabajo de UI:

| # | Decisión | Estado | Pregunta a resolver |
|---|----------|--------|---------------------|
| 1 | Semántica de `nextMilestoneDays` en el snapshot | Diseñado pero ambiguo | ¿Es la distancia al próximo **hito semanal de racha** (7/11/22/50) o al próximo **"cofre" de 50 días** del mock móvil (`NEXT_CHEST_DAYS = 50`)? No hay cofre real: el campo es derivado, pero falta confirmar la cadencia que prefiere el producto. |
| 2 | Valores por defecto de XP | Sembrados del mock actual | Los puntos base del catálogo (`TASK_*`, `DAY_BONUS`, `STREAK_*`) vienen de los valores que la app móvil ya muestra. ¿Se mantienen como definitivos o se ajustan antes de exponerlos a clínicos reales? |
| 3 | Mapeo de permisos para paciente | Diseñado pero no implementado | El JWT de paciente no incluye aún el permiso `Program.View`. ¿Se resuelve desde `ICurrentContext.UserId → app.patient_profiles.user_id` (un query por request, cacheado), o se agrega el permiso al token? |
| 4 | Aprobación clínica para cambios de rutina | Decisión por defecto | ¿Toda edición de rutina requiere aprobación o solo cambios de dificultad/nivel? El plan sugiere auto-aplicar cambios de contenido dentro de la misma categoría. |
| 5 | Cadencia de concesión de congelamientos | Decisión por defecto | 1 cada 7 días perfectos (tope 3). ¿Se mantiene o se ajusta por cohorte? |
| 6 | Avance de semana | Decisión por defecto | Al cruzar medianoche local del domingo, ¿o al completar la primera tarea de la nueva semana? El plan asume medianoche local. |
| 7 | Línea base: ¿solo clínicos o también admin? | Definido parcialmente | El campo `set_by` requiere rol clínico. ¿El rol `Admin` cuenta o se restringe a `Physician / Nutritionist / Psychologist / ClinicalDirector`? |

---

## 6. Referencias

| Documento | Rol |
|-----------|-----|
| [`PLAN.md`](./PLAN.md) | Plan maestro del módulo: decisiones, fases, riesgos y verificación |
| [`SPEC.md`](./SPEC.md) | Especificación funcional y técnica: modelo de datos, contratos, reglas, criterios de aceptación |
| [`TASKS.md`](./TASKS.md) | Backlog ordenado por dependencias con IDs estables (T-01 a T-61) |
| `../README.md` | Índice de módulos de `coppAddresdBack/docs/modules` |
| `../../../antares-paciente/AGENTS.md` | Convenciones de la app móvil y mock que este módulo reemplaza |
| `../../../antares-paciente/src/data/program.ts` | Mock actual de tareas, niveles, XP y `NEXT_CHEST_DAYS` |
| `../../../antares-paciente/src/pages/ProgramPage.tsx` | Pantalla móvil del programa (consume `GET /program/scores` cuando se conecte) |
| `../../../General-context.md` | Contexto general de la plataforma Coppaddresd (monorepo, módulos, alcance) |
| `coppAddresdBack/docs/agents/skills/` | Skills de estándares del backend (arquitectura, EF, query performance, etc.) |

**Inspiración clínica**: los hitos de racha, el multiplicador x2 y el rescate con tareas esenciales siguen la **referencia ADRED** del módulo (citada en `SPEC.md` §16 y §17). La regla "rescate exige tarea esencial" y los hitos 7/11/22/50 son una adaptación ADRED a la economía de congelamientos del programa.
