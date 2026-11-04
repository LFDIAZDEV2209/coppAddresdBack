# Transacciones — Registro

Reglas del proyecto (skill `transactions`). Toda operación con transacción explícita se registra aquí.

## Registro

| Flujo | Qué valida | Qué escribe | Aislamiento | Duración esperada | Error | Cancelación | Doble ejecución |
|---|---|---|---|---|---|---|---|
| Auth `UserService.CreateAsync` | Email no duplicado (antes de la tx); existencia de cada rol (RoleManager) y permiso (query EF) pedidos | `auth.users` (Identity), `auth.user_roles`, `auth.user_permissions` | Read Committed | < 100 ms | Rollback total + error único (primer rol/permiso inválido o fallo de asignación); el `await using` deshace si no se commitea | Rollback en catch (o dispose) — no deja usuario ni asignaciones | Email duplicado → 23505 / mensaje amigable pre-tx; reintentar es seguro |
| Auth `UserService.UpdateAsync` | Usuario existe; si `RoleIds`/`PermissionIds` vienen (no null): existencia de cada rol/permiso | Perfil (`auth.users`), sync total de `auth.user_roles` y `auth.user_permissions`, security stamp si hubo cambios (o desactivación) | Read Committed | < 100 ms | Rollback total + error único | Rollback en catch (o dispose) — no deja sync a medias | Idempotente: sync total contra el estado actual; reintentar converge |

| Nutrición `ApproveAsync` (individual/masiva) | Alcance por profesional, revisión exacta, baterías requeridas completas bajo lock de pacientes, menú completo y ciclo vigente | Plan aprobado, asignación de 30 días, cierre de asignaciones previas, evento con actor y snapshot | Read Committed + `FOR UPDATE` de pacientes en orden UUID | Una operación SQL por grupo de datos; lote máximo 100 pacientes, sin red externa | Cualquier conflicto/validación revierte todo el lote; la estrategia recarga entidades nuevas en cada reintento | Token propagado a locks, queries, SaveChanges y commit; dispose revierte | Revisión optimista y exclusión de ciclo vigente: la segunda aprobación no duplica el ciclo |
| Nutrición `UpdateNutritionPlan` | Plan existente y revisión cargada por el editor | Reemplazo opcional de comidas, cambio a pending_review, revocación de aprobación y evento de modificación | Read Committed + revisión optimista | Solo base de datos; sin proveedor IA | Rollback de comidas y metadatos juntos ante error/409 | Token propagado en toda la unidad | El request con revisión anterior obtiene409 |

## Reglas permanentes

- Una sola escritura atómica → **no** necesita transacción explícita.
- 2+ escrituras todo-o-nada → `BeginTransactionAsync` + `CommitAsync`, `await using`, rollback en catch.
- **Nunca** dentro de la transacción: HTTP externo, email, S3, SQS, I/O de usuario (locks retenidos).
- Aislamiento default `Read Committed`; `Repeatable Read`/`Serializable` solo con justificación + retry por `40001`.
- Duración objetivo < ~100ms; transacciones largas = locks = contención/deadlocks.
- Retry con backoff en `40001` (serialization) / `40P01` (deadlock), nueva transacción + datos re-leídos.
- Publicación de eventos post-commit: patrón outbox si se necesita entrega garantizada (a decidir cuando exista el primer caso).
