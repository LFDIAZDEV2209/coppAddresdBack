# Avisos SOS al contacto de emergencia

## Lecturas congeladas al activar (KAN-65, 2026-11-12)

El SOS combina las últimas tomas reales disponibles en el teléfono con mediciones clínicas guardadas del mismo paciente. El teléfono admite tomas en vivo conservadas de hasta 24 horas aunque el reloj ya esté desconectado; excluye diagnósticos/debug, volcados históricos sin garantía temporal, fechas futuras y datos fuera de rango. El backend selecciona por métrica las lecturas fechadas más recientes de hasta 24 horas, con origen `device`, `patient`, `professional` o `lab`; prefiere una toma nueva reportada cuando corresponde. Un cliente anterior sin fecha se identifica como auto-reporte con hora desconocida. La presión solo combina sistólica/diastólica de la misma fecha y origen. No inventa datos ni completa faltantes con cero.

La migración aditiva `AddSosVitalsSnapshot` añade `app.sos_alerts.vitals_snapshot` (jsonb nullable). Preserva valor, unidad, fecha y procedencia: los reintentos e idempotencia conservan el mismo snapshot. La respuesta del SOS devuelve el snapshot compartido; la app no afirma haber compartido lecturas basándose únicamente en lo que intentó enviar. Correo HTML/texto muestra fecha UTC, procedencia y antigüedad respecto a la activación; SMS conserva valores/unidades, hora de activación y fechas de toma con zona COL explícita y antigüedad. Los datos ausentes se identifican por métrica cuando hay lecturas parciales. No se acortan signos ni se corta la URL para limitar segmentos. La llamada identifica los valores como últimas lecturas registradas.

Pruebas locales usan PostgreSQL aislado y APIs sintéticas, sin enviar SMS, correo ni llamadas reales. El canal SOS WhatsApp está implementado con outbox por destinatario y un interruptor desactivado por defecto. Ver [operación y plantilla](WHATSAPP.md); la aceptación real en Meta y la recepción en un teléfono requieren configuración y prueba autorizada.


Los avisos se construyen únicamente en el servidor. El paciente autenticado
activa la alerta; su contacto configurado es quien recibe llamada, SMS y correo.
La selección manual de ambulancia, policía o bomberos en el teléfono es una
acción independiente y no se presenta como un despacho automático.

## Mensajes

- `SosSmsTemplate.Build(alert, locationUrl?)`: abre con «Alerta SOS: {nombre}
  necesita tu ayuda». Incluye marca, todos los signos reportados, `Direccion:`,
  mapa completo y teléfono del paciente cuando es válido. Sin signos, lo indica.
  El objetivo es hasta dos segmentos GSM-7 (306 septetos), hasta cuatro Unicode
  con signos (268 unidades) o tres sin ellos. Abrevia nombre/referencia con `...`
  si hace falta; nunca elimina signos ni corta el enlace para imponer ese objetivo.
  No incorpora documento, edad ni referencia interna de alerta.
- `SosEmailTemplate.BuildHtml(alert, locationUrl?)`: correo HTML con marca Copp
  Adresd, nombre, fecha UTC explícita, llamada y ubicación como botones,
  lecturas opcionales y explicación de por qué el destinatario lo recibe.
  Funciona sin imágenes remotas y escapa todos los valores dinámicos.
- `SosEmailTemplate.BuildText`: versión de texto disponible para integraciones.
  El contrato actual de `IEmailService` envía un único cuerpo; el despacho SOS
  usa HTML, sin afirmar que envíe una alternativa MIME de texto.
- `SosEmailTemplate.Subject` es estático, sin datos personales en el asunto.
- `SosVoiceTemplate.Build(alert)`: lee la dirección aproximada del snapshot GPS
  antes de las lecturas opcionales. Ambos contactos oyen la misma referencia,
  sin URLs ni coordenadas dictadas. Con GPS grueso/desconocido se conserva
  únicamente la ciudad/área que resolvió el enriquecedor. Sin dirección o sin
  GPS, lo informa y pide confirmar la ubicación al contactar al paciente.
  Nunca sustituye el GPS por el domicilio del perfil ni afirma que el SMS ya
  llegó. Recomienda los servicios de emergencia de la ubicación, sin imponer
  un 911 que puede ser incorrecto fuera de Estados Unidos.

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
o del gateway que enruta `/api/v1/s/*` y `/api/v1/sos/maps/*`, sin ruta ni parámetros.
Si no se define, se reutiliza `SosWebhook:BaseUrl`. Ejemplo:
`SosLocationLinks__PublicBaseUrl=https://erp.coppadresd.com`. No cambia los
callbacks de Twilio. Ambas rutas quedan dentro del catch-all `/api/v1/*` existente.

