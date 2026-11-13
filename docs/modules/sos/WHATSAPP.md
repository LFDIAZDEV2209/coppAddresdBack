# SOS por WhatsApp — KAN-66

Implementación local; apagada por defecto. No afirma entrega real ni aprobación de Meta.

## Flujo

- `SosWhatsApp:Enabled=true` reserva una fila por teléfono E.164 distinto del snapshot de contactos principal/respaldo en la **misma transacción** que la alerta. No consulta contactos actuales al reintentar. No reproduce alertas antiguas al habilitarse.
- Worker independiente, polling cada 2 s, claims SQL condicionales entre réplicas. No ejecuta SMS, voz ni correo; tampoco depende de que esos canales terminen.
- Dirección y enlace reutilizan los servicios existentes. Nombre y teléfonos se congelan al activar, lecturas provienen del snapshot SOS. Los cinco parámetros se persisten antes del primer HTTP y se reutilizan en reintentos. Datos ausentes se identifican, nunca se inventan ceros ni se presenta una toma vieja como actual.
- Solo inicia envíos de alertas activas con menos de 15 min. Canceladas/atendidas/vencidas quedan `Cancelled`. La carrera entre cancelación y HTTP ya iniciado no permite retirar un mensaje aceptado.
- Hasta tres intentos, backoff 15/30 s tras rechazo transitorio explícito. `Accepted` significa aceptación de Meta, no recepción. `timeout`, errores de red, ACK 2xx sin wamid y caídas con lease vencido quedan `Unknown`: sin reenvío automático, para evitar duplicados. No se promete exactly-once a través de HTTP.
- `State` (trabajo) y `DeliveryStatus` (proveedor) se guardan por destinatario. Correlación `biz_opaque_callback_data=sos:<deliveryId>` permite recibir callbacks antes del ACK HTTP. Firma SHA-256, número emisor y destinatario deben coincidir; sent → delivered → read son monotónicos. Failed no degrada delivered/read; no provoca reenvío automático.
- No se escriben cuerpos, coordenadas ni números completos en logs de aplicación. Los parámetros y teléfonos del outbox son datos clínicos privados, sujetos a la misma retención y acceso administrativo que SOS. No se expone un endpoint público de consulta del outbox.

## Alta de plantilla en Meta — 13/11/2026

Plantilla `copp_sos_alert_v1` enviada a la WABA del proyecto como UTILITY en español.
Meta devolvió `PENDING` (id `2633169853803885`). El emisor configurado fue verificado
mediante API; esto no equivale a aprobación de la plantilla ni a entrega del SOS.
El usuario autorizó un único mensaje WhatsApp de prueba y eligió esperar aprobación;
a la fecha de este registro no se ha enviado. La funcionalidad permanece apagada
por defecto. La prueba de recepción real y habilitación operativa siguen pendientes.

## Configuración y puesta en servicio

1. Aplicar `AddSosWhatsAppOutbox` mediante el flujo habitual de migración del backend, antes de habilitar. Up crea solo tabla e índices nuevos; Down elimina únicamente esa tabla nueva. Probar rollback solo en base desechable.
2. Registrar [whatsapp-template.json](whatsapp-template.json) en la WABA del proyecto y comprobar `APPROVED`, nombre `copp_sos_alert_v1`, idioma `es`. Categoría solicitada UTILITY; la decisión final es de Meta. [Referencia oficial de plantillas](https://developers.facebook.com/docs/graph-api/reference/whats-app-business-account/message_templates/).
3. Mantener WhatsApp global habilitado y sus credenciales existentes; configurar `SosWhatsApp__Enabled=true`, `SosWhatsApp__TemplateName=copp_sos_alert_v1`, `SosWhatsApp__Language=es`. Si el canal global está apagado, no se consume el outbox.
4. Webhook existente recibe los estados de mensajes: firma y `phone_number_id` correctos. El módulo de WhatsApp debe usar la misma versión del contrato actualizado para incluir correlación en el envío.
5. Probar un SOS controlado con destinatario expresamente autorizado y lecturas/ubicación conocidas. Verificar wamid, callback delivered y recepción física; no confundir ACK HTTP con entrega. La prueba real no se ejecutó en esta tarea.
6. Rollback funcional: desactivar `SosWhatsApp__Enabled`; los canales existentes siguen funcionando. Los pendientes no se borran y no se envían después de 15 minutos.

## Plantilla

Cinco parámetros: nombre, dirección, enlace, hora UTC del SOS, lecturas con unidades/procedencia/hora. Texto estático idéntico a `SosWhatsAppTemplate.Body`; no requiere LLM. El enlace corto existente se usa si está configurado; en otro caso se conserva un mapa Google válido. Sin GPS se indica ausencia de ubicación.

## Validación local

Host exclusivo `tests/CoppAddresd.SosWhatsApp.E2EHost`: loopback 5199, PostgreSQL desechable loopback 55439, transporte HTTP interceptado (sin red externa), UI de QA con botón que ejecuta el handler real de activación. Nunca conectar este host a producción.

```sh
docker run -d --name copp-sos-whatsapp-review -e POSTGRES_HOST_AUTH_METHOD=trust -e POSTGRES_DB=copp_review_sos_whatsapp -p 127.0.0.1:55439:5432 postgres:17-alpine
docker exec copp-sos-whatsapp-review psql -U postgres -d copp_review_sos_whatsapp -c 'CREATE ROLE app_user;'
dotnet run --project tests/CoppAddresd.SosWhatsApp.E2EHost -p:AllowMissingPrunePackageData=true
COP_E2E_OUTPUT=/tmp/copp-sos-whatsapp-review node tests/CoppAddresd.SosWhatsApp.E2EHost/sos-whatsapp.mjs
```

Playwright usa la instalación del repo hermano antares-paciente. El flag AllowMissingPrunePackageData compensa una instalación local incompleta de datos de poda del SDK; no modifica código de producción.

RED: activación Created, WhatsApp `[]`. GREEN: dos contactos, snapshot, replay sin duplicados; dos workers compitiendo; JSON del sender Meta productivo sin red; callbacks firmados, inválidos y fuera de orden; rechazo 429 con retry; fallo de red ambiguo sin reenvío. Pruebas sintéticas no prueban aprobación, credenciales vigentes ni entrega en un teléfono.
