# Informe de implementación — canal-whatsapp (2026-11-12)

Fases 1–6 implementadas en `feature/canal-whatsapp`, desde dev sincronizado.
Sin push ni operaciones contra Meta/AWS. Flags apagados por defecto. La corrección
de Luis está incorporada: bienestar, racha, podcast y evaluación inicial; sin citas
ni reconstrucción de plantillas eliminadas. El nutricionista usa la conversación IA.

## Commits por fase

| Fase | Commit | Archivos/áreas principales | Verificación al cierre |
|---|---|---|---|
| 1 | cd2a5a7 | Nuevo proyecto WhatsApp, Domain, DI/Program, contexto/factory, migración, tests de configuración y persistencia | Build sin errores; 3 tests; migración local e historial separado |
| 2 | 46b1569 | Contrato Application, sender, políticas y SenderTests | Build; 17 tests; endpoint/payload/headers, clasificación, máscara, fragmentación y ventana |
| 3 | 12395c3 | Webhook/controller/cola, registro MVC y WebhookTests | Build; 19 tests; handshake, firma, dedupe concurrente y ACK <5 s |
| 4 | 53e493d | Query Patients, adaptador, vínculos/conversaciones y migración | Build; 27 tests; perfil real de fixture, vínculo/hilo reutilizados y guía desconocidos |
| 5 | a4aea74 | Worker/processor, idioma, ChatCommand/ChatRequest/AiServiceClient, InboundFlowTests | Build; 39 tests y 27 tests existentes de chat; dos turnos/hilo, fallo seguro, replay y comandos |
| 6 | 0b7c4f0 | Reminder/Delivery, scheduler/worker/hooks, consulta de programa, hooks HealthTests/ProgramProgress, dos migraciones | Build; 60 tests; cuatro motivos reales, reintentos, cancelación y concurrencia |

El commit final de preparación agrega workflow/Dockerfile, scripts/runbook,
normalización UTF-8 sin BOM de migraciones y tres verificaciones adicionales:
round-trip Down/Up, claim concurrente inbound y comando SALIR sin acento.

## Evidencia final local

```text
dotnet build --no-restore -v quiet -clp:ErrorsOnly
Build succeeded. 0 Error(s).

dotnet test tests/CoppAddresd.WhatsApp.Tests --no-build -v quiet
Passed: 63, Failed: 0, Skipped: 0.

dotnet test tests/CoppAddresd.WhatsApp.Tests --no-build --filter Category=Unit
Passed: 38, Failed: 0, Skipped: 0.

dotnet test tests/CoppAddresd.WhatsApp.Tests --no-build --filter Category=Integration
Passed: 25, Failed: 0, Skipped: 0.

dotnet ef migrations list --project src/CoppAddresd.WhatsApp --context WhatsAppDbContext --no-build
20261112163426_InitialWhatsApp
20261112164437_PatientLinksAndConversations
20261112165457_AccompanimentReminders
20261112170205_ProgramAccompanimentReasons
```

Las cuatro migraciones se aplicaron en la BD local y su SQL fue revisado: solo
schema whatsapp, sin FK ni DDL hacia otros módulos. Down/Up probado en BD temporal.
Las pruebas crean/eliminan bases locales desechables y los tests de recordatorios
usan el modelo EF completo, repositorios y handlers reales de tareas/evaluaciones.
El AI Service está simulado en HTTP; Meta se sustituye por un sender controlado.

Pruebas nuevas y evidencia de cada tarea marcada:

| Tasks | Evidencia |
|---|---|
| 1.1–1.3 | Build, ConfigurationTests, MigrationTests: entorno exclusivo, flag off sin servicios, unicidad/historial |
| 2.1–2.3 | SenderTests: Graph v23.0/Bearer/payload, errores Meta, cancelación, 5.500+ caracteres y Unicode |
| 3.1–3.3 | WebhookTests HTTP: 200/challenge, 403, 401 sin escritura, dos POST concurrentes y cronómetro; InboundFlowTests demuestra una sola respuesta |
| 4.1–4.3 | Migración + PatientResolutionTests: normalización, paciente activo, vínculo e hilo idempotentes, número desconocido |
| 5.1–5.3 | InboundFlowTests + LanguageTests: handler/cliente HTTP reales, patient_id/user_id/language/thread_id, sin control_context, fallo seguro, replay y comandos |
| 6.1 | Migraciones y MigrationTests: esquema, restricciones, trazabilidad y reversa |
| 6.2 | ScheduleTests y ReminderTests: backoff 120/240/480/960, dos fallos y éxito al tercero, 190 cancela, máximo cinco, dos workers y un intento |
| 6.3 | ScheduleTests y ReminderTests: Bogotá, cambio de día, fin de semana, 24 h sin consumir intento |
| 6.4 | RealSources_CreateFourKinds_AndCancelEachResolvedReason; RealEvaluationSubmit; RealProgramTaskCompletion; evaluación inicial sin inscripción activa |
| 6.5 | PatientReply_CancelsPendingReminderBeforeAck_AndContinuesAgent; opt-out y conversación voluntaria |
| 9.1 | README, OPERACION, scripts, informe y enlace desde OVERVIEW |

