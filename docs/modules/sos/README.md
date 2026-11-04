# Avisos SOS al contacto de emergencia

Los avisos se construyen únicamente en el servidor. El paciente autenticado
activa la alerta; su contacto configurado es quien recibe llamada, SMS y correo.
La selección manual de ambulancia, policía o bomberos en el teléfono es una
acción independiente y no se presenta como un despacho automático.

## Mensajes

- `SosSmsTemplate.Build(alert, locationUrl?)`: SMS breve con el nombre, la
  acción de llamar, teléfono válido del paciente cuando existe, enlace de
  ubicación y solo las lecturas recibidas. No incorpora documento, edad,
  referencia interna de alerta ni precisión GPS.
- `SosEmailTemplate.BuildHtml(alert, locationUrl?)`: correo HTML con marca Copp
  Adresd, nombre, fecha UTC explícita, llamada y ubicación como botones,
  lecturas opcionales y explicación de por qué el destinatario lo recibe.
  Funciona sin imágenes remotas y escapa todos los valores dinámicos.
- `SosEmailTemplate.BuildText`: versión de texto disponible para integraciones.
  El contrato actual de `IEmailService` envía un único cuerpo; el despacho SOS
  usa HTML, sin afirmar que envíe una alternativa MIME de texto.
- `SosEmailTemplate.Subject` es estático, sin datos personales en el asunto.

El enlace opcional permite usar una URL compartida del propio servicio, sin
coordenadas en el mensaje. Cuando no hay ese servicio/configuración se conserva
un enlace HTTPS válido a Google Maps; las coordenadas forman parte de su URL,
sin el antiguo sufijo de precisión que la invalidaba. El HTML muestra un botón
y no imprime la URL. Sin GPS, se indica «Ubicación no compartida»; la dirección
registrada del paciente no se usa como si fuera su ubicación actual.

No se completa ninguna lectura ni se inventan valores faltantes. La aplicación
debe enviar únicamente las lecturas reales que haya obtenido. La hora del
correo se marca en UTC porque la alerta no persiste la zona horaria actual del
dispositivo; no se asume que un paciente en Estados Unidos esté en Colombia.

## Enlaces de ubicación legibles

`SosLocationLinks:PublicBaseUrl` configura el origen HTTPS público de la API
o del gateway que enruta `/api/v1/sos/maps/*`, sin ruta ni parámetros. Si no
se define, se reutiliza `SosWebhook:BaseUrl`. Ejemplo de despliegue:
`SosLocationLinks__PublicBaseUrl=https://erp.coppadresd.com`. Esta opción no
cambia las URLs de callback de Twilio. El origen público no es un secreto.

Los enlaces se firman usando la clave **existente** `Storage:SignatureKey`
(al menos 32 bytes), con un propósito separado. No hay nuevas credenciales
ni migraciones. Si falta el origen o la clave no cumple el mínimo, el mensaje
conserva un enlace directo de Maps para no perder la ubicación durante la
emergencia; en ese fallback SMS sí muestra las coordenadas dentro de la URL.
Configurar el origen público es necesario para ocultarlas también en el SMS.

El token no contiene coordenadas, nombre ni teléfono. El endpoint valida la
firma antes de consultar la alerta y solo redirige a Google Maps. Caduca a
las 24 horas de crear la alerta; cancelarla lo revoca de inmediato. Marcarla
como atendida no rompe la ubicación ya compartida durante esa ventana.
No devuelve datos clínicos ni el perfil del paciente. Usa `no-store`,
`no-referrer`, `noindex` y un límite de 30 consultas/minuto por IP.

El middleware de aplicación oculta el token en rutas de logs y errores.
Los registros de acceso del proxy/ALB deben omitir o redactar el segmento
`/api/v1/sos/maps/*`; la URL compartida es una capacidad privada del contacto.

## Entrega y privacidad

Se conservan el dedupe `sos:sms:{alertId}` / `sos:email:{alertId}`, los reintentos
existentes, la propagación de cancelación y los estados por canal.
`IEmailService.IsConfigured` informa si existe un transporte real. SMTP requiere
configuración y estar habilitado; el proveedor `Log` devuelve false. SOS marca
`NoConfigurado` sin intentar enviar si no hay transporte, en vez de mostrar un
correo simulado como enviado.

Los adaptadores SMTP/Log no registran destinatarios, nombres, asuntos ni
fragmentos del cuerpo. Las excepciones de correo SOS se registran por tipo y
alerta, sin copiar mensajes del proveedor que puedan contener datos personales.

## Verificación

Las pruebas de `SosMessageTemplatesTests`, `SosEmailTransportReadinessTests`,
`SosLocationLinkTests` y `SosDispatchersTests` comprueban acciones, HTML escapado,
esquemas de enlaces, firma, caducidad, revocación, datos ausentes, privacidad de
logs y entrega no configurada. Las 134 pruebas SOS pasan en este entorno. No
envían mensajes ni efectúan llamadas reales. La integración con PostgreSQL
requiere una base de pruebas aislada y no se ejecutó en esta entrega.

```sh
dotnet test tests/CoppAddresd.UnitTests --filter 'FullyQualifiedName~Features.Sos'
dotnet build
```

En el SDK Linux 10.0.112 de este entorno falta la información de poda de
paquetes del framework ASP.NET; la validación local se ejecuta con
`-p:AllowMissingPrunePackageData=true`, sin cambiar los archivos del proyecto.


## Destinatarios principal y respaldo (noviembre 2026)

La migración `20261104152746_AddSosRecipientSnapshots` agrega
`app.sos_alert_recipients`. La activación toma una instantánea del contacto
principal y del respaldo opcional del perfil; el cliente no elige destinatarios.
Cada fila conserva teléfono/correo de envío, estados y datos de proveedor por
canal. Las respuestas exponen solo teléfono enmascarado y estados.

Se reservan claves outbox por canal y contacto: principal conserva la clave
histórica; respaldo agrega `:1`. Un teléfono/correo repetido dentro de la alerta
se procesa una vez, sin generar dos llamadas al mismo número. Un fallo en el
principal no interrumpe el respaldo. Alertas anteriores sin instantáneas
conservan el camino legado.

SMS/voz registran callbacks con `alertId` y `recipientId`. El webhook valida
firma de Twilio sobre URL completa y comprueba que el destinatario pertenezca
a la alerta. Un callback de respaldo cambia su fila, sin atribuir la entrega
al principal. El agregado histórico sigue representando el principal; la app
muestra ambos contactos y sus estados independientes. «Enviado» es aceptación
del proveedor, no prueba de entrega ni de llamada contestada.

`tests/CoppAddresd.E2EHost` permite la prueba integrada contra Auth/API/Postgres
con transportes externos exclusivamente simulados. Exige una base `copp_review_`
y un directorio temporal. Nunca usar ese host para servir pacientes reales.
