# Informe — Vinculación WhatsApp por OTP (fase 10)

Implementación local del 2026-11-12 en `feature/whatsapp-otp-vinculacion`, creada
desde `dev` actualizado con `git pull --ff-only origin dev` (base `552e2f3`).
Commit funcional: `2091d6e` (`feat(whatsapp): vincular pacientes por OTP SMS de Auth`).
El commit siguiente incorpora este informe. Sin push, despliegue, SMS real ni
operaciones contra Meta/AWS; se conservan los nombres actuales de sus plantillas.

## Resultado y tareas

| Tarea | Implementación | Evidencia |
|---|---|---|
| 10.1 | BindingSession, estado/lease, intentos, dos reservas SMS, expiración y migración exclusiva whatsapp | BindingPolicyTests; migración local; Down/Up y constraints en PostgreSQL |
| 10.2 | Processor → query Patients → id-lookup → send-otp PHONE → verify-otp → vínculo/confirmación | BindingFlowTests: HTTP Auth simulado, processor/query/BD reales; siguiente mensaje al ChatCommand existente |
| 10.3 | Sin contraseñas; límites persistidos; teléfono/documento enmascarados; tokens/cookies/devCode descartados; prueba PHONE | Logs capturados y revisión de llamadas; pruebas de 429, sujeto incorrecto y Auth antiguo sin prueba de canal |
| 10.4 | Build y suite del módulo verdes, commits locales en la rama solicitada | 91 tests WhatsApp y 0 errores de build; árbol limpio al cerrar |

Flujo: `VINCULAR` → documento → respuesta con teléfono enmascarado → código SMS de
seis dígitos → confirmación. Se admite documento directo, REENVIAR y CANCELAR.
No se crea conversación IA ni se invoca al agente antes de aprobar. Una vez creado
patient_links, el siguiente mensaje usa la conversación y el agente existentes.

## Archivos y decisiones

- Domain: BindingSession y política de límites. Application: contrato del cliente OTP
  y query de identidad de Patients. Infrastructure: PatientBindingQuery, con filtro
  Activo, sin soft delete y máximo dos coincidencias para detectar ambigüedad.
- Módulo WhatsApp: cliente Auth configurable, flujo, DI, processor y guía actualizada.
  Schema: nueva binding_sessions y quinto registro de migración propio.
- Auth: `contactId` opcional en verify-otp; PHONE no admite un OTP EMAIL pendiente.
  Solo el consumidor que solicita PHONE recibe `verifiedContactId=phone`. El resto
  conserva su contrato. Se enmascararon documentos en los logs OTP.
- Pruebas: BindingPolicyTests y BindingFlowTests nuevos; regresiones de Auth y
  actualización de la guía/migraciones en pruebas existentes. Documentación en los
  README de WhatsApp/Auth y catálogos de índices/transacciones.

Decisiones necesarias para cerrar ambigüedades:

1. Dos envíos se interpreta como ventana móvil de una hora. Reiniciar/cancelar no
   borra reservas; reenviar no reinicia los tres intentos ni los 10 minutos. Se
   reserva antes del envío HTTP y se conserva la cuota ante fallo/timeout, para
   no excederla cuando la aceptación del SMS es incierta.
2. El TTL de 10 minutos es absoluto desde el inicio del flujo. Auth/Twilio puede
   invalidar antes su código; se respeta su resultado. Al agotar intentos o
   expirar, se requiere VINCULAR para iniciar otro proceso.
3. El perfil se revalida antes y después del check. Se exige correspondencia entre
   su user_id y el sujeto devuelto por el Auth configurado; el token se descarta,
   no se usa como sesión del canal. Documento y códigos no se almacenan en
   BindingSession. Al guardar la respuesta se limpia el evento de vinculación.
4. El endpoint verify-otp existente podía seleccionar EMAIL si aparecía un OTP de
   correo durante la espera. La extensión opcional `contactId=phone` y la prueba
   explícita de respuesta cierran ese caso. Sin prueba, incluso HTTP 200 falla
   cerrado; no se vincula hasta que Auth soporte esta versión.
5. Se conserva la unicidad actual de patient_links por paciente y número. No se
   reemplaza un vínculo existente a otro WhatsApp. Conflictos reciben respuesta
   segura; no se reasigna identidad automáticamente.

## Verificación ejecutada

```text
dotnet build --no-restore -v quiet -clp:ErrorsOnly
Build succeeded. 0 Error(s).

dotnet test tests/CoppAddresd.WhatsApp.Tests --no-build -v quiet
Passed: 91, Failed: 0, Skipped: 0.

dotnet test tests/CoppAddresd.WhatsApp.Tests --no-build --filter Category=Unit
Passed: 49, Failed: 0, Skipped: 0.

dotnet test tests/CoppAddresd.WhatsApp.Tests --no-build --filter Category=Integration
Passed: 42, Failed: 0, Skipped: 0.

dotnet test tests/CoppAddresd.UnitTests --no-build --filter [OTP/Protection/Twilio]
Passed: 80, Failed: 0, Skipped: 0.

dotnet test tests/CoppAddresd.UnitTests --no-build -v quiet
Passed: 2008, Failed: 0, Skipped: 105.
```

91 incluye 28 pruebas nuevas del canal sobre las 63 previas; se agregaron dos
regresiones específicas en OtpServiceTests. Los 105 omitidos son de la suite
general de unitarios, no de WhatsApp.

Migración `20261112183130_WhatsAppOtpBinding`: SQL revisado y aplicado en PostgreSQL
local; solo CREATE TABLE/INDEX en whatsapp, sin tocar schemas de otros módulos.
`migrations list` muestra las cinco migraciones. El test de reversa baja a cero y
reaplica en una BD desechable; `has-pending-model-changes` confirma ausencia de drift.
`git diff dev --check` sin errores. Archivos nuevos UTF-8 sin BOM.

Escenarios verificados: documento válido, inexistente, inactivo, eliminado y ambiguo;
aprobación y siguiente chat; tres códigos malos y cuarto sin Auth; expiración exacta;
dos envíos por hora aun cancelando/reiniciando; reenvío que conserva intentos;
429 de send/verify sin retry; baja del paciente durante espera; sujeto incorrecto;
Auth sin prueba PHONE; lease de worker expirado; payload duplicado sin otro SMS;
rechazo de contraseñas; logs sin documento/código/tokens; teléfono enmascarado y
limpieza del payload crudo de vinculación.

La infraestructura de integración usa PostgreSQL real con el modelo EF de la app,
fixtures aislados y un Auth HTTP simulado mediante TestServer. No se verificó envío
real con Twilio ni despliegue; esas acciones no se solicitaron. La suite global de
integración de otros módulos no se repitió para esta fase. Postman no se utilizó
para publicar recursos; el contrato reutilizado es el código local de Auth.

## Configuración para revisión y despliegue posterior

`WhatsApp__Auth__BaseUrl` es el origen/prefijo HTTP(S) de Auth accesible desde la
API, sin credenciales ni query. Default local: `http://localhost:5123`; el cliente
agrega `api/auth/...`. Mantiene WHATSAPP_ENABLED apagado por defecto.
Timeout 10 s, CancellationToken, sin redirects/cookies/reintentos automáticos.

Desplegar Auth con la prueba de canal antes de habilitar esta vinculación en el
backend. El canal rechaza una aprobación de versiones anteriores. No se cambió
ninguna task definition ni variable remota. La revisión/merge/push queda en manos
de Luis. Se marcaron exclusivamente 10.1–10.4; no se sincronizó ni archivó OpenSpec.
