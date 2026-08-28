# TASKS — Integración Móvil del Progreso del Programa

> Plan de implementación ejecutable para conectar `antares-paciente` a los endpoints
> `/api/v1/program/*` de `coppAddresdBack`. Contrato: [`MOBILE-INTEGRATION-SPEC.md`](./MOBILE-INTEGRATION-SPEC.md)
> (requisitos R1–R7); decisiones y gates: [`MOBILE-INTEGRATION-DESIGN.md`](./MOBILE-INTEGRATION-DESIGN.md);
> hechos verificados: [`MOBILE-INTEGRATION-EXPLORATION.md`](./MOBILE-INTEGRATION-EXPLORATION.md).
> Este archivo es MOBILE-INTEGRATION-focused y NO reemplaza a `TASKS.md` (backlog backend).
> Repo objetivo de implementación: `antares-paciente/`. Migraciones asumidas aplicadas.

---

## Review Workload Forecast

| Campo | Valor |
|-------|-------|
| Líneas cambiadas estimadas | ~1.500–1.800 (móvil: tipos, servicios, hooks, UI, tests; 0 backend) |
| Presupuesto activo de la sesión | **800 líneas** (no 400: decisión de sesión) |
| Riesgo sobre 400 líneas | **High** |
| Riesgo sobre 800 líneas | **High** (requiere 2 slices de working-tree) |
| PRs encadenados recomendados | Sí (como fronteras de revisión coordinación) |
| Entrega | **Una sola entrega en working tree — SIN commits ni PRs** (decisión explícita del usuario) |
| Estrategia de cadena | `pending` (no aplica: sin PRs; se fijaría solo si el equipo pidiera PRs después) |

```text
Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: pending
400-line budget risk: High
```

**Nota de presupuesto**: con el presupuesto activo de 800 líneas, la implementación se parte en
**2 slices de working tree** (ver Work Units). Apply permanece working-tree-only; los gates G1/G2/G5
se resuelven ANTES del rollout, no bloquean el apply de código con manejo dual (G1/G2) ni de
infraestructura (G5 usa flag por env var por defecto).

### Suggested Work Units (fronteras de revisión, sin PRs)

| Unit | Alcance | Frontera de revisión | Verificación de salida |
|------|---------|----------------------|------------------------|
| 1 | Gates G1/G2/G5 + Fundamentos + Servicios + Hooks de lectura (~700 líneas) | Slice 1 del working tree | `npm run build` + `npm run lint` + smoke snapshot/enroll |
| 2 | Hooks de mutación + cola offline + puente AppContext + wiring UI (~900 líneas) | Slice 2 del working tree | `npm run build` + `npm run lint` + smoke completo R2–R6 |
| 3 | Verificación final + gates P2 (G3/G4) + este doc | Revisión final | Smoke degradado + grep anti ai-service |

---

## Gates de contrato (Fase 0 — validar/decidir antes de implementar o rollout)

