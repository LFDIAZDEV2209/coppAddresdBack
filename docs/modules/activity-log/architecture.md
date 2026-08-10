# Activity Log — Arquitectura

## Objetivo

Registrar automáticamente toda escritura (INSERT/UPDATE/DELETE) en tablas de negocio, con contexto del actor, sin que el código de negocio participe. Auditoría completa aunque la escritura venga de SQL directo, jobs o consolas.

## Flujo

```text
.NET (Api/Job/Consola)
        │  escritura EF Core
        ▼
AuditTriggerInterceptor (Infrastructure)
        │  tras BEGIN: set_config('audit.*', ..., true)  → GUC transaccionales
        ▼
PostgreSQL — transacción del escritor
        │  INSERT/UPDATE/DELETE en tabla de negocio
        ▼
trigger {tabla}_audit (AFTER ... FOR EACH ROW)
        │  audit.audit_trigger_function() — TG_OP/TG_TABLE_*/OLD/NEW + GUC actor
        ▼
INSERT en audit.activity_logs  (misma transacción)
```

- Commit de la operación ⇒ commit de la auditoría. Rollback ⇒ sin auditoría. **Atomicidad garantizada por PostgreSQL.**
- El actor viaja por GUC con `is_local=true`: viven solo en la transacción y mueren con ella → el connection pooling **nunca** filtra el actor de un request a otro (probado en test `ConexionReutilizada_NoFiltraActorDeTransaccionAnterior`).

## Arquitectura hexagonal

| Capa | Qué contiene | Dependencias |
|---|---|---|
| Domain | `ActivityLog` (entidad de solo lectura), `AuditAction`, `AuditActorType` | ninguna |
| Application | `IAuditActorContext` (contrato del actor: actor_type, user_id, email, role, ip, request_id, correlation_id) | Domain |
| Infrastructure | `AppDbContext`, `ActivityLogConfiguration`, `AuditTriggerInterceptor` (propaga GUCs), `HttpAuditActorContext` (lee HttpContext + Activity), `AppDbContextFactory` (design-time), DI | Domain, Application |
| Api | Registra `AddInfrastructure(configuration)`; connection string en `appsettings.json` (gitignored) o `ConnectionStrings__DefaultConnection` | Application, Infrastructure |

- **Domain no conoce la BD**: el trigger es invisible para la entidad.
- **Application no tiene SQL**: solo el contrato `IAuditActorContext`.
- **Todo lo PostgreSQL vive en Infrastructure** (configuración EF + interceptor). El SQL del trigger vive en la migración (`Migrations/*_InitialAuditSchema.cs`).
- Cuando exista Identity, `HttpAuditActorContext` mapeará los claims del usuario autenticado (`AuditActorType.User` + `UserId`); hoy: `System` + `null`.

## Decisiones de diseño (ADR)

| Decisión | Motivo | Alternativa descartada |
|---|---|---|
| Trigger AFTER ... FOR EACH ROW | Ve OLD/NEW completos; es el único punto que ve TODA escritura | BEFORE (corre igual; AFTER es el estándar de auditoría) |
| Función genérica con `TG_ARGV` (PK + columnas sensibles) | Una sola función para todas las tablas; sin catálogo ni dynamic SQL por fila | Función por tabla (duplicación), catálogo `information_schema` en cada write (lento) |
| GUC transaccionales para el actor | Sin tablas auxiliares por request, sin joins en el trigger, sin leak de pooling | `tmp_table` por sesión (filtra entre conexiones), columna en cada tabla (invasivo) |
| `record_id` como `text` | PK puede ser uuid/int/compuesta; el trigger serializa `to_jsonb` del valor | Tipo por tabla (rompe genericidad) |
| Exclusión de columnas sensibles vía `TG_ARGV[1..]` | NUNCA persisten secrets en jsonb | Confiar en que el código no escriba secretos |

## Cambios futuros previstos

- Identity: `user_id` ← `AspNetUsers.Id` (sin FK aún, por diseño); el interceptor leerá el token/claims.
- Endpoints de consulta del activity log (solo lectura, `AsNoTracking`).
- Retención/archivo de `activity_logs` (ver `performance.md`).
