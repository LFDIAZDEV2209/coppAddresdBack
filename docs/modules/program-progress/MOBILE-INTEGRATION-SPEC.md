# Especificación — Integración Móvil del Progreso del Programa

> Contrato ejecutable entre `antares-paciente` y los endpoints `/api/v1/program/*` de `coppAddresdBack`.
> Define QUÉ debe hacer la app y QUÉ no debe asumir. El backend es la fuente de verdad de rutas, DTOs y
> estados (SPEC §7); los hechos verificados contra código (MOBILE-INTEGRATION-EXPLORATION.md) corrigen
> suposiciones previas (MOBILE-INTEGRATION.md §2.3). Palabras clave RFC 2119 (MUST/SHALL/SHOULD/MAY);
> escenarios DADO/CUANDO/ENTONCES. Migraciones asumidas aplicadas: esta fase no agrega trabajo de BD.

---

## R1 — Cliente API autenticado

| # | Requisito |
|---|-----------|
| R1.1 | La app DEBE enviar el access token en `Authorization: Bearer` en cada request a `/api/v1/program/*`, leído de `sessionStorage` (`copp_access_token`). |
| R1.2 | Ante `401`, DEBE ejecutar refresh **single-flight** vía `POST /api/auth/refresh` con `credentials: 'include'` (cookie HttpOnly `copp_refresh_token`) y reintentar la petición original **una vez**. Si el refresh falla, DEBE limpiar la sesión y redirigir a login (sin bucle de reintentos). |
| R1.3 | Timeout por request: 15 s (SHOULD, configurable). Timeout = `ApiError` tipo `TIMEOUT`, tratado como fallo de red. |
| R1.4 | La app DEBE mapear RFC 7807/ProblemDetails a `ApiError` con `status`, `title`, `detail` y `code` (cuando exista). La UX DEBE decidir por `code`; el `detail` crudo del servidor NO se muestra al paciente. |

**Escenarios**
- DADO token válido y backend disponible / CUANDO se ejecuta `GET /me/snapshot` / ENTONCES `200` y respuesta parseada sin error.
- DADO access token expirado y cookie de refresh válida / CUANDO una petición devuelve `401` / ENTONCES se refresca una sola vez, se reintenta la petición y la UI no muestra error.
- DADO refresh fallido (cookie inválida/expirada) / CUANDO el refresh devuelve `401` / ENTONCES se limpia la sesión, se redirige a login y no hay más reintentos.
- DADO backend lento (> 15 s) / CUANDO una query excede el timeout / ENTONCES `ApiError TIMEOUT` y la query cae al fallback de R5.

## R2 — Contratos de endpoints (backend autoritativo)

| Endpoint | Request | Response (clave) | Errores a manejar |
|----------|---------|------------------|-------------------|
| `POST /api/v1/program/enrollments/me` | `{ patientId: null, templateId?, timezone (IANA, requerido), startLocalDate? }` | `ProgramEnrollmentDto` | `404` sin perfil, `409` ya activo, `422` sin plantilla |
| `GET /api/v1/program/me/snapshot` | — (resuelto del JWT) | `ProgramSnapshotDto` | `404 NO_ACTIVE_ENROLLMENT`, `503` → cache stale |
| `POST /api/v1/program/tasks/complete` | `{ enrollmentId, localDate, taskCode, clientRequestId, clientCompletedAt?, moodScore?, barriers?, contentFingerprint? }` | `CompleteTaskResponseDto` | `409 ENROLLMENT_INACTIVE` / `TASK_NOT_SCHEDULED` / `IDEMPOTENCY_KEY_REUSED`; `422 CONTENT_FINGERPRINT_MISMATCH` / `DATE_OUTSIDE_ACTIVE_WEEK` |
| `GET /api/v1/program/calendar?from=&to=` | rango ≤ 92 días, fechas locales | `{ days[], summary }` | `404` |
| `GET /api/v1/program/path` | — | `{ weeks[] }` | `404` |
| `GET /api/v1/program/scores` | — | `ScoresResponseDto` (+ headers `X-Score-Stale` / `X-Score-Recalculated`) | `404` |
| `POST /api/v1/program/nutrition/log` | `{ mealCode: des\|alm\|mer\|cen\|agua, localDate? }` | `NutritionLogResultDto` | `409 HABIT_ALREADY_LOGGED` |