| Gate | Decisión | Bloquea | Detalle |
|------|----------|---------|---------|
| G1 | ¿Cómo distingue el backend `Paused`/`Withdrawn` de `NO_ACTIVE_ENROLLMENT`? Verificar `ProgramRepository.ResolveActiveEnrollmentIdAsync` + `ProgramSnapshotDto` en `coppAddresdBack/src/CoppAddresd.Infrastructure/Repositories/ProgramRepository.cs` y `ProgramProgressDtos.cs`. Cliente reconoce códigos explícitos futuros (`ENROLLMENT_PAUSED`/`ENROLLMENT_WITHDRAWN`) sin inventar campos wire; NO auto-inscribir a ciegas ante 404 hasta confirmar el contrato. | **ROLLOUT** (apply avanza con reconocimiento dual) | Design §gates; SPEC R4.1/R4.4 |
| G2 | Alinear `TEMPLATE_NOT_ACTIVE` actual (409) con el `422` del contrato. El cliente maneja AMBOS sin retry ("Tu programa no está listo aún. Contacta a tu equipo de salud."). Decidir el código objetivo de aceptación. | **ROLLOUT** | Design §gates; SPEC R4.3 |
| G3 | `calculatedAt` de scores: el DTO actual no lo expone. NO inventar el campo TS; mostrar frescura solo con header `X-Score-Stale`. ¿Endpoint/campo backend futuro? | P2 | Design §gates; SPEC R2.5 |
| G4 | Refresh cookie en Capacitor nativo HTTPS (cookie `Path=/api/auth`, `SameSite=Lax`): validar en build nativa real; en dev solo cubre el proxy de Vite. | P2 | Design §gates; SPEC R1.2 |
| G5 | Feature flag: `VITE_PROGRAM_API_ENABLED` (build-time env var) vs configuración remota. Implementar por env var; decidir el mecanismo de producción. | **ROLLOUT** | Design §Feature safety |
| G7 | Test runner: `antares-paciente` NO tiene runner de tests (sin vitest/jest/MSW). Decidir si se introduce Vitest como devDependency para unit tests de parser/cola/hooks, o se verifica solo con `build`/`lint`/smoke. | P2 (no bloquea; no inventar tooling ausente) | Ver Fase 5 |

---

## Fase 1: Fundamentos e infraestructura

- [ ] 1.1 `antares-paciente/package.json` — agregar `@tanstack/react-query` (v5) a dependencies — verificación: `npm install` sin errores — dep: — paralelo: —
- [ ] 1.2 `antares-paciente/src/App.tsx` — envolver `IonApp` con `QueryClientProvider` + `QueryClient` estable; defaults R5.1: `staleTime: 5min`, `retry: 2`, `refetchOnWindowFocus: true`, `networkMode: 'online'` — aceptación: build limpio; query keys activas — dep: 1.1 — paralelo: —
- [ ] 1.3 `antares-paciente/src/utils/apiClient.ts` (nuevo) — `apiFetch<T>(path, opts)`: Bearer desde `getAccessToken()` de `authApi.ts`, `credentials: 'include'`, timeout 15s con `AbortController` (→ `ApiError` tipo `TIMEOUT`/`network`), refresh single-flight en 401 (`POST /api/auth/refresh`, sin Bearer, sin retry; todas las requests concurrentes esperan el mismo `refreshPromise` y reintentan la original 1 vez), `ApiError` RFC 7807 (`status/title/detail/code/correlationId/errors` + fallback `{message}` + prefijo `NO_ACTIVE_ENROLLMENT:`) — aceptación: escenarios R1.1–R1.4; 204 aceptado; sin log de tokens — dep: 1.1 — paralelo: —
- [ ] 1.4 `antares-paciente/vite.config.ts` — mantener proxy `/api/auth → http://localhost:5123` y agregar `/api/v1 → http://localhost:5122` — aceptación: Network tab muestra 200 a ambos puertos — dep: — paralelo: sí (con 1.1)
- [ ] 1.5 `antares-paciente/src/services/program/types.ts` (nuevo) — interfaces wire literales: `ProgramSnapshotDto`, `TodayTaskDto`, `TodayTaskContentDto`, `XpInfoDto`, `StreakInfoDto`, `NbNextMilestoneDto`, `ProgramCalendarDto`, `ProgramPathDto`, `CompleteTaskResponseDto`, `ProgramEnrollmentDto`, `ScoresResponseDto`, `HealthScoreDto`, `TransformationScoreDto`, `IndicatorDetailDto`, `NutritionLogResultDto`; `TaskCode = 'podcast'|'vitals'|'nut'|'ejercicio'|'nutribiotico'|'emocional'`; fechas `YYYY-MM-DD` string; campos aditivos opcionales con defaults (R7.1); `import type` (verbatimModuleSyntax) — aceptación: R7.4 sin campos inventados; `npm run build` limpio — dep: — paralelo: sí (con 1.3)
- [ ] 1.6 `antares-paciente/src/utils/authApi.ts` — puente de sesión: callback/evento a `AppContext` cuando refresh devuelve `X-Refresh-Status: invalid` → `flow='login'`, `screen='home'`; `missing` NO se presenta como sesión expirada; sin logout recursivo — aceptación: R1.2/R1.4, R4.5 — dep: 1.3 — paralelo: —
- [ ] 1.7 `antares-paciente/src/utils/apiClient.ts` + `vite.config.ts` — leer `VITE_API_BASE_URL`/`VITE_AUTH_BASE_URL` (default relativo en dev) para Capacitor nativo; sin URLs productivas hardcodeadas; flag `VITE_PROGRAM_API_ENABLED` (G5) — aceptación: env var documentada en `.env.example` — dep: 1.3 — paralelo: —

