# Triggers — Registro

Reglas del proyecto (skill `database`): **no crear triggers salvo razón arquitectónica válida.**

## Checklist antes de usar un trigger

1. ¿Se resuelve en Application/Domain? — No: la auditoría debe capturar **toda** escritura, incluso las que escapan a los handlers (SQL directo, jobs, consolas, errores). Centralizarla en código exigiría que nadie escriba por otro camino: inviable.
2. ¿Con evento de dominio (MediatR)? — No: mismo problema de completitud + acoplar auditoría a cada feature.
3. ¿Con proceso background (job/SQS)? — No: pierde atomicidad (la auditoría debe ser 100% transaccional con la operación).
4. ¿Efectos secundarios ocultos? — Documentados: fila extra por escritura; `activity_logs` crece (retención pendiente); trigger corre dentro de la tx del escritor.
5. ¿Bloqueos potenciales? — Solo compite por el insert en `audit.activity_logs` (misma tx del escritor, sin locks adicionales).
6. ¿Impacto en transacciones? — Nulo adicional: el trigger solo hace un INSERT en la misma tx; si la app hace rollback, la auditoría también (deseado).
7. ¿Rendimiento? — Cero queries a catálogo, cero joins, cero dynamic SQL por fila: manipulación `jsonb` de OLD/NEW en memoria + 1 INSERT. Medido: ver `docs/modules/activity-log/performance.md`.
8. ¿Debugging? — Las filas aparecen en `audit.activity_logs` con `occurred_at`, `request_id` y `correlation_id` que correlacionan con los logs de la app.
9. ¿Comportamiento inesperado con EF Core? — Aceptado y documentado: `activity_logs` es de **solo lectura** para EF (nunca se inserta desde código); las entidades de negocio no ven la fila de auditoría (no se les añade navegación).

## Registro

| Trigger | Tabla | Evento | Qué hace | Por qué es necesario | Alternativas descartadas | Pruebas | Riesgos |
|---|---|---|---|---|---|---|---|
| `{tabla}_audit` (creado con `audit.attach_table_audit`) | cualquiera (schema `audit`) | AFTER INSERT OR UPDATE OR DELETE, FOR EACH ROW | Llama `audit.audit_trigger_function()`: serializa OLD/NEW a `jsonb`, calcula `changed_data` (solo columnas cambiadas), excluye columnas sensibles (TG_ARGV), lee el actor de GUC transaccionales (`audit.*`) y hace INSERT en `audit.activity_logs` | Auditoría completa, atómica, sin depender del código de la app | Audit en Application/Domain (incompleto), evento de dominio MediatR (incompleto), job background (pierde atomicidad) | `tests/CoppAddresd.IntegrationTests/AuditTriggerTests.cs`: CRUD, rollback (sin auditoría), no-leak de pooling, interceptor EF→GUC | Crecimiento de `activity_logs` (retención pendiente de decidir), coste por escritura (~µs), filas fuera del tracking de EF |

## Detalle del diseño

- Función genérica **sin argumentos declarados** (regla de PostgreSQL: las funciones trigger solo reciben `TG_ARGV`):
  - `TG_ARGV[0]` = columna PK (por defecto `id`).
  - `TG_ARGV[1..]` = columnas sensibles a **excluir** de los payloads jsonb (passwords, tokens, secrets). NUNCA se guardan.
- Actor (user_id, user_email, user_role, ip_address, request_id, correlation_id) llega por GUC **transaccionales**:
  `set_config('audit.*', ..., true)` — mueren con el commit/rollback → el connection pooling no filtra el actor de una request a otra.
  En .NET los propaga `Infrastructure/Persistence/AuditTriggerInterceptor.cs` (tras `BEGIN`).
- Sin Identity: `actor_type = SYSTEM`, `user_id = NULL`. Preparado para futuro: `user_id` es `uuid` nullable, sin FK, apunta a `AspNetUsers.Id`.
- Compatible AWS RDS: solo funciones plpgsql y `gen_random_uuid()` (built-in PG 13+); ninguna extensión extra.

## Añadir una tabla a auditoría (despliegue)

```sql
-- schema público (tablas de negocio) — la tabla debe existir ya
SELECT audit.attach_table_audit('public', 'mi_tabla', 'id', 'password_hash', 'api_key');
-- con PK 'id' y sin exclusiones:
SELECT audit.attach_table_audit('public', 'otra_tabla');
```

Se adjunta en una migración EF (ver `docs/modules/activity-log/`). Clasificación AUDIT_REQUIRED/OPTIONAL/EXCLUDED por tabla: `docs/modules/activity-log/database.md`.

## Resultado prueba end-to-end (2026-08-10)

Verificación real sobre PostgreSQL 18 local (BD `coppaddresd`, migración `InitialAuditSchema` aplicada). Evidencia completa en `docs/modules/activity-log/README.md`.

| Prueba | Resultado |
|---|---|
| INSERT → 1 log (old/changed NULL, new = fila) | PASS |
| UPDATE → 1 log (changed_data = solo columnas cambiadas) | PASS |
| DELETE → 1 log (old = fila, new NULL) | PASS |
| NULL → valor en changed_data | PASS |
| valor → NULL en changed_data | PASS |
| NULL → NULL NO genera cambio | PASS |
| ROLLBACK → 0 filas negocio y 0 auditoría | PASS |
| .NET (EF Core) → Npgsql → trigger → activity_logs | PASS |
| No duplicación (1 operación = 1 log) | PASS |
| Exclusión de secretos (old/new/changed) | PASS |
| Trigger asociado (pg_trigger: AFTER INSERT DELETE UPDATE, ROW, tgtype=29) | PASS |

Sin problemas encontrados en el mecanismo. Notas de diseño confirmadas: `updated_at = now()` sí aparece en `changed_data` porque su valor realmente cambió; `actor_type` por defecto `SYSTEM` sin GUC.
