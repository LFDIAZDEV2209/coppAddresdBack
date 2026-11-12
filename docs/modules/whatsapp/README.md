# Canal WhatsApp

Módulo del monolito (`CoppAddresd.WhatsApp`), con contratos en Application y entidades
en Domain. No incorpora otro servicio desplegable. OpenSpec:
`openspec/changes/canal-whatsapp` en la raíz del workspace (decisiones D1–D9).

## Configuración

Solo variables de entorno: `WHATSAPP_ENABLED` (`0` por defecto),
`WHATSAPP_ACCESS_TOKEN`, `WHATSAPP_PHONE_NUMBER_ID`, `WHATSAPP_APP_ID`,
`WHATSAPP_APP_SECRET`, `WHATSAPP_WEBHOOK_VERIFY_TOKEN`. Las cinco son obligatorias
al habilitar el módulo. Con el flag apagado no registra servicios del canal.

## Persistencia

Schema exclusivo `whatsapp`, historial `whatsapp.__ef_migrations_history`.
El modo `--migrate` aplica este contexto incluso con el canal apagado; no necesita
credenciales de Meta. Nunca auto-migrar durante el arranque normal.

```powershell
dotnet ef database update --project src/CoppAddresd.WhatsApp --context WhatsAppDbContext
dotnet ef migrations list --project src/CoppAddresd.WhatsApp --context WhatsAppDbContext
dotnet test tests/CoppAddresd.WhatsApp.Tests
```

## Envío

`IWhatsAppMessageSender` (Application) devuelve wamid o error clasificado, sin
propagar cuerpos de Meta. `MetaCloudWhatsAppSender` usa Graph v23.0, Bearer por
petición y timeout de 15 s. HTTP 429/5xx y códigos 1/2/4/80007/130429 son
transitorios; 190 tiene prioridad permanente. No hay retry HTTP automático.
El worker divide la respuesta en fragmentos de hasta 4096 unidades UTF-16 sin
romper pares sustitutos. El sender rechaza texto si la última entrada tiene 24 h
o más; los recordatorios usan siempre plantillas deterministas (D9).
HttpClient no registra cuerpos, URLs ni headers del proveedor. Los logs propios
solo contienen teléfono enmascarado y códigos de error numéricos/internos.