## Fase 2: Capa de servicios (transporte)

- [ ] 2.1 `antares-paciente/src/services/program/program-service.ts` (nuevo) — `getSnapshot()` (GET `/api/v1/program/me/snapshot`), `getPath()` (GET `/path`), `getCalendar(from,to)` (GET `/calendar?from=&to=` con rango ≤ 92 días), `enrollMe(timezone)` (POST `/enrollments/me` con `{patientId:null, templateId:null, timezone}`) — aceptación: rutas/bodies exactos de R2; `404/409/422` propagados como `ApiError` — dep: 1.3, 1.5 — paralelo: sí
- [ ] 2.2 `antares-paciente/src/services/program/tasks-service.ts` (nuevo) — `completeTask(payload: CompleteTaskInput)` POST `/api/v1/program/tasks/complete` con `enrollmentId`, `localDate`, `taskCode`, `clientRequestId` (obligatorio, ULID/UUID), `clientCompletedAt?`, `moodScore?`, `barriers?`, `contentFingerprint?` — aceptación: R2.2/R3.1; `enrollmentId`/`todayLocalDate` SIEMPRE del snapshot, nunca de la UI — dep: 1.3, 1.5 — paralelo: sí
- [ ] 2.3 `antares-paciente/src/services/program/scores-service.ts` (nuevo) — `getScores()` GET `/api/v1/program/scores`, captura headers `X-Score-Stale`/`X-Score-Recalculated`; nombres `snake_case` (`health_score`, `transformation_score`) — aceptación: R2.5; sin campo `calculatedAt` inventado (G3) — dep: 1.3, 1.5 — paralelo: sí
- [ ] 2.4 `antares-paciente/src/services/program/nutrition-service.ts` (nuevo) — `logMeal(mealCode, localDate?)` POST `/api/v1/program/nutrition/log`; `mealCode` validado `des|alm|mer|cen|agua` — aceptación: R2.6; duplicado → `409 HABIT_ALREADY_LOGGED` propagado — dep: 1.3, 1.5 — paralelo: sí
- [ ] 2.5 `antares-paciente/src/services/program/offline-queue.ts` (nuevo) — cola `localStorage` keyed por `clientRequestId` (máx. 50 entradas FIFO, tope de tamaño, payloads sin PHI); listener `online` + resume → replay en orden con la MISMA clave; entrada que falla por red permanece; por regla de negocio (4xx) se descarta con aviso; tras lote exitoso → callback de invalidación de queries — aceptación: R5.4/R5.5; idempotencia server previene doble premio (R3.2) — dep: 2.2 — paralelo: —

## Fase 3: Hooks y caché

