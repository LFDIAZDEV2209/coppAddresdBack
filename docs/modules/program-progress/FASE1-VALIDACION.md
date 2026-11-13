# Validación Fase 1 — organización de Bienestar

Rama `org/f1-coppAddresdBack`, base `a9fd51f`. Sin integrar `origin/dev`.

## 1.1 — Confirmación del pisado

La hipótesis 5 de `DIAGNOSTICO.md` §7 pasa a hecho: ambas activaciones públicas
(por fecha y por semana perfecta) reemplazan la preedición de una semana bloqueada.
La escritura real del ERP guarda un jueves de 77 puntos; al activar queda en 10.
Dos pruebas PostgreSQL pasan afirmando este defecto, con migraciones en BD efímeras.
Se habilita la corrección 1.2.

Validación: `dotnet build -c Release` sin errores; integración `ActivacionPor`: 2/2.
Debug bloqueado por servicios locales en ejecución; Release evita detenerlos.

## 1.2 — Conservación al activar

Las dos regresiones fallaron con 1.1 (2 fallos) y pasan tras conservar snapshots
no vacíos en ambas activaciones. Las semanas vacías mantienen la copia de plantilla.
La combinación por origen y actualización del resto de días corresponde a 1.5.
Validación: build Release y pruebas de activación PostgreSQL.

## 1.3 — Plantillas por semana (esquema)

Migración aditiva `AddWeeklyTemplateWeekNumber`: columna nullable, índice único
por expresión COALESCE y rango positivo. Nombres físicos: `weekday`, `task_code`.
Índices concurrentes; rollback comentado y protegido contra pérdida de específicas.
PostgreSQL: genérica + específica coexisten, duplicadas en ambos ámbitos fallan
con 23505; reversa con específicas aborta; Up/Down/Up preservan 10.001 filas.
Volumen local leído: 84 filas. No se migró la BD local; prueba en BD efímera.
Validación: build Release, integración 1/1; ProgramProgress 566 pasan, 97 omitidas.

## 1.4 — Snapshot tolerante

Un único serializador compartido preserva `origin`, `rest`, `edited_by`,
`edited_at` y `backfill_version` (entero). Campos ausentes/nulos o metadatos inválidos
se leen como null; contenido antiguo conserva sus referencias/puntos/orden.
Las copias nuevas nacen `program`; las preediciones ERP nacen `individual`, con
actor y fecha. No se cambia aún la resolución de descansos (tarea 1.9).
Validación: ida/vuelta antiguo/nuevo, descanso true/false, auditoría y edición selectiva;
build Release correcto; ProgramProgress 570 pasan, 97 omitidas; activación PostgreSQL 2/2.

## 1.5 — Combinación al activar

Ambas activaciones combinan plantilla vigente (específica > genérica) con ítems
individuales. Origen antiguo/desconocido se conserva como individual por prudencia.
Las semanas ya activas/completadas no se rematerializan al completar una anterior.
La semana inicial y los respaldos de lectura también seleccionan por semana.
PostgreSQL: jueves individual de 77 sobrevive, lunes program de 15 pasa a 30,
martes específico gana con 40, miércoles ignora la específica de otra semana;
la semana anterior conserva sus 10 puntos. Cubiertas las dos activaciones.
Validación: build Release; integración 4/4; ProgramProgress 570 pasan, 97 omitidas.

## 1.6 — Clasificación simulada

Script y operación documentados en `scripts/README-week-snapshot-origin.md`.
Simulación local en conexión de solo lectura: 11.788 semanas; 3.326 excluidas,
8.462 futuras vacías; 0 ítems, 0 conflictos, 0 escrituras. No se aplicó a BD local.
PostgreSQL efímero: simulación, clasificación por versión/específica, prudencia
ante ambigüedad, idempotencia y reversa exacta/protegida contra edición posterior.
Validación: build Release; integración 1/1; ProgramProgress 570 pasan, 97 omitidas.

## 1.7 — API de tareas por semana

GET/PUT `templates/{id}/weekday-tasks?weekNumber=N`. Omitido: ámbito genérico
compatible con clientes anteriores. GET semanal combina específica > genérica;
PUT reemplaza solo filas de la semana solicitada, sin tocar las demás ni snapshots.
La respuesta añade `weekNumber` (null identifica respaldo genérico).
Semana fuera de rango: 422; plantilla de 12 semanas, semana 12/día 7: 422
`PROGRAM_DAY_OUT_OF_RANGE`; GET y materialización omiten ese día 84.
El CRUD anterior de metadatos/genéricas conserva las específicas.
Validación: PostgreSQL lectura/escritura aislada, persistencia entre semanas,
rechazo sin pérdida del día 84, día 83 aceptado, rangos y editor anterior: 1/1.
Build Release correcto; ProgramProgress 570 pasan, 97 omitidas.

