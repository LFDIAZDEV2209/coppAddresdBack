# Módulo: Activity Log

Auditoría automática de escrituras (INSERT/UPDATE/DELETE) mediante **triggers PostgreSQL**, 100% transaccional con la operación original.

## Documentos

- [architecture.md](architecture.md) — diseño, flujo, decisiones, hexagonal.
- [database.md](database.md) — tabla, trigger, índice, clasificación AUDIT_*, futuro Identity.
- [performance.md](performance.md) — coste por escritura, pooling, retención, riesgos.

## Estado

- Base de datos `coppaddresd` (UTF8) + usuario `app_user` creados (script `scripts/database/01-create-database.sql`).
- Migración `InitialAuditSchema`: schema `audit`, tabla `activity_logs`, función trigger, helper de adjunta, grants.
- Trigger activo: **ninguna tabla de negocio existe aún** — la maquinaria está lista y probada (tests de integración); se adjunta a cada tabla de negocio con `audit.attach_table_audit` al crearla.
- Identity: NO implementado (fuera de alcance); `user_id` preparado sin FK.

## Prueba end-to-end (2026-08-10) — PostgreSQL 18 real

Tabla temporal `audit_test_customers` (public) con trigger vía `audit.attach_table_audit('public', 'audit_test_customers', 'id', 'secret_value')`. Resultado: **10/10 PASS**, sin problemas. Evidencia real:

```
INSERT | audit_test_customers | old: NULL
       | new: {"id": "...", "name": "Carlos Test", "email": "carlos.test@example.com", "status": "ACTIVE", ...}
       | changed: NULL

UPDATE | audit_test_customers | old: {"name": "Carlos Test", "status": "ACTIVE", ...}
       | new: {"name": "Carlos Updated", "status": "INACTIVE", ...}
       | changed: {"name": "Carlos Updated", "status": "INACTIVE", "updated_at": "..."}   ← solo lo que cambió

DELETE | audit_test_customers | old: fila completa | new: NULL

NULL → valor:   changed {"name": "Con Valor"}
valor → NULL:  changed {"name": null}
NULL → NULL:   changed NULL (sin cambio) ✓
ROLLBACK: 0 filas negocio + 0 auditoría ✓
secret_value: NUNCA en old/new/changed ✓
Conteo por acción: INSERT 1, UPDATE 1, DELETE 1 (sin duplicación) ✓
pg_trigger: AFTER INSERT DELETE UPDATE, ROW, tgtype=29 ✓
```

Prueba .NET permanente: `tests/CoppAddresd.IntegrationTests/ActivityLogE2ETests.cs` (EF Core → Npgsql → trigger → activity_logs, sin tocar ActivityLog desde código). Suite total: 7/7 (4 `AuditTriggerTests` + 3 `ActivityLogE2ETests`).