- [ ] 3.1 `antares-paciente/src/hooks/useProgram.ts` (nuevo) — `useQuery(['program','snapshot'], getSnapshot)`: 404 → auto-inscripción UNA vez por montaje con `Intl.DateTimeFormat().resolvedOptions().timeZone` (R4.1); `409 PATIENT_ALREADY_ENROLLED` → ignorar (NUNCA parsear body como enrollment, R4.2) + refetch 1 vez; `422`/`TEMPLATE_NOT_ACTIVE`/`ENROLLMENT_WITHDRAWN` → sin retry, estado de producto (R4.3–R4.5, G1/G2); error elegible → cache stale o mock fallback marcado `// TODO: Remove mock fallback`, SIN toast (R5.2) — aceptación: escenarios R4.1–R4.4, R5.2 — dep: 2.1 — paralelo: —
- [ ] 3.2 `antares-paciente/src/hooks/useProgramPath.ts` (nuevo) — `useQuery(['program','path'], getPath)` + fallback elegible — aceptación: R2.4 nodos `Locked/Active/Completed` — dep: 2.1 — paralelo: sí
- [ ] 3.3 `antares-paciente/src/hooks/useCompleteTask.ts` (nuevo) — `useMutation`: genera `clientRequestId` UNA vez por acción; `onMutate`: cancelar queries, guardar snapshot previo, marcar tarea completada + estimación XP, confeti/popup UNA vez por clave (dedupe local, el body de replay no trae flag); `onError`: rollback de TODOS los caches; `onSuccess`: reconciliar con `xpBalanceAfter`/`streakCurrent`/`dayPoints`/`dailyBonusAwarded`; `onSettled`: invalidar `snapshot`, `path`, `calendar`; red → cola offline (R5.4); `409 IDEMPOTENCY_KEY_REUSED` → clave nueva + 1 reintento (R3.3); `409 TASK_NOT_SCHEDULED`/`422 DATE_OUTSIDE_ACTIVE_WEEK`/`409 ENROLLMENT_INACTIVE` → SIN retry ni cola, invalidar snapshot (R3.4, R4.4) — aceptación: escenarios R3.2–R3.4, R5.3 — dep: 2.2, 2.5 — paralelo: —
- [ ] 3.4 `antares-paciente/src/hooks/useProgramCalendar.ts` (nuevo) — `useQuery(['program','calendar',from,to], getCalendar)`; rango del mes en curso ≤ 92 días — aceptación: R2.3 mapa con `days[].isPerfectDay` — dep: 2.1 — paralelo: sí
- [ ] 3.5 `antares-paciente/src/hooks/useProgramScores.ts` (nuevo) — `useQuery(['program','scores'], getScores)` + header de frescura (G3) + fallback elegible — aceptación: R2.5 — dep: 2.3 — paralelo: sí
- [ ] 3.6 `antares-paciente/src/hooks/useNutritionLog.ts` (nuevo) — `useMutation(logMeal)` con optimismo; `409 HABIT_ALREADY_LOGGED` → revertir optimismo, reconciliar, SIN XP nueva ni cola (R2.6, R5.6) — aceptación: escenario R2.6 — dep: 2.4 — paralelo: sí
- [ ] 3.7 `antares-paciente/src/hooks/queryKeys.ts` (nuevo, o constante en 3.1) — centralizar keys `['program','snapshot']`, `['program','calendar',from,to]`, `['program','path']`, `['program','scores']` + matriz de invalidación (completar/registrar → snapshot+path+calendar+scores; enroll → todo; focus/reconnect → stale) — aceptación: R5.3 invalidation exacta — dep: 3.1 — paralelo: —

## Fase 4: Puente de estado y wiring de UI