Las alertas nuevas usan `/api/v1/s/{codigo}` con 22 caracteres base64url:
128 bits de HMAC derivados de `Storage:SignatureKey` (mínimo 32 bytes), con
propósito v2 separado del Storage y de los tokens v1. El código no contiene
coordenadas, identidad ni el UUID público de la alerta. Se regenera para los
canales y **solo su SHA-256** queda en `app.sos_alerts.location_link_hash`,
en el mismo commit que la alerta/outbox. El lookup usa un índice único parcial
y proyecta únicamente id, estado, fecha y coordenadas; no carga paciente/contactos.
No se añade proveedor de acortamiento ni otra clave secreta.

La migración `AddSosLocationShortLink` agrega una columna nullable sin backfill
y crea el índice concurrentemente. Las filas anteriores conservan el enlace
firmado `/api/v1/sos/maps/{token43}` y ambas rutas siguen funcionando. Si la
clave rota entre activación y despacho, el dispatcher usa el formato v1 firmado
con la nueva clave; los códigos cortos ya emitidos conservan su ventana de acceso.
Si falta el origen o la clave no cumple el mínimo, el mensaje conserva el
fallback directo de Maps (con coordenadas en la URL). El origen público es
necesario para ocultarlas también en SMS.

Ambos formatos caducan a las 24 horas de crear la alerta; cancelarla revoca
el enlace. Atenderla no impide consultarlo durante esa ventana. Solo redirigen
al dominio fijo de Google Maps; nunca a un destino elegido por el cliente.
No devuelven perfil ni datos clínicos. Usan `no-store`, `no-referrer`, `noindex`
y un límite compartido de 30 consultas/minuto por IP.

El middleware oculta los códigos de ambas rutas en logs y errores. Los registros
de acceso de proxy/ALB deben omitir o redactar `/api/v1/s/*` y
`/api/v1/sos/maps/*`; la URL es una capacidad privada del contacto.

### Validación de migración y lookup

SQL Up/Down revisado: `ADD COLUMN` nullable; `CREATE UNIQUE INDEX CONCURRENTLY`
fuera de transacción; rollback con `DROP INDEX CONCURRENTLY` y luego columna.
Up → Down → Up se comprobó en PostgreSQL 18 aislado con 2.002 alertas históricas,
sin pérdida de filas. Con 2.000 digests sintéticos y estadísticas actualizadas,
`EXPLAIN (ANALYZE, BUFFERS)` confirmó Index Scan sobre
`ux_sos_alerts_location_link_hash`; sin ese índice usó Seq Scan. Son mediciones
locales de corrección del plan, no una prueba de carga ni una promesa de latencia.
El rollback elimina los digests cortos, por lo que requiere
volver a la versión anterior antes de emitir más enlaces; las URLs cortas ya
emitidas dejan de resolver tras ese rollback. No ejecutar Down con la versión
nueva en servicio. Si falla la creación concurrente, revisar/eliminar el índice
inválido antes de reintentar; no dar por válida su mera existencia.

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
logs y entrega no configurada. No envían mensajes ni efectúan llamadas reales.
La integración con PostgreSQL requiere una base de pruebas aislada; el host
E2E descrito más abajo permite recorrer los canales sin transportes reales.

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


## Dirección aproximada en SMS y correo

El procesador de SOS resuelve el punto GPS mediante **Amazon Location Places V2**
(`ReverseGeocode`), fuera de la petición HTTP de activación. SMS y correo incluyen:

- Dirección aproximada (calle/ciudad cuando la precisión lo permite) y enlace de mapa.
- Hora de captura GPS en UTC, separada de la hora de activación de la alerta.
- Precisión declarada por el dispositivo, en metros o kilómetros; sin inventar valores.
- Si no hay GPS: «Ubicación no compartida», sin dirección, fecha GPS ni botón de mapa.
- Si falla la geocodificación: «Dirección no disponible» y mapa válido. El SOS continúa.

La calle se incluye solo con precisión GPS conocida de 0–100 m y un resultado
situado a 150 m o menos del punto. Con precisión gruesa/desconocida se muestra
ciudad/área. La dirección siempre es aproximada: no demuestra un edificio concreto
ni permite seguimiento en tiempo real. Nunca se sustituye por el domicilio del perfil.
Fechas de captura imposibles (antes de Unix epoch o más de 1 minuto después de la
activación) no se presentan como válidas ni se usan para enriquecer la dirección.

El lookup tiene un presupuesto total de **2 segundos**, sin reintentos AWS. La
activación HTTP no lo espera; el despacho de canales sí puede esperar ese máximo
antes de continuar. No se envían nombre, teléfono, signos vitales ni identidad al
geocodificador, solamente longitud/latitud y opciones de consulta. El servicio público
de Nominatim no se usa para coordenadas personales.

