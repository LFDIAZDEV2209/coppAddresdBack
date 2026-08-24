# Transacciones — Registro

Reglas del proyecto (skill `transactions`). Toda operación con transacción explícita se registra aquí.

## Registro

| Flujo | Qué valida | Qué escribe | Aislamiento | Duración esperada | Error | Cancelación | Doble ejecución |
|---|---|---|---|---|---|---|---|
| Auth `UserService.CreateAsync` | Email no duplicado (antes de la tx); existencia de cada rol (RoleManager) y permiso (query EF) pedidos | `auth.users` (Identity), `auth.user_roles`, `auth.user_permissions` | Read Committed | < 100 ms | Rollback total + error único (primer rol/permiso inválido o fallo de asignación); el `await using` deshace si no se commitea | Rollback en catch (o dispose) — no deja usuario ni asignaciones | Email duplicado → 23505 / mensaje amigable pre-tx; reintentar es seguro |
| Auth `UserService.UpdateAsync` | Usuario existe; si `RoleIds`/`PermissionIds` vienen (no null): existencia de cada rol/permiso | Perfil (`auth.users`), sync total de `auth.user_roles` y `auth.user_permissions`, security stamp si hubo cambios (o desactivación) | Read Committed | < 100 ms | Rollback total + error único | Rollback en catch (o dispose) — no deja sync a medias | Idempotente: sync total contra el estado actual; reintentar converge |

_(Vacío por esqueleto.)_

## Reglas permanentes

- Una sola escritura atómica → **no** necesita transacción explícita.
- 2+ escrituras todo-o-nada → `BeginTransactionAsync` + `CommitAsync`, `await using`, rollback en catch.
- **Nunca** dentro de la transacción: HTTP externo, email, S3, SQS, I/O de usuario (locks retenidos).
- Aislamiento default `Read Committed`; `Repeatable Read`/`Serializable` solo con justificación + retry por `40001`.
- Duración objetivo < ~100ms; transacciones largas = locks = contención/deadlocks.
- Retry con backoff en `40001` (serialization) / `40P01` (deadlock), nueva transacción + datos re-leídos.
- Publicación de eventos post-commit: patrón outbox si se necesita entrega garantizada (a decidir cuando exista el primer caso).