- [ ] 4.1 `antares-paciente/src/context/AppContext.tsx` — `completeStep` → wrapper que delega en `useCompleteTask()`; campos legacy (`completedSteps`, `streak`, `xp`, `programWeek`, `weekCheckins`, `pointsToday/Total`) marcados `// TODO: Remove after full migration`; ningún campo local gana a respuesta del server — aceptación: R6 ProgramPage; navegación `flow/screen` intacta (Design §Integración visual) — dep: 3.3 — paralelo: —
- [ ] 4.2 `antares-paciente/src/data/program.ts` — mocks como fallback con `// TODO: Remove mock fallback` en cada constante; misma forma que respuestas del server — aceptación: grep `TODO: Remove mock fallback` localiza todos — dep: 3.1 — paralelo: —
- [ ] 4.3 `antares-paciente/src/pages/ProgramPage.tsx` — header con `useProgram()` (racha, `pointsTotal`, `programWeek`); estados: loading (`IonLoading`/`IonSpinner`), pausado, retirado, "programa no listo" con CTA de contacto (R4.4/R4.5, G1/G2); conservar `IonSegment`/tabs/modal lecciones — aceptación: R6 ProgramPage; sin regresión visual — dep: 3.1, 4.1 — paralelo: —
- [ ] 4.4 `antares-paciente/src/pages/program/TodayView.tsx` — nodos del sendero desde `useProgramPath()` (`Locked/Active/Completed`); completar → `useCompleteTask().mutate()` con optimismo; confeti/popup en `onMutate`; nunca inventar tareas fuera de `todayTasks[]` (SPEC §6.1) — aceptación: R6 TodayView + escenarios R2.2 — dep: 3.2, 3.3 — paralelo: — (secuencia: header primero, luego Hoy)
- [ ] 4.5 `antares-paciente/src/pages/program/StreakView.tsx` — racha de `useProgram().snapshot.streak` (current/longest/freezes/multiplier) + mapa de consistencia con `useProgramCalendar().days[].isPerfectDay`; mock solo fallback — aceptación: R6 StreakView, R2.3 — dep: 3.1, 3.4 — paralelo: —
- [ ] 4.6 `antares-paciente/src/pages/program/EvolutionView.tsx` — pilares/filas desde `useProgramScores()`; etiquetas "Índice de Salud / Transformación" (nunca XP) + frescura por header (G3); conservar pilares/filas solo si el DTO los provee — aceptación: R6 EvolutionView, R2.5 — dep: 3.5 — paralelo: —
- [ ] 4.7 `antares-paciente/src/pages/program/Lessons.tsx` — cada lección (podcast/vitals/nut/ejercicio/nutribiotico/emocional) completa su `taskCode` vía `useCompleteTask()`; emoción exige `moodScore` 1..5 — aceptación: R6 Lessons; `moodScore` validado en borde (Design §Tipos) — dep: 3.3 — paralelo: —
- [ ] 4.8 `antares-paciente/src/pages/NutritionPage.tsx` — SI existe UI de registro de comidas/hidratación: wire `useNutritionLog()`; duplicado → estado "ya registrado" sin XP (R2.6); si no hay UI de log, documentar y no inventarla (fuera de alcance) — aceptación: R2.6 — dep: 3.6 — paralelo: —

## Fase 5: Verificación

- [ ] 5.1 `antares-paciente/` — `npm run build` (`tsc -b && vite build`) 0 errores — aceptación: R6.1 DoD — dep: Fase 4 — paralelo: —
- [ ] 5.2 `antares-paciente/` — `npm run lint` (oxlint) limpio — aceptación: R6.1 — dep: Fase 4 — paralelo: —
- [ ] 5.3 Smoke API real: `npm run dev` + Network tab — login → `GET snapshot` 200; `404` → `POST enrollments/me` → refetch 200; completar tarea → 200 `pointsAwarded`; replay misma clave → 200 sin XP nueva; nutrición duplicada → `409 HABIT_ALREADY_LOGGED` (escenarios R2/R3/R4) — aceptación: sin mocks en el flujo server — dep: 5.1, 5.2 — paralelo: —
- [ ] 5.4 Smoke degradado: backend caído → cache stale primero, mock fallback si no hay cache, SIN toast de query; mutación sin red → toast + cola `localStorage`; reconexión → replay en orden + invalidación (R5.4/R5.5) — aceptación: escenarios R5 — dep: 5.3 — paralelo: —
- [ ] 5.5 Anti-leak: `grep -r "ai-service\|localhost:8000" antares-paciente/src` → 0 coincidencias (R7.3; todo tráfico IA vía backend) — aceptación: R7.3 — dep: Fase 4 — paralelo: —
- [ ] 5.6 SOLO si G7 decidió introducir Vitest: unit tests de `apiClient` parser `ApiError`/timeout, guardas de tipos/fechas, FIFO de cola, dedupe de animación por clave, auto-enroll con 409/422, rollback e invalidación — aceptación: suite verde; SIN inventar runner si G7 no se decide (verificamos con 5.1/5.2/5.3) — dep: G7 — paralelo: —

