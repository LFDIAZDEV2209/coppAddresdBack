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

## Nutrición: fundamentos legacy y prescripciones (grupos 1–2)

`UpdateNutritionPlanCommandHandler` recarga y valida el plan dentro de
`ExecuteInTransactionAsync`; elimina días, añade reemplazos y actualiza
plan/revisión/evento en una transacción Read Committed con estrategia Npgsql.
Se propaga el token recibido por la operación a todas las llamadas. Un error
limpia el tracking; `await using` revierte también si el token está cancelado.
Un reintento recarga entidades y revisa la versión original. No hay llamadas
externas dentro de la transacción. Objetivo: duración típica inferior a 100 ms.

`NutritionPrescriptionRepository` persiste cada agregado nuevo o actualizado en
un solo `SaveChangesAsync`, usando la transacción automática de EF: cabecera,
comidas e ítems se confirman o revierten juntos. La revisión observada participa
en el WHERE del UPDATE y una escritura concurrente produce
`DbUpdateConcurrencyException`. El caso de uso futuro traducirá ese conflicto.
El contexto de una escritura fallida se descarta al finalizar el alcance.
La integración contra PostgreSQL verifica fallo de FK tras el borrado legacy,
fallo de FK en un agregado nuevo y rollback de una revisión obsoleta.

### Ola 2: decisiones profesionales y bitácora

`NutritionCaseStore.ExecuteAsync` ejecuta en Read Committed con estrategia Npgsql
y lock de `app.patient_profiles` por paciente. Cada reintento limpia tracking,
recarga alcance/revisión y confirma versión, comidas, ítems, reemplazo de la
publicación y evento con snapshot juntos. Objetivo: transacción corta, sin IO
externo; el cálculo y la llamada a IA terminan antes de abrirla. Fallo/cancelación
revierte por dispose. Los fallos de revisión se traducen a conflicto de negocio.
La integración inyecta fallo en bitácora tras el guardado del agregado y verifica
que no persisten cabecera, hijos ni eventos.

### Nutrición Ola 6: reserva, lease y generación

Cada escritura de reserva/claim/resultado/fallo usa una transacción corta Read
Committed con estrategia Npgsql y token; BEGIN instala GUC del actor y COMMIT
los elimina. INSERT ON CONFLICT serializa replay por paciente/batería. Claim usa
un UPDATE con subquery FOR UPDATE SKIP LOCKED, sin espera entre réplicas. El
claim token impide que un worker antiguo confirme una lease tomada por otro.
IA/cálculo ocurren fuera de esas transacciones. Generate conserva la transacción
del agregado con lock del paciente; reutiliza el ID de reserva al reejecutar.
El commit de agregado precede la confirmación ready: tras un corte, el retry
relee el plan existente, no agrega otra versión ni otro evento. Cancelación
deja la lease recuperable; cinco intentos agotados quedan failed y visibles.
