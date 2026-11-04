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

## Asignación post-tests (paquete 2)

El submit usa su transacción clínica actual y bloquea la asignación para
serializar clientRequestId. Tras commit/dispose publica BatteryCompletedEvent.
El consumidor usa una transacción independiente Read Committed, dentro de la
estrategia Npgsql, y SELECT del perfil FOR UPDATE: comprueba batería requerida
completa y ausencia de Active/PendingAssignment, lee perfil/catálogo y guarda
enrollment+StreakState+semanas Locked+recommendation. No hace HTTP dentro del lock.
La inscripción manual toma el mismo lock y comprueba ambos estados. Error o
cancelación: rollback íntegro del programa, sin afectar el submit confirmado;
el consumidor registra Warning. Después del commit invalida scores-history y
encola solo el ID de recomendación. Narrativa se procesa en otro scope fuera de
esta transacción. Las pruebas contra PostgreSQL incluyen fallo después de crear
la inscripción, rollback y dos consumidores concurrentes sin duplicación.

## Aplicación/fallback de asignación y avisos (paquete 3)

ActivateAssignmentAsync usa Read Committed con estrategia Npgsql y orden de locks
perfil → inscripción → recomendación. Comprueba estado/plazo dentro del lock,
resuelve contenido disponible, hornea snapshot, actualiza semanas y estado,
persiste el aviso al paciente y auditoría semántica antes del commit. No hace
HTTP en esa transacción. Replays terminales no escriben ni notifican.
La creación de la recomendación guarda también el inbox profesional. El job
reutiliza la división relacional de completitud y el mismo comando de aplicación.
Recoveries usa INSERT ON CONFLICT condicional para claim/backoff durable. La
entrega usa UPDATE condicional con lease y progreso sticky por canal; FCM/chat
ocurren después del commit. Una caída entre envío y confirmación admite entrega
externa repetida (al menos una vez); la decisión y el inbox sí son idempotentes.