## Fase 6: Fallback operativo (CONDICIONAL — solo con evidencia)

- [ ] 6.1 SOLO si el smoke 5.3 muestra tablas ausentes (ej. `relation "app.task_completions" does not exist`): DETENER el flujo y ejecutar `dotnet ef database update --project src/CoppAddresd.Infrastructure --startup-project src/CoppAddresd.Api` en `coppAddresdBack/` (migraciones `public.__EFMigrationsHistory`) — NUNCA como paso automático ni tarea por defecto — aceptación: smoke 5.3 pasa tras aplicar — dep: 5.3 — paralelo: —

---

## Orden de implementación y workstreams paralelos

```
Gates G1/G2/G5 → F1 Fundamentos (1.1–1.7)
   ↓
F2 Servicios (2.1–2.5, en paralelo entre sí) ──┐
   ↓                                            │ (merge boundary A: build+lint tras F1+F2)
F3 Hooks (3.1–3.7; 3.2/3.4/3.5/3.6 en paralelo) │
   ↓                                            │ (merge boundary B: smoke snapshot/enroll tras F3)
F4 Puente + UI (4.1 → 4.2 → 4.3 → 4.4 → 4.5 → 4.6 → 4.7/4.8) — secuencial
   ↓                                            │ (merge boundary C: smoke completo tras F4)
F5 Verificación (5.1–5.6) → F6 condicional
```

| Workstream | Archivos | Depende de | Paralelo |
|------------|----------|------------|----------|
| A — contrato/infra | `apiClient.ts`, `authApi.ts`, `vite.config.ts`, `types.ts`, `App.tsx`, `package.json` | Gates G1/G2/G5 | A interno: tipos y apiClient en paralelo |
| B — transporte | `services/program/*` (5 archivos) | A | Sí, servicio por servicio |
| C — caché | 6 hooks + `queryKeys.ts` | B | Sí, hook por hook (3.3 necesita 2.5) |
| D — puente | `AppContext.tsx`, `data/program.ts` | C | No (debe ver hooks) |
| E — UI | 5 vistas | C/D | Secuencial: header → Hoy → Racha → Evolución → lecciones/nutrición |

**Fronteras de integración** (sin commits/PRs): cada merge boundary = punto de verificación en el
working tree (`npm run build` + `npm run lint` + smoke parcial). Slice 1 = WS A+B+C-lectura;
Slice 2 = C-mutación + D + E; Slice 3 = F5 + gates P2.

## Criterios transversales (aplican a toda tarea)

- Backend = fuente de verdad: rutas, DTOs, estados (SPEC §7, R7.4). Tipos TS literales, sin campos inventados (R7.1).
- Anti-IDOR (R7.2): lecturas sin ids de paciente/inscripción; mutaciones usan ids del snapshot; `404` en lecturas = "sin acceso o sin inscripción".
- UI Ionic-first (`ionic-rules`): estados con `IonLoading`/`IonSpinner`/`IonToast`/`IonButton`; animaciones actuales y CSS de sendero conservados; sin router nuevo (Design §Integración visual).
- Sin llamadas directas al `ai-service` (R7.3). Sin commits, sin PRs, sin cambios en backend (esta fase es solo documentación + `antares-paciente`).