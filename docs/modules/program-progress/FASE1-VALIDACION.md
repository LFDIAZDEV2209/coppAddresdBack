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