La migración `20261106203006_AddSosLocationDescription` agrega únicamente dos
columnas nullable en `app.sos_alerts`: `location_description` (varchar 300) y
`location_lookup_completed_at` (timestamptz). Se guarda tanto éxito como fallo para
que los reintentos reutilicen el resultado; compare-and-set decide un único snapshot
si dos réplicas compiten. No se agrega un índice ni se modifica información histórica.
En una carrera dos réplicas pueden iniciar un lookup, pero conservan el mismo snapshot
guardado. Si la persistencia falla se continúa sin dirección. Las plantillas por contacto
conservan también precisión, captura y snapshot: el principal y el opcional ven lo mismo.

### Activación en AWS

La opción está **deshabilitada por defecto en código**. El pipeline de la API la
activa explícitamente en Ohio, una vez añadido el permiso al task role.
Aplicar la migración antes del backend
nuevo y configurar en el task de la API:

```text
SosGeocoding__Enabled=true
SosGeocoding__Region=us-east-2
SosLocationLinks__PublicBaseUrl=https://<origen-publico-del-backend>
```

El pipeline de la API declara `https://erp.coppadresd.com` como origen del enlace.
El origen debe enrutar `/api/v1/s/*` y `/api/v1/sos/maps/*` públicamente por HTTPS y ya debe existir
`Storage__SignatureKey` (mínimo 32 bytes) en Secrets Manager. Sin origen/firma el
mapa sigue disponible mediante URL directa de Google, pero el SMS expondrá las
coordenadas dentro de esa URL. Verificar el origen real antes del rollout.

Agregar **al task role de la API**, no solo al usuario que despliega:

```json
{
  "Version": "2012-10-17",
  "Statement": [{
    "Effect": "Allow",
    "Action": "geo-places:ReverseGeocode",
    "Resource": "*",
    "Condition": {"StringEquals": {"aws:RequestedRegion": "us-east-2"}}
  }]
}
```

No requiere Place Index ni API key en la app. El SDK usa el rol del task; localmente
puede utilizar perfiles IAM estándar. Los perfiles `aws login` requieren
`AWSSDK.Signin` para uso directo del SDK; como alternativa de QA se pueden exportar
credenciales temporales con AWS CLI e inyectarlas solo en el entorno del proceso,
sin imprimirlas ni guardarlas en el repositorio. El task de ECS utiliza su rol y no
necesita ese paquete de desarrollo. No guardar claves AWS en el repositorio.
Se solicita `IntendedUse=Storage` porque el resultado queda guardado y se reutiliza.
La tarifa consultada para Ohio es US$0,004 por lookup (US$4/1.000), aparte de SMS/voz;
revisar precio vigente antes de habilitar. No afirmar gratuidad del nivel Stored.

