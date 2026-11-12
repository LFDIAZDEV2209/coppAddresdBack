# Operación pendiente de aprobación de Luis

Este documento prepara las fases 7–9. No se creó ninguna plantilla, webhook,
despliegue ni credencial durante la implementación. Ejecutar desde el backend,
con PowerShell 7 y una sesión sin transcripción/depuración de HTTP.

## 7. Plantillas

Revisar primero el copy (ocho plantillas, nombre+detalle del programa):

```powershell
./docs/modules/whatsapp/Meta-WhatsApp.ps1 -Action Plan
# Solo después de aprobación:
./docs/modules/whatsapp/Meta-WhatsApp.ps1 -Action CreateTemplates -WabaId 1174342838653556 -Apply
./docs/modules/whatsapp/Meta-WhatsApp.ps1 -Action ListTemplates -WabaId 1174342838653556 -Apply
./docs/modules/whatsapp/Meta-WhatsApp.ps1 -Action VerifyTemplates -WabaId 1174342838653556 -Apply
```

El script lee el token del entorno. Las credenciales pueden cargarse en una sesión
de proceso mediante Read-Host seguro, usando el patrón en Update-WhatsAppSecret;
no copiarlas a comandos, appsettings ni ficheros. Si Meta recategoriza a marketing,
reescribir el copy y esperar nueva revisión; no habilitar ese envío. El catálogo
contiene exclusivamente bienestar, racha, podcast y evaluación inicial es/en.

## 8. Preparar y desplegar

La rama es feature/canal-whatsapp. Los commits son locales y no hay push. Revisar
diff y pruebas; después Luis autoriza merge/push a dev (dispara el workflow).

Variables de GitHub para el entorno dev del workflow existente:

| Variable | Valor / acción |
|---|---|
| WHATSAPP_ENABLED_DEV | 0 por defecto; 1 solo para activar el canal tras migrar |
| WHATSAPP_REMINDERS_ENABLED_DEV | false hasta que VerifyTemplates confirme las ocho plantillas |
| WHATSAPP_SECRET_ARN_DEV | ARN completo del secret cooppadresd/whatsapp, sin sufijos de campo |

Claves JSON del secret: WHATSAPP_ACCESS_TOKEN, WHATSAPP_PHONE_NUMBER_ID,
WHATSAPP_APP_ID, WHATSAPP_APP_SECRET, WHATSAPP_WEBHOOK_VERIFY_TOKEN. ECS recibe
referencias `ARN:CLAVE::`; el runner no lee sus valores. El execution role de ECS
debe tener secretsmanager:GetSecretValue y kms:Decrypt cuando corresponda. El flag
se inyecta aparte como configuración no secreta. No añadir un valor secreto al
workflow. El Dockerfile de la API incluye el nuevo csproj en la capa de restore.

Secuencia tras aprobación:

1. Deploy con canal apagado. El modo --migrate aplica primero el AppDbContext
   existente y después WhatsAppDbContext (cuatro migraciones, historial separado).
2. Verificar rollout y health de la API. El job de CI añade PostgreSQL efímero
   local para las pruebas WhatsApp; no usa RDS.
3. Configurar ARN y activar solo WHATSAPP_ENABLED_DEV. Confirmar 403 con handshake
   inválido y 401 ante POST sin firma en la URL pública del gateway.
4. Registrar webhook con callback público terminado en /api/v1/whatsapp/webhook.
   Usar la URL real publicada por el gateway (no localhost ni self-HTTP).

```powershell
$callback = Read-Host 'URL HTTPS pública completa del webhook'
./docs/modules/whatsapp/Meta-WhatsApp.ps1 -Action RegisterWebhook -CallbackUrl $callback -Apply
./docs/modules/whatsapp/Meta-WhatsApp.ps1 -Action Subscribe -WabaId 1174342838653556 -Apply
```

5. Confirmar webhook verificado y campo messages en Meta; iniciar conversación
   desde el número QA. Solo después de aprobar las ocho plantillas, activar
   WHATSAPP_REMINDERS_ENABLED_DEV y desplegar.