Contrato contrastado con [texto de Meta](https://whatsapp.github.io/WhatsApp-Nodejs-SDK/api-reference/messages/text/)
y Orbit (payload de plantillas). La verificación local no envía mensajes reales.

## Webhook

`GET /api/v1/whatsapp/webhook`: handshake subscribe y verify token válido → texto
del challenge, 200; cualquier otro caso → 403. Flag apagado → 404.
`POST`: HMAC-SHA256 sobre bytes originales, comparación de tiempo constante;
401 sin persistencia ante firma ausente/inválida. Máximo 1 MiB por request.
El endpoint persiste cada mensaje antes del 200 (índice único wamid), cuenta
duplicados y emite una señal de cola en proceso. No ejecuta el agente en HTTP.
Solo acepta el phone_number_id configurado. Recibos de estado no abren ventana.
El payload completo es información sensible: no se imprime en logs.

## Identidad y conversación

`FindActivePatientByPhoneQuery` pertenece a Patients; su adaptador consulta solo
identidad y teléfono del perfil activo, sin datos clínicos. Normaliza prefijo,
espacios y puntuación (nacional colombiano sin prefijo → 57). Ante varios perfiles
activos con el mismo número responde como desconocido: no elige arbitrariamente.
La primera búsqueda normalizada recorre perfiles; ocurre una vez por vínculo y
no añade índices ni funciones en schemas ajenos. Luego `patient_links.wa_id`
es la fuente del canal (D3). V1 admite un número por paciente y una conversación.
El vínculo conserva user_id para reutilizar la identidad y consentimiento del chat.
Un teléfono desconocido recibe guía de registro sin crear conversación de agente.
`ON CONFLICT` y unicidad evitan vínculos/hilos duplicados en primeras entradas
concurrentes. No hay FK desde `whatsapp` a `app` ni a `auth`.

## Conversación y recuperación

El worker sondea la BD cada segundo además de drenar señales de cola. Reclama un
evento con `FOR UPDATE SKIP LOCKED` y lease de cinco minutos en una sola sentencia,
libera el lock y procesa con límite de dos minutos. Ordena por recepción por número;
un turno anterior pendiente impide que otro worker adelante el mismo hilo.
Invoca `ChatCommand` por MediatR (el mismo handler de ChatController), agente `base`
por defecto, user_id real cuando existe y patient_id; sin control_context en v1.
Los campos nuevos son opcionales: clientes existentes conservan su comportamiento.
El cliente de IA mantiene su validación de consentimiento, también en background.

Idioma inicial es; cambia a en solo con señal clara y se conserva entre turnos.
SALIR/STOP e INICIAR/START son insensibles a caso y acentos; confirman y cambian
opt_out sin consultar al agente. Opt-out no bloquea conversaciones iniciadas por
el paciente. Si la IA falla (límite 90 s), se guarda la causa técnica tipada y se
envía un texto seguro. No se guardan excepciones ni cuerpos del proveedor en logs.

La respuesta se persiste antes del envío, y cada fragmento aceptado avanza un
cursor durable. Un fallo transitorio del sender reintenta el fragmento pendiente
con backoff de 5–60 s (máximo cinco intentos), sin repetir el LLM. Permanente o
ventana cerrada termina el evento con código de error. Un reinicio recupera leases
vencidos. Existe una ventana inevitable si el proceso muere después de que Meta
acepte y antes del commit del cursor: Cloud API no ofrece una clave idempotente
del cliente para garantizar exactly-once en ese punto. El dedupe de webhooks y el
cursor evitan duplicados ordinarios; no se promete atomicidad entre Meta y la BD.

## Acompañamiento (D9 actualizado por Luis)

No hay funciones ni plantillas de citas. El nutricionista responde por el agente
dentro de la conversación y su ventana de 24 h; no tiene plantilla proactiva.

| Tipo | Fuente canónica en el backend | Motivo resuelto |
|---|---|---|
| Bienestar | Paciente activo + inscripción `Active` | Interacción, baja o programa inactivo |
| Racha | `streak_states`: CurrentStreak > 0 y LastActiveDate = ayer en Bogotá | Día calificado hoy o racha ya no vigente |
| Podcast | `program_weeks.tasks_snapshot` del día, fallback `weekly_day_templates.media_id`, medio Podcast publicado y no demo | `task_completions` podcast en la fecha local o contenido desasignado/no publicado |
| Evaluación inicial | Asignaciones pending/in_progress de `bateria-inicial`, activa y AutoAssignOnPatientCreate; sin evaluación completada | Evaluación entregada, asignación cancelada/vencida o batería deshabilitada |

Racha en riesgo significa racha vigente ayer que aún no calificó hoy, la misma
vigencia que `ProgramRepository.EffectiveCurrentStreak`; no se altera el contador
ni el inventario de congelamientos. Se reutiliza `WeekSnapshotJson.Parse` para el
podcast y la prioridad snapshot → plantilla del snapshot móvil. La evaluación
inicial es elegible aunque todavía no exista inscripción activa; los otros tres
tipos requieren programa activo. No se recuerdan baterías clínicas ajenas al
onboarding. Las claves incluyen inscripción/fecha/medio o asignación real.

`ProgramAccompanimentQuery` lee por lotes de hasta 100 vínculos, sin consultas por
paciente en loops. El scheduler crea los motivos con un INSERT por lote y
ON CONFLICT. Los flujos de asignar batería, asignar podcast a inscripción y
completar tarea invocan hooks opcionales; SubmitEvaluation cancela después del
commit clínico. El polling reconcilia también cambios de plantilla, alta automática
y fallos del hook. Cada envío revalida el motivo contra su fuente actual.

`WhatsApp__Reminders__Enabled=false` por defecto: habilitar solo tras aprobación
de las ocho plantillas en Meta. Configuración no secreta: StartHour=9, EndHour=18
(fin exclusivo), DailyHour=10 y ActiveConversationMinutes=120. Zona America/Bogota,
lunes a viernes. Fuera de franja se difiere al siguiente día hábil. Una aceptación
impone 24 h sin otro toque del mismo tipo; el diferimiento no consume intento.

Estados de Reminder: Pending, Completed, Cancelled, Exhausted. Cada intento tiene
fila ReminderDelivery (paciente, tipo, número, fecha, wamid, estado y error).
Accepted significa aceptación por Meta, no confirmación de lectura/entrega final.
Un lease vencido marca el intento anterior Dispatching como Unknown. Los errores
transitorios esperan 120, 240, 480 y 960 minutos (ajustados a franja), máximo cinco
intentos; los permanentes cancelan. El índice parcial admite un pendiente por tipo
y paciente. El claim usa SKIP LOCKED; reserva intento+entrega en transacción corta,
confirma y después llama a Meta. Los updates finales no reabren cancelaciones.

El webhook hace visible la interacción antes de su ACK y cancela reintentos ya
programados; el procesador cancela todos los pendientes del paciente antes del
agente. Un envío ya aceptado/en curso en Meta no se puede revocar.

## Pruebas locales

El proyecto `tests/CoppAddresd.WhatsApp.Tests` separa traits Category=Unit e
Integration. Integra PostgreSQL real en bases desechables `copp_whatsapp_test_*`;
solo admite host local. Usa COP_TEST_DB_CONNECTION si existe o configuración local
del API sin imprimirla. Las pruebas de recordatorios crean el modelo EF completo
en esa BD temporal y usan los repositorios/handlers clínicos reales. No migran
schemas de otros módulos en la BD de desarrollo. El AI Service se simula por HTTP
con TestServer y el cliente/handler de chat reales. Meta usa fakes controlados.
