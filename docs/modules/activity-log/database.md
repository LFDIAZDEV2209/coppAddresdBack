# Activity Log — Base de datos

## Base de datos

- Nombre: `coppaddresd` (minúsculas — PostgreSQL pliega identificadores sin comillas; ver script `scripts/database/01-create-database.sql`).
- Encoding: `UTF8`. Colación: la del servidor (dev local; en AWS RDS: UTF8 + ICU, ver `docs/aws/production.md`).
- Usuario app: `app_user` (dev). En producción: rol con permisos mínimos, secretos en Secrets Manager.

## Schemas

| Schema | Contenido |
|---|---|
| `public` | Tablas de negocio (aún ninguna) |
| `audit` | `activity_logs` + función trigger + helper `attach_table_audit` (aislado de negocio; permisos mínimos) |

## Tabla `audit.activity_logs`

| Columna | Tipo | Notas |
|---|---|---|
| `id` | `uuid` PK | `gen_random_uuid()` (built-in PG 13+, RDS-safe) |
| `occurred_at` | `timestamptz` | `clock_timestamp()` desde el trigger |
| `action` | `varchar(10)` | INSERT / UPDATE / DELETE (TG_OP) |
| `schema_name` / `table_name` | `varchar(64)` | TG_TABLE_SCHEMA / TG_TABLE_NAME |
| `record_id` | `text` | PK serializada de la fila |
| `actor_type` | `varchar(20)` | SYSTEM / ANONYMOUS / USER (futuro) |
| `user_id` | `uuid` NULL | Futuro `AspNetUsers.Id`. **Sin FK** (Identity llega después) |
| `user_email` / `user_role` | `varchar(320)` / `varchar(100)` NULL | |
| `ip_address` | `varchar(45)` NULL | IPv4/IPv6 |
| `request_id` / `correlation_id` | `varchar(100)` NULL | Trazabilidad con logs de la app |
| `old_data` / `new_data` / `changed_data` | `jsonb` NULL | `changed_data` solo en UPDATE, únicamente columnas cambiadas |
| `metadata` | `jsonb` NULL | Reservado |

## Función `audit.audit_trigger_function()`

- Sin argumentos declarados (regla PG para funciones trigger): recibe `TG_ARGV[0]` = columna PK, `TG_ARGV[1..]` = columnas sensibles excluidas.
- Ultra-ligera: sin joins, sin catálogo, sin dynamic SQL; solo `to_jsonb` + resta de claves + `jsonb_object_agg` para el diff.
- Actor leído de GUC: `audit.actor_type`, `audit.user_id`, `audit.user_email`, `audit.user_role`, `audit.ip_address`, `audit.request_id`, `audit.correlation_id` (`current_setting(..., true)`, ausencia = NULL/`SYSTEM`).
- Compatible AWS RDS: solo plpgsql + built-ins, **ninguna extensión**.

## Índices (documentados en `docs/database/indexes.md`)

- `ix_activity_logs_occurred_at` — rangos temporales.
- `ix_activity_logs_table_record` — historial por fila: `(table_name, record_id)`.

## Clasificación AUDIT_* (política por tabla)

| Clase | Regla | Ejemplo |
|---|---|---|
| AUDIT_REQUIRED | Tablas con datos de negocio críticos/regulatorios: siempre trigger completo | pedidos, usuarios (futuras) |
| AUDIT_OPTIONAL | Tablas de alto volumen donde el coste del índice/insert no compense; decisión documentada | logs de eventos no críticos |
| AUDIT_EXCLUDED | Tablas de soporte/efímeras, o que contienen secretos que ni así deben persistirse | sesiones, tokens |

Decisión vigente (2026-09, F4 de Telemedicina): las tablas clínicas y del
ciclo de vida de `tele.` se clasifican así.

| Tabla | Clase | Notas |
|---|---|---|
| `tele.clinical_encounters` | AUDIT_REQUIRED | PHI clínica; trigger en `AttachClinicalEncounterAudit` (Fase 12) |
| `tele.pre_visit_intakes` | AUDIT_REQUIRED | PHI reportada por el paciente; trigger en `AddPreVisitIntakeAndAddenda` (F4) |
| `tele.encounter_addenda` | AUDIT_REQUIRED | PHI append-only; trigger en `AddPreVisitIntakeAndAddenda` (F4) |
| `tele.appointments` | AUDIT_REQUIRED | Ciclo de vida (estado/reprogramación); trigger en `AttachAppointmentLifecycleAudit` (F4) |
| `tele.telemedicine_sessions` | AUDIT_REQUIRED | Inicio/fin/reapertura de sesión; trigger en `AttachAppointmentLifecycleAudit` (F4) |
| `tele.virtual_rooms` | AUDIT_REQUIRED | Creación/cambio de estado de sala; trigger en `AttachAppointmentLifecycleAudit` (F4) |
| `tele.chat_messages` | AUDIT_EXCLUDED | Alto volumen; contenido ya persistido y sin update/delete |
| `tele.notification_dispatch` | AUDIT_EXCLUDED | Dedupe operativo de recordatorios |
| `tele.telemedicine_webhook_events` | AUDIT_EXCLUDED | Idempotencia técnica del proveedor |
| `tele.appointment_cancellations` / `tele.appointment_reschedules` | AUDIT_EXCLUDED | Historial append-only; el efecto ya queda en el diff de `appointments` |

Los triggers de `tele.` son **condicionales e idempotentes**: el schema `audit`
es del backend y las migraciones del microservicio lo omiten sin error si no
existe (bases nuevas de tests). Actor: `USER` con el `user_id` del JWT en
acciones autenticadas (transacción explícita para que el interceptor dispare);
`SYSTEM` en barrido de sesiones estancadas y webhooks. La retención de
`activity_logs` sigue como decisión abierta (F4 no implementa purga).

## Transacciones y concurrencia

- La auditoría es 100% transaccional con la operación (misma tx; ver `docs/database/transactions.md`).
- Sin locks adicionales: el trigger solo inserta en su propia tabla.
- Concurrencia: GUC transaccionales aíslan el actor por transacción (test dedicado).
