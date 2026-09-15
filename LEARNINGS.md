# LEARNINGS — memoria del backend (CoppAddresd.Backend)

Entradas breves y verificadas. Transversal en `../LEARNINGS.md`.
Protocolo en `../.agents/rules/01-memoria.md`.

(Sin entradas aún — anexar con formato `## YYYY-MM-DD — título`.)

## 2026-09-11 — Snapshot de semana horneado: editar plantilla no toca semanas activas

`program_weeks.tasks_snapshot` se hornea al activar (`BuildSnapshotFromTemplateAsync`).
Editar `weekly_day_templates` (o subir `Version`) solo afecta FUTURAS activaciones.
Para dar contenido a semanas ya activas: `PUT content/week` / `content/range`
o re-inscribir. Verificado con paciente 32534534 (semanas 1-2 parchadas por SQL
solo como prueba; flujo real = endpoints). Default con contenido lo pone
`DevProgramContentSeeder` (solo NULL, nunca pisa curado).

## 2026-09-14 — slnx roto: compilar por proyecto, no la solución

`dotnet build CoppAddresd.slnx` falla (MSB3202: faltan
`tests/CoppAddresd.Gateway.IntegrationTests` y `tests/CoppAddresd.Gateway.UnitTests`,
referenciados pero no existen en disco). Workaround: `dotnet build <proyecto>`
por servicio (auth/community/gateway/telemedicine/api compilan en 0 errores).
Para correr todo con cell físico: NO usar `scripts/dev-up.sh` si Postgres de raíz
ya corre (conflicto de container_name, ver raíz); arrancar manual con
`ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build --project <p>` y exponer
front con `yarn dev -H 0.0.0.0` + `NEXT_PUBLIC_GATEWAY_URL=http://<IP-LAN>:5080`.
Gateway ya bindea `0.0.0.0:5080` vía `appsettings.Development.json` (Urls) y su CORS
incluye la IP LAN; `dotnet run` pisa `ASPNETCORE_URLS` con launchSettings en los
demás servicios (localhost, correcto tras el gateway).

## 2026-09-14 — Api NO auto-migra: tras merge aplicar EF manual

POST /api/v1/patients devolvía 500 `errorMissingColumn`: el código iba
adelante de la BD (migraciones `RenameProgramMilestoneSendsToProgramControls`,
`AddEmployeeErpAccessVersion` sin aplicar; solo Auth auto-migra al arrancar).
Fix: instalar `dotnet-ef` (global 10.0.12) y
`dotnet ef database update --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api`.
Regla: tras cada `merge dev`, correr ese update antes de probar.

## 2026-09-14 — Login con CC falla si hay patient_profiles duplicados huérfanos

`PatientLookupService` resuelve documento con `ORDER BY created_at LIMIT 1`:
si existe una fila vieja sin `user_id` (aprovisionada por OTP/id-lookup),
el login con CC devuelve "Credenciales inválidas" aunque el paciente real
esté vinculado. Además `user_id` es UNIQUE: no se puede vincular ambas.
Fix: borrar la huérfana (verificar 0 dependientes: enrollments, mediciones,
alergias, diagnósticos, medicamentos, planes) y el login con CC funciona.

## 2026-09-14 — Program Controls fase 2 exige ControlsEnabled=true en dev

Sin `Program:Controls:ControlsEnabled=true` en `appsettings.Development.json`
(el Example lo trae en false), el control jamás avanza de Sent: ni
Sent→Responded (hook de chat), ni Completed por upload, ni follow-ups/cierres
del job. Además `Templates` vacío ⇒ todo force queda Skipped (idempotente:
hay que resetear con DELETE /list tras configurar plantillas).
Regla: al probar controles, verificar el flag + plantillas en
GET /program-controls/templates antes de forzar.

## 2026-09-14 — Contexto de examen al thread vía SystemMessage interno

El upload (`POST /lab-exams`) analizaba pero el thread del ai-service nunca
veía el documento (narrate es stateless, resumen solo local) ⇒ follow-ups
"no veo el documento". Fix: `UploadLabExamCommandHandler` inyecta best-effort
un SystemMessage compacto (métricas + resumen) con
`ProactiveMessageAsync(..., role: "system")` — invisible en historial
(`_visible_role`), presente para el LLM. El endpoint
`/internal/agents/proactive-message` acepta `role: bot|system` (default bot,
sin cambios para push/controles).