QA (registrar evidencia saneada en openspec/changes/canal-whatsapp/qa/, en la raíz):

- [ ] Dos turnos reales en español y dos en inglés; hilo estable y respuesta del agente.
- [ ] Reenvío del mismo wamid: una sola respuesta y duplicate_count incrementado.
- [ ] Número desconocido: guía genérica, sin datos clínicos ni conversación IA.
- [ ] Racha real pendiente hoy; completar tareas hasta calificar y comprobar cancelación.
- [ ] Podcast publicado asignado al día; escuchar/completar y comprobar cancelación.
- [ ] Test de batería inicial pendiente; entregar evaluación y comprobar cancelación.
- [ ] Check-in dentro de franja; siguiente toque del mismo tipo diferido 24 h.
- [ ] Error transitorio simulado en entorno de QA → backoff, trazabilidad y máximo cinco.
- [ ] SALIR/STOP, conversación voluntaria durante baja, INICIAR/START y reactivación.
- [ ] Texto fuera de 24 h rechazado; plantilla aprobada recibida fuera de ventana.
- [ ] Mensaje del nutricionista únicamente como respuesta del agente dentro de ventana.
- [ ] Logs sin valores de credenciales ni teléfonos completos.

Envío real de plantilla, únicamente con destinatario QA autorizado:

```powershell
./docs/modules/whatsapp/Meta-WhatsApp.ps1 -Action SendTemplate -WabaId 1174342838653556 -Recipient 573053924819 -TemplateName checkin_bienestar -Language es -Apply
```

Rollback: poner WHATSAPP_ENABLED_DEV=0 y WHATSAPP_REMINDERS_ENABLED_DEV=false y
redesplegar; opcionalmente desregistrar el webhook en Meta. No borrar tablas.
El canal apagado responde 404; las migraciones permanecen. No usar database update 0
en producción. En BD temporal se puede comprobar Down y volver a aplicar.

## 9. Rotación (no realizada)

1. En Meta, generar nuevo token del system user con permisos WhatsApp y regenerar
   app_secret. Planificar la transición de firma: al cambiar el secreto, webhooks
   con el anterior se rechazan hasta actualizar ECS. Conservar rollback mientras
   Meta lo permita y coordinar el corte.
2. Tener AWS.Tools.SecretsManager instalado en la estación administrativa y una
   sesión AWS con permisos mínimos; no usar access keys en el script.
3. Revisar Update-WhatsAppSecret.ps1 (modo por defecto no escribe). Tras aprobación,
   ejecutar con -Apply e introducir los cinco campos con Read-Host -AsSecureString.
   El SDK envía JSON en memoria; no hay secret-string en argumentos ni archivos.
4. Redesplegar tareas: ECS no refresca automáticamente las variables de un secret.
5. Validar el token sin imprimirlo y verificar firma/recepción reales:

```powershell
./docs/modules/whatsapp/Update-WhatsAppSecret.ps1
# Tras aprobación y con valores nuevos generados en Meta:
./docs/modules/whatsapp/Update-WhatsAppSecret.ps1 -Apply
./docs/modules/whatsapp/Meta-WhatsApp.ps1 -Action ProbeToken -Apply
```

6. Repetir conversación y envío de plantilla QA. Revocar token antiguo y confirmar
   que ya no es válido; conservar solo evidencia de is_valid, nunca el token.
   La actualización de Secrets Manager por sí sola no revoca credenciales en Meta.

## Costos consultados el 2026-11-12

La [tarifa oficial de Meta](https://business.whatsapp.com/products/platform-pricing)
cobra por mensaje entregado según mercado y categoría. Indica que los mensajes
service y utility enviados en respuesta al usuario son gratuitos; la ventana de
servicio dura 24 h desde su último mensaje. Consultar la rate card vigente de
Colombia/mercado del destinatario antes de activar volumen. No usar como presupuesto
el supuesto de “1.000 service gratis y cobro posterior” que aparece en el contexto
inicial del change: no coincide con la página oficial consultada. Una plantilla
recategorizada a marketing cambia su tratamiento; verificar UTILITY antes de activar.
