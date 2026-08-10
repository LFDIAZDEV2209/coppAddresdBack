# Activity Log — Rendimiento

## Coste por escritura (target)

El trigger agrega a cada INSERT/UPDATE/DELETE auditado:

1. Serialización `to_jsonb(OLD/NEW)` (en memoria, sin I/O).
2. Para UPDATE: diff `jsonb_object_agg` sobre columnas (O(n) con n = nº de columnas; microsegundos).
3. Un `INSERT` en `audit.activity_logs` + mantenimiento de 2 índices.

**Cero** queries adicionales, **cero** acceso a catálogo, **cero** dynamic SQL por fila. El coste es proporcional al ancho de la fila y al nº de índices de `activity_logs`, no a la carga.

Pendiente de medición con volumen representativo (miles de filas) — ver `docs/database/query-performance.md` antes de producción.

## Connection pooling (leak de actor)

- El actor se propaga con `set_config(..., is_local := true)`: la GUC vive solo en la transacción y muere en commit/rollback.
- Protege el escenario "Usuario A filtra contexto al Usuario B" en conexiones reutilizadas. Probado por el test de integración `ConexionReutilizada_NoFiltraActorDeTransaccionAnterior` (tx1 con actor USER → tx2 sin actor → `SYSTEM`).
- El interceptor `AuditTriggerInterceptor` además **siempre** escribe las 7 GUCs (valores o NULL) al inicio de cada transacción, eliminando cualquier estado residual por diseño.

## Crecimiento y retención

- `activity_logs` crece 1 fila por escritura auditada — es la tabla de mayor volumen proyectado del sistema.
- Sin retención definida aún (decisión pendiente: archivo frío vs borrado por rango con chunking; ver `docs/database/migrations.md` sobre UPDATE/DELETE masivos).
- Los índices `occurred_at` y `(table_name, record_id)` soportan las consultas de auditoría; no se agregarán índices sin query real que los justifique.

## Consideraciones

- Tablas de altísimo INSERT: evaluar clasificar como AUDIT_OPTIONAL (decidir coste vs trazabilidad).
- Columnas sensibles se excluyen vía `TG_ARGV`; nunca se serializan (también regla de seguridad: `docs/security`).
- Concurrencia: el trigger no introduce contención nueva relevante (insert secuencial en tabla propia, 2 índices).