Conteos de lectura de la BD dev (sin PHI): 295 inscripciones Active, 975 semanas
Active con una tarea podcast en el snapshot, 994 asignaciones pendientes/en curso
de bateria-inicial activa/autoasignable y 0 rachas con last_active_date=ayer y
current_streak>0. Son conteos de fuentes, no destinatarios elegibles: el canal exige
vínculo, opt-out desactivado y revalida motivo/fecha/publicación antes de enviar.

## Verificación ampliada y límites

`dotnet test --no-build` completo se ejecutó. UnitTests: 2.006 aprobados y 105
omitidos; el módulo WhatsApp quedó verde. La suite global NO quedó verde:
IntegrationTests: 27 fallos, 26 aprobados, 62 omitidos; Community.IntegrationTests:
93 fallos, 5 aprobados. Reproducciones puntuales identifican fixtures sin
COP_TEST_DB_CONNECTION: MediaIntegrationTests falla por DbContext nulo y
Community ChatQueryTests por conexión no inicializada. No se modificaron esos
fixtures ni se migraron schemas ajenos en dev. Task 9.2 permanece pendiente.
No hay lint del repo; se verificaron build, git diff --check, parseo YAML y sintaxis
de los 11 bloques Bash del workflow sin ejecutarlos.

`Test-WhatsAppOperations.ps1` simuló la red y validó ocho payloads UTILITY es/en,
ejemplos nombre+detalle, creación idempotente, modo Plan sin red y rechazo de
recategorización a marketing. Sus mensajes APPROVED/enviada son respuestas del
mock: ninguna plantilla real se creó. Scripts PowerShell con BOM/CRLF; C#/Markdown
en UTF-8 sin BOM. No se encontraron nombres de plantillas de citas en el módulo.

Postman: se cargaron api-engineer, api-discovery y api-simulation. Orbit se consultó
para plantillas y documentación oficial de Meta para texto/webhook. El CLI instalado
no soporta `postman status`; no se creó un workspace ni se publicó ningún mock.
Las verificaciones están en .NET/TestServer/PostgreSQL y el simulador PS local.

## Decisiones de implementación

- Módulo ensamblado dentro del API; contratos en Application y entidades en Domain.
  El proyecto CoppAddresd.WhatsApp es la infraestructura del canal, no otro servicio.
- Un número por paciente en v1; teléfonos compartidos por varios perfiles activos
  reciben guía genérica. El vínculo conserva la identidad y el hilo del canal.
- Campos opcionales internos de chat agregan patient_id y exclusión de controles;
  el chat existente conserva sus defaults y la defensa de consentimiento.
- Racha en riesgo: vigente ayer y todavía sin calificar hoy. Podcast: snapshot
  canónico del día, fallback de plantilla y task_completions; no medios demo.
  Evaluación inicial: batería canónica, incluso antes de inscripción activa.
- Franja por defecto 09–18 Bogotá, lunes a viernes, check-in desde 10:00; dos horas
  de quietud tras interacción. Configurable. Las respuestas voluntarias siguen
  funcionando durante opt-out.
- Una aceptación termina el motivo; solo fallos transitorios reintentan. Accepted
  registra aceptación de Meta, no entrega/lectura confirmada. Un crash entre Meta
  y commit puede duplicar un envío; no hay atomicidad entre ambos sistemas.
- Costos documentados con la página oficial vigente; no se repite el supuesto
  inicial de cobro service después de 1.000 mensajes, que esa fuente no respalda.

## Pendiente de aprobación/ejecución externa

Fases 7–9: copy y aprobación UTILITY de las ocho plantillas; configuración del ARN
y flags; review/merge/push autorizado; despliegue; registro del webhook; QA real y
rotación/revocación de credenciales. El [runbook](OPERACION.md) contiene los comandos
y checklist. No se sincronizó ni archivó OpenSpec ni se tocaron AGENTS.md u otros repos.