| # | Requisito |
|---|-----------|
| R2.1 | La app DEBE renderizar el snapshot con `todayTasks[]` (taskCode/title/short/points/status/content), `xp` (balance/level/nextLevelAt), `streak` (current/longest/freezesRemaining/multiplierActive/multiplierEndsAt/multiplierRemainingHours), `todayPoints`, `todayPointsMax`, `nextMilestoneDays` y `template.currentWeekNumber`. |
| R2.2 | `tasks/complete` DEBE enviar `enrollmentId` y `todayLocalDate` tomados del snapshot (nunca de input del usuario) y `taskCode` del catálogo (`podcast`\|`vitals`\|`nut`\|`ejercicio`\|`nutraceutico`\|`emocional`); `clientRequestId` es obligatorio. |
| R2.3 | `calendar` DEBE solicitar un rango ≤ 92 días; `StreakView` usa `days[].isPerfectDay` para el mapa de consistencia. |
| R2.4 | `path` DEBE renderizar nodos por estado `Locked`/`Active`/`Completed` y `isPerfectWeek`. |
| R2.5 | `scores` DEBE alimentar `EvolutionView` (Health + Transformation con breakdown por pilar); la UI DEBE etiquetarlos como "Índice" (no XP) y mostrar la fecha de cálculo. |
| R2.6 | `nutrition/log` DEBE permitir una comida/hidratación por código y día; duplicado → `409` y la UI refleja el estado ya registrado sin XP nueva. |

**Escenarios**
- DADO paciente con inscripción activa / CUANDO `GET /me/snapshot` / ENTONCES `200` y las vistas renderizan datos del server (sin mocks).
- DADO tarea `podcast` del día / CUANDO `POST tasks/complete` con `clientRequestId` nuevo / ENTONCES `200` con `pointsAwarded = 80` y `xpBalanceAfter` incrementado.
- DADO `des` ya registrado hoy / CUANDO `POST nutrition/log` con `mealCode: "des"` / ENTONCES `409 HABIT_ALREADY_LOGGED` y la UI muestra la comida como ya logueada.
- DADO rango de 31 días válido / CUANDO `GET calendar` / ENTONCES `200` con `days[]` y `summary`; la vista de consistencia usa `isPerfectDay` por día.

## R3 — Idempotencia y reintentos (`clientRequestId`)

| # | Requisito |
|---|-----------|
| R3.1 | La app DEBE generar `clientRequestId` (ULID/UUID) **una vez por acción** de completación y REUSAR la misma clave en los reintentos de esa misma acción. |
| R3.2 | Replay con la misma clave → `200` con el cuerpo existente y **0 XP nueva**: la app DEBE reconciliar la respuesta sin animar doble recompensa (confeti/XP popup solo en la primera confirmación). |
| R3.3 | Ante `409 IDEMPOTENCY_KEY_REUSED` (misma clave, distinta fecha/tarea), DEBE generar una clave nueva y reintentar una vez; si persiste, no reintentar más y reconciliar. |
| R3.4 | Ante `409 TASK_NOT_SCHEDULED` o `422 DATE_OUTSIDE_ACTIVE_WEEK`, la app DEBE invalidar el snapshot (la tarea ya no aplica) y NO reintentar. |

**Escenarios**
- DADO fallo de red tras enviar `completeTask` con clave K / CUANDO se reintenta con la misma K / ENTONCES `200` con el body original y sin XP duplicada.
- DADO clave K usada para `podcast` de hoy / CUANDO se envía K para `nut` de hoy / ENTONCES `409 IDEMPOTENCY_KEY_REUSED`; la app genera K2 y reintenta `nut`.
- DADO tarea no programada para hoy / CUANDO `POST tasks/complete` / ENTONCES `409 TASK_NOT_SCHEDULED`; sin reintentos; snapshot invalidado.

## R4 — Estados de inscripción y auto-inscripción

| # | Requisito |
|---|-----------|
| R4.1 | La app DEBE auto-inscribir (`POST /enrollments/me` con `timezone` IANA del dispositivo) **solo** cuando el snapshot devuelve `404 NO_ACTIVE_ENROLLMENT` y el paciente no está `Withdrawn`; luego refetch del snapshot. |
| R4.2 | Un `409` en la auto-inscripción (inscripción activa existente) NO es error: la app DEBE refetchear el snapshot y continuar. |
| R4.3 | Sin plantilla publicada → `422`: la app DEBE mostrar "Tu programa no está listo aún. Contacta a tu equipo de salud." y NO reintentar en el mismo montaje. |
| R4.4 | `Paused`: snapshot `200` con estado pausado → UI "Tu programa está pausado"; las mutaciones reciben `409 ENROLLMENT_INACTIVE` → la app DEBE invalidar el snapshot, mostrar el estado pausado y NO encolar la mutación. |
| R4.5 | El paciente NUNCA ve el concepto "inscripción": cada estado se traduce a un mensaje de producto. |

**Escenarios**
- DADO paciente sin inscripción y plantilla publicada / CUANDO snapshot `404` → `POST enrollments/me` `201` → refetch / ENTONCES snapshot `200` y UI normal.
- DADO carrera: snapshot `404` y `POST enrollments/me` devuelve `409` / CUANDO la app auto-inscribe / ENTONCES ignora el `409`, refetchea y renderiza.
- DADO inscripción `Withdrawn` / CUANDO snapshot `404` / ENTONCES la app NO llama `enrollments/me`; muestra "Tu programa no está disponible. Contacta a tu equipo de salud."
- DADO inscripción `Paused` / CUANDO la app completa una tarea / ENTONCES `409 ENROLLMENT_INACTIVE`; la app muestra el estado pausado y descarta la acción (sin cola).

## R5 — Queries, caché y fallback

| # | Requisito |
|---|-----------|
| R5.1 | TanStack Query: `staleTime: 5 min`, `retry: 2`, `refetchOnWindowFocus: true` (SHOULD, ajustable). |
| R5.2 | Queries: datos del servidor cuando están disponibles; ante error de red/5xx → cache stale; sin cache → mock fallback (marcado `// TODO: Remove mock fallback`). **NUNCA** toast de error en queries. |
| R5.3 | Mutaciones: update optimista en `onMutate` (confeti/XP popup se disparan ahí, no en `onSuccess`); rollback al cache previo en `onError`; `invalidateQueries` del snapshot en `onSettled`. |
| R5.4 | Falla de red en mutación: toast "Sin conexión. Tu progreso se sincronizará cuando vuelvas a estar en línea." + cola en `localStorage` keyed por `clientRequestId` (máx. 50 entradas; descarte FIFO con advertencia). |
| R5.5 | Reconexión: replay de la cola en orden con la misma clave; la idempotencia del server previene doble premio; al terminar, invalidar todas las queries del programa. |
| R5.6 | Errores 4xx de negocio en mutaciones (409/422): NO se encolan; la app muestra mensaje por `code` y reconcilia la cache. |

**Escenarios**
- DADO red caída y cache stale del snapshot / CUANDO se abre la pestaña Hoy / ENTONCES renderiza cache stale; sin toast; sin mock si hay cache.
- DADO red caída y sin cache / CUANDO se abre Hoy / ENTONCES renderiza mock fallback (marcado TODO) sin error visible.
- DADO `completeTask` falla por red / CUANDO `onError` / ENTONCES rollback al cache previo + toast + entrada en la cola de `localStorage`.
- DADO reconexión con 2 tareas encoladas / CUANDO se flushea la cola en orden / ENTONCES ambas devuelven `200` (replay) y el snapshot se invalida.