## Contraste final de ProgramRepository con PostgreSQL

Al activar COP_TEST_DB_CONNECTION, la clase ProgramRepositoryTests ejecuta 63
casos: 54 pasan y 9 fallan. Repetidos en worktree limpio de a9fd51f: mismos
54/9 y exactamente los mismos nueve nombres (sin casos nuevos fallidos).
Los fallos de base corresponden a tres paridades de racha, hito 14, recentVitals,
tres limpiezas de DAY_BONUS con FK RESTRICT y una aserción antigua que presupone
que la última migración sea AddStructuredEmotionalAssessment. No se modificaron
estas pruebas ni reglas de XP fuera del alcance.

Casos reproducidos en base y rama:
- `BonusDayBonus_BaseXpNula_Fallback50`.
- `BonusDayBonus_Multiplicador2_MaxUsaBaseYGrantReflejaMultiplicador`.
- `BonusDayBonus_ReglaBase75_SnapshotYCompletacionSiguenLaRegla`.
- `GetSnapshot_RecentVitals_PobladoTrasVitals`.
- `Racha_Hito14_OtorgaStreak14UnaVezYNoActivaMultiplicador`.
- `Racha_Parity_DobleQuiebre_ParidadEnCadaPaso`.
- `Racha_Parity_JobZeroeado_SinRescate_ReiniciaEn1`.
- `Racha_Parity_MismoDia_Idempotente_SinDeriva`.
- `TareaEmocional_MigracionReversible_ConservaRegistrosAnteriores`.

## 1.8 — Medios externos y filtros

Migración aditiva `AddMediaSourceAndTags`; actuales `file`/`[]`, enlace nullable,
GIN concurrente, checks y Down protegido/comentado. PostgreSQL Up/Down/Up con
10.000 filas y EXPLAIN ANALYZE del GIN aprobados. No se migró la BD local.
GET media filtra origen/etiquetas en SQL antes de conteo/paginación; se probó la
combinación con tipo/categoría/estado, páginas distintas y etiquetas múltiples.
URLs HTTPS de YouTube/Vimeo verificadas por proveedor; contrato externo, portada
y duración opcionales. Crear/actualizar/publicar/eliminar externos no requiere blob.
Validación específica: 128 unitarios de medios y 2 PostgreSQL, sin fallos.

## Validación conjunta al cerrar 1.1–1.8

- `dotnet build -c Release`: correcto; Debug estaba ocupado por servicios existentes.
- Unitarios del proyecto: 2.393 aprobados, 105 omitidos sin conexión de pruebas.
- Integración PostgreSQL de `NutritionPlanWorkflowTests` (incluye toda la Fase 1):
  **59/59**, sin omisiones, cada caso en BD efímera con migraciones reales.
- ProgramRepository PostgreSQL: 54/63; los mismos 9 fallan en base `a9fd51f`.
  Contraste exacto de nombres arriba; ninguna modificación fuera de alcance.
- `git diff --check`: correcto. Sin push, pull de dev, rebase ni despliegue.
- La integración coordinada debe conservar el ORDER BY de `392439a`; no se tocó.
- Nota de Luis sobre pacientes/plantillas afectados para 1.12 registrada en
  `docs/modules/media/README.md`; todavía no se implementa 1.12.

## 1.7b — Referencias clínicas conservadas al editar podcasts

`WeeklyDayTemplateDto` añade `routineId` y `nutritionPlanId` (nullable), tanto en
GET como en la respuesta de PUT. PUT semanal reemplaza las clases enviadas en
su ámbito; conserva las clases omitidas. Para una fila enviada, una referencia
clínica null/omitida conserva la previa efectiva (específica > genérica); una
referencia no nula la cambia. El editor de podcasts no desasigna contenido clínico.
La retirada explícita de referencias requiere un caso de uso específico; no se
interpreta la ausencia de un campo como una decisión clínica de desasignación.

PostgreSQL: guardar solo podcasts conserva rutina/plan específicos; reenviar
las misiones sin campos clínicos conserva las referencias; una semana nueva
hereda genéricas y otras semanas permanecen intactas. GET devuelve ambas refs.
Pruebas de 1.7 y regresión 1.7b: 2/2; build Release y módulo ProgramProgress.