Fuentes: [API ReverseGeocode](https://docs.aws.amazon.com/location/latest/APIReference/API_geoplaces_ReverseGeocode.html),
[uso de resultados almacenados](https://docs.aws.amazon.com/location/latest/developerguide/places-intended-use.html),
[precios](https://aws.amazon.com/location/pricing/),
[atribución](https://docs.aws.amazon.com/location/latest/developerguide/data-attribution.html).
El correo enlaza la atribución de Amazon Location cuando muestra una dirección.
La confirmación de SOS en la app informa del envío del punto GPS a Amazon
Location y permite abrir sus fuentes antes de activar la alerta. Por la restricción
de almacenamiento de AWS/HERE en Japón, una ventana conservadora (latitud 20–47,
longitud 122–155) omite el lookup Storage; en esa región se conserva el mapa original.
Ver [términos del servicio, sección 82](https://aws.amazon.com/service-terms/#82._Amazon_Location_Service).

### Verificación E2E sin mensajes reales

`antares-paciente/e2e/sos-location-messages.mjs` usa Playwright contra el host
`tests/CoppAddresd.E2EHost`, JWT de pacientes sintéticos y PostgreSQL `copp_review_*`.
Los transportes capturan cuerpos únicamente en `COP_E2E_OUTPUT` temporal; no envían
SMS, llamadas ni correo. `COP_E2E_GEOCODING=synthetic` sustituye solo el límite AWS
para probar direcciones de Colombia/EE. UU., GPS grueso/ausente, timeout, resultado
vacío, texto HTML, fecha futura, región con restricciones de almacenamiento, contactos principal/opcional, replay y revocación.
Las capturas prueban HTML en Chromium, no todos los clientes de correo ni entrega
por un proveedor. El modo sintético no prueba IAM, conectividad ni calidad de datos AWS.
El chequeo real usa solo coordenadas públicas de prueba y conserva transportes aislados.

El host E2E captura también el guion real de voz, exclusivamente en el directorio
temporal de QA. El recorrido comprueba dirección/calle, ciudad, GPS ausente,
timeout, fecha imposible y coincidencia entre principal/respaldo sin efectuar
llamadas. Capturar el guion no prueba pronunciación de Polly ni recepción por
un teléfono real.

### Diagnóstico de SMS no recibido

Registrar el SID y los estados sin cuerpo ni teléfonos completos. `queued`/
`sent`/`delivered` son estados del proveedor: `delivered` representa su recibo
del operador (y del terminal cuando está disponible), no confirma que el usuario
vea el mensaje. Si el usuario informa no recepción pese a ese estado, conservar
el incidente como discrepancia, revisar filtros/bloqueados del teléfono y escalar
al operador/Twilio con SID, hora y cantidad de segmentos. No reenviar SOS ni
alterar la deduplicación para compensar un recibo dudoso.

En Colombia puede cambiar el remitente mostrado y el soporte de concatenación
depende del tipo de remitente/codificación. Un SMS largo dividido en partes es
un dato para investigar, no prueba por sí solo la causa de la no recepción.
Fuentes: [estados de mensajes](https://www.twilio.com/docs/messaging/api/message-resource#message-status-values),
[guía de Colombia](https://www.twilio.com/en-us/guidelines/co/sms).

### SMS compacto y equivalencia de transporte

El SOS utiliza el mismo `POST /2010-04-01/Accounts/{AccountSid}/Messages.json`,
cuenta, remitente y formulario `To`/`From`/`Body` del diagnóstico corto recibido.
Conserva `StatusCallback` para seguir la entrega; no añade otro proveedor ni
reenvía automáticamente un mensaje que figure entregado.

La plantilla prioriza nombre, signos reportados, calle/ciudad y enlace firmado intacto.
Omite región/país repetidos cuando el snapshot ya contiene calle/ciudad. Solo
normaliza para SMS los diacríticos latinos que no caben en GSM-7; caracteres GSM
como `ñ` y `é` se conservan. No modifica datos guardados ni correo/voz. El
presupuesto cuenta los caracteres extendidos GSM como dos septetos. No corta
pares Unicode ni trunca el token del mapa. Los nombres/direcciones no latinos se
conservan en Unicode con objetivo de hasta cuatro segmentos con signos; un origen de mapa
excepcionalmente largo también puede requerir más de un segmento. La entrega
final sigue dependiendo del operador y del terminal.

El host E2E ejecuta ahora `TwilioSmsSender` y el SDK productivo; solo reemplaza
`ITwilioRestClient` en la frontera de red. Comprueba host, cuenta sintética,
endpoint Messages, método POST, remitente, destinos permitidos y campos del
formulario. Los recorridos verifican hasta dos segmentos GSM-7 para los casos latinos,
dirección, mapa válido, cancelación y deduplicación. Ni pruebas ni capturas
certifican por sí solas la recepción en un teléfono real del nuevo texto.


### Presentación de mensajes y enlaces cortos

El SMS usa «Alerta SOS: {nombre} necesita tu ayuda», incluye los signos vitales
persistidos sin inventar lecturas y rotula la dirección como `Direccion:`.
Conserva la URL íntegra; el dominio actual con código de 22 caracteres ahorra
28 caracteres frente al formato v1. La información adicional puede requerir
varios segmentos; el objetivo de dos partes GSM no equivale a garantía de entrega.

El correo es una tarjeta compacta con nombre, fecha UTC, llamada, dirección,
mapa y tabla de signos (o ausencia explícita). Se elimina el bloque de tres pasos
y las explicaciones repetidas. Captura/precisión GPS y vigencia del enlace se
conservan como texto secundario breve. El rótulo visible es «Dirección»; los datos
siguen siendo una referencia GPS, sin seguimiento en tiempo real.
`BuildText` mantiene la misma información esencial, pero el transporte SOS actual
sigue enviando HTML.

Evidencia: Playwright sobre API/JWT/PostgreSQL reales y HTML saliente renderizado
a 320/390/768 px. Fronteras Twilio/email/voz y geocodificador sintéticos: no envía
mensajes reales ni prueba recepción en iPhone o todos los clientes de correo.
Se comprueban ambos contactos, SDK SMS real con red aislada, integridad del enlace,
manipulación, cancelación, atención, expiración, compatibilidad v1, escape HTML,
datos ausentes y nombres largos/Unicode. El baseline falló por el texto anterior
antes de implementar los cambios.