## R6 — Criterios de aceptación por vista

| Vista | Hooks | Criterio de aceptación |
|-------|-------|------------------------|
| `ProgramPage` (header) | `useProgram()` | Racha, `pointsTotal` y `programWeek` vienen del snapshot; `AppContext` queda solo como wrapper legacy con `// TODO: Remove after full migration`. |
| `TodayView` | `useProgramPath()` + `useCompleteTask()` | Nodos `locked/active/completed` del path; completar dispara mutation optimista con rollback; confeti/popup en `onMutate`. |
| `StreakView` | `useProgram()` + `useProgramCalendar()` | Racha del snapshot y mapa del calendar; mock solo como fallback. |
| `EvolutionView` | `useProgramScores()` | Pilares y filas del endpoint `scores`; etiquetado "Índice de Salud / Transformación" con fecha de cálculo. |
| `Lessons` | `useCompleteTask()` / `useNutritionLog()` | Cada lección completa una tarea con su `taskCode`; comidas/agua vía `nutrition/log`. |

| # | Requisito |
|---|-----------|
| R6.1 | DoD por vista: renderiza datos del backend cuando la API está disponible; cae a mock sin romper UX; las mutaciones persisten y reconcilian la cache; sin regresión visual; `npm run build` y `npm run lint` pasan; todo fallback marcado con TODO. |

**Escenarios**
- DADO backend disponible / CUANDO se navega a cada vista / ENTONCES cada vista renderiza datos del server (verificable por request real, sin valores mock).
- DADO backend caído / CUANDO se navega a cada vista / ENTONCES la vista renderiza fallback (cache o mock) sin error fatal ni regresión visual.

## R7 — Compatibilidad y seguridad

| # | Requisito |
|---|-----------|
| R7.1 | Campos aditivos null-safe: el móvil DEBE tratar como opcionales los campos nuevos del snapshot (`streakMinTasks`, `essentialTaskCodes`, `nbStreak`, `nbLongestStreak`, `nbNextMilestone`, `multiplier*`) y DEBE ignorar campos desconocidos al parsear (nunca fallar). |
| R7.2 | Anti-IDOR: las lecturas (`snapshot`, `calendar`, `path`, `scores`) DEBEN enviarse sin ids de paciente/inscripción (el backend resuelve del JWT); `tasks/complete` y `nutrition/log` usan ids del snapshot. Un `404` en lecturas se trata como "sin acceso o sin inscripción". |
| R7.3 | La app NUNCA DEBE llamar al `ai-service` directamente; todo tráfico de IA pasa por el backend (proxy autenticado). |
| R7.4 | El backend es la fuente de verdad: los tipos TypeScript DEBEN reflejar SPEC §7 y no inventar campos; la app SHALL NOT asumir que la inscripción devuelve la existente ante un `409` (la auto-inscripción solo crea cuando no hay inscripción activa). |

**Escenarios**
- DADO snapshot con campos nuevos no tipados / CUANDO se parsea / ENTONCES los campos conocidos renderizan y los desconocidos se ignoran (sin crash).
- DADO paciente intenta acceder a datos de otro vía ids manipulados / CUANDO `GET snapshot` / ENTONCES `404` (el backend resuelve del JWT); la app muestra "sin programa", nunca datos ajenos.

---

## Fuera de alcance (P2+)

Notificaciones gamificadas (`/notifications`), debilidades (`/weaknesses`), intervenciones (`/interventions`), migración a Capacitor Secure Storage, push FCM, cola offline con sync en background, i18n EN. Los endpoints existen; su consumo móvil requiere diseño/UI posterior.