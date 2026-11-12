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
