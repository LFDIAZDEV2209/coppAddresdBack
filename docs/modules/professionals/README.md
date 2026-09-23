# Acceso ERP de profesionales — fase 1

Un profesional Activo/Inactivo conserva su cuenta y acceso como paciente. El estado Invitado requiere terminar onboarding. El comando exige `Employees.Update`, audiencia ERP y prohíbe desactivar al propio actor. No modifica roles ni permisos.

## Contrato y coordinación

`PUT /api/v1/employees/{id}/access` recibe `{ "operationId": "uuid", "status": "Active|Inactive" }`. Responde `{ operationId, status, pending }`: 200 cuando directorio y Auth están coordinados; 202 cuando Auth confirmó y falta proyección. Un fallo de transporte tiene resultado incierto: reintentar con el mismo UUID y payload.

Auth es la autoridad. Una transacción incrementa `UserApplications.SessionVersion`, actualiza `IsSuspended`, revoca únicamente refresh ERP y registra `ErpAccessOperations`. El índice único parcial de UserId con CompletedAt nulo impide cambios simultáneos pendientes. El token de concurrencia SessionVersion detecta escrituras competitivas. No se mantiene una transacción durante HTTP.

API proyecta estado y versión mediante UPDATE condicional: una versión anterior nunca pisa una nueva. Confirma el journal después. Un worker cada cinco segundos recupera hasta 100 pendientes; el listado consulta pendientes por lote, sin N+1. Las métricas solicitadas con `fresh=true` consultan BD y reemplazan su entrada de caché.

Los endpoints internos bajo `/api/auth/internal/erp-access` requieren la clave interna existente. No se exponen como acciones del navegador. Todos los caminos propagan CancellationToken.

## Sesiones y disponibilidad

JWT incluye `application_session_version`; tokens anteriores sin claim equivalen a versión cero. Login, refresh y OTP ERP verifican la asignación no suspendida. Reactivar incrementa otra vez la versión: no revive JWT ni refresh antiguos. Las sesiones app no cambian.

API, Telemedicina y Community consultan Auth por cada petición ERP; sin caché positiva. Auth devuelve 204 o 401. Un fallo de autoridad HTTP produce 503 y Retry-After en consumidores REST, sin convertir una interrupción de servicio en cierre de sesión. La validación de Community WS ocurre al conectar, iniciar operación y antes de cada resultado; una conexión inactiva no se cierra inmediatamente por TCP, pero no entrega nuevos resultados autorizados con una sesión revocada.

La lectura remota añade latencia y dependencia de Auth (timeout cinco segundos). No se permiten peticiones ERP cuando no se puede comprobar su autorización. [Mapa de audiencias y límites](erp-audiences-phase01.md).

## Migraciones y despliegue

- Auth: `20260913184501_AddErpApplicationSuspension`, columnas aditivas y journal. Auth auto-migra al arrancar.
- API: `20260913184538_AddEmployeeErpAccessVersion`, columna aditiva `erp.employees.erp_access_version` con cero inicial.
- SQL revisado y ambas migraciones aplicadas al PostgreSQL local. La BD local estaba atrasada: EF aplicó también migraciones previas de dev pendientes.
- **AuthService__BaseUrl** debe existir en API, Telemedicina y Community con la URL interna real de Auth, y conservarse la clave interna API/Auth. **Resuelto**: el pipeline (`deploy-backend.yml`) ya la inyecta para Telemedicina y Community con la URL del ALB (`http://cooppadresd-alb-269201785.us-east-2.elb.amazonaws.com`); la API ya la tenía en su task definition. Sin ella, todo token ERP respondía 500 (`Falta AuthService:BaseUrl...`). En desarrollo cada appsettings local (gitignoreado) usa `http://localhost:5123`; no usar localhost para contenedores separados.
- Orden recomendado: esquema Auth/API, Auth actualizado, consumidores actualizados, frontend. El pipeline existente migra API después del rollout: coordinar la migración antes de habilitar el toggle para evitar una ventana con columna ausente. No se ejecutó despliegue remoto.
- El detector de servicios ahora incluye `src/Shared/`, para recompilar consumidores cuando cambia el validador enlazado.

No volver a binarios antiguos durante una suspensión: desconocen el bloqueo y podrían admitir sesiones ERP. Preferir corrección hacia delante. Revertir una migración elimina versiones/journal y requiere una decisión operativa; no se ejecutó rollback.

## Recuperación y operación

Si aparece pendiente, revisar conectividad y logs de Auth/API. Restablecer el servicio permite recuperar sin repetir el cambio de estado. Si cambia o desaparece el vínculo employee/user, el UPDATE no confirma: investigar esa operación; no alterar manualmente la versión ni marcarla completada sin verificar proyección. El journal conserva operaciones completadas para idempotencia; definir retención antes de purgar. Una acumulación de operaciones fallidas puede retrasar el lote de 100: monitorizar antigüedad y volumen.

La FK del journal a Users es restrictiva para conservar evidencia; una futura eliminación física de un usuario con operaciones exige una política explícita. No se modificó eliminación de usuarios en esta fase.

## Evidencia

- 24 pruebas focalizadas de comando/directorio/versiones y 4 de integración PostgreSQL: suspensión, reactivación, preservación app, idempotencia, concurrencia y rollback. Bases aisladas por prueba; no llamadas a pacientes.
- 16 pruebas de permisos API y 8 de Telemedicina; 4 del consumidor Auth y 3 de WebSocket con sesiones existentes simuladas.
- Suite Telemedicina: 186 pasan. Community: 115 pasan, 5 omitidas.
- Suite UnitTests antes de añadir las cuatro últimas pruebas de consumidor: 673 pasan, 95 omitidas, 2 fallan en FoodAiAnalyzeEndpointTests. No declarar la suite global verde; TRX local en TestResults.
- Comparación estática con HEAD `19e4ada`: ambos fallos Food AI están en código no modificado. El handler omite Intake al construir el resultado; el caso gramos cero usa DatabaseNutritionProvider real con credenciales ficticias (`28P01`). No se ejecutó de nuevo sobre HEAD ni se corrigió el módulo ajeno.
- Builds por proyecto Auth/API/Telemedicina/Community correctos; advertencias existentes de nulabilidad/EF permanecen.
- QA del frontend: ver `coppaddresd-front/docs/erp-phase-01-review.md`.
