# EXPLORACIÓN — Integración Móvil del Programa de Progreso

> Exploración técnica del estado actual del código backend y móvil para la
> integración del módulo Progreso del Programa. Fuente de verificación: código
> real, no文档ación. Articulo de referencia: [`MOBILE-INTEGRATION.md`](./MOBILE-INTEGRATION.md).

---

## 1. Estado actual verificado

### 1.1 Backend (.NET) — 100% implementado

| Componente | Estado | Detalle |
|------------|--------|---------|
| `ProgramController.cs` | ✅ 998 líneas | 44+ endpoints REST bajo `/api/v1/program/*`. Todos requieren `[Authorize]`. |
| `ProgramActorContext.cs` | ✅ Implementado | Resolución del paciente desde JWT (`patient_id` claim o fallback a `app.patient_profiles.user_id`). Memoizado por request. Anti-IDOR por diseño. |
| DTOs (`ProgramProgressDtos.cs`) | ✅ 593 líneas | `ProgramSnapshotDto`, `CompleteTaskResponseDto`, `ProgramCalendarDto`, `ProgramPathDto`, `ScoresResponseDto`, etc. |
| Repository (`ProgramRepository.cs`) | ✅ Implementado | `FOR UPDATE` concurrency, snapshot jsonb, resolución de contenido por fecha. |
| Migraciones | ✅ 12 generadas | Todas en `public.__EFMigrationsHistory`. Migraciones pendientes de aplicación (asumido aplicado según instrucciones). |
| Seeders | ✅ Implementado | `default-83w`, 24 reglas XP, 5 health score weights, 5 habit templates, permisos `Program.*`. |
| Tests unitarios | ✅ 454/454 passing | Cubren handlers, validators, lógica de dominio. |

### 1.2 App móvil (`antares-paciente`) — UI completa, 0% integrada

| Componente | Estado | Detalle |
|------------|--------|---------|
| `ProgramPage.tsx` | ✅ UI completa | Header + 3 tabs (Hoy/Racha/Evolución) + modal de lecciones. Consume `AppContext` (estado local). |
| `TodayView.tsx` | ✅ UI completa | Sendero Duolingo gamificado con pasos. Lee `AppContext.completedSteps`. |
| `StreakView.tsx` | ✅ UI completa | Racha + calendario de consistencia. Mock: `streak=12`, constantes. |
| `EvolutionView.tsx` | ✅ UI completa | Health Score + Transformation Score. Mock: `HEALTH_SCORE=86`, `TRANSFORM_SCORE=87`. |
| `Lessons.tsx` | ✅ UI completa | 6 lecciones interactivas. Sin persistencia, solo UI. |
| `data/program.ts` | ✅ Mock maestro | 700 pts/día, 7 niveles XP, 5 pilares de salud. Constantes estáticas. |
| `AppContext.tsx` | ✅ Estado global | `completedSteps`, `streak`, `xp`, `programWeek`. In-memory, sin sync. |
| `types.ts` | ✅ Mínimo | `ProgramTaskId`, `ProgramDay`. Sin tipos de backend. |
| `authApi.ts` | ✅ Auth only | Login, OTP, logout. Token en `sessionStorage`. Sin `apiClient.ts`. |
| `package.json` | ⚠️ Sin dependencias HTTP | Sin `@tanstack/react-query`, sin cliente HTTP genérico. |

### 1.3 Gap crítico

**Cero llamadas HTTP del móvil al backend.** No existe:
- `apiClient.ts` (wrapper de fetch autenticado)
- Capa de servicios (`program-service.ts`, `tasks-service.ts`)
- Hooks de TanStack Query (`useProgram`, `useCompleteTask`)
- Tipos TypeScript que matcheen los DTOs del backend

---

## 2. Contratos validados del backend

### 2.1 Ruta de auto-inscripción del paciente

**Endpoint implementado**: `POST /api/v1/program/enrollments/me`

```csharp
// ProgramController.cs, línea 189
[HttpPost("enrollments/me")]
public async Task<ActionResult<ProgramEnrollmentDto>> EnrollSelf(
    [FromBody] EnrollRequest request, CancellationToken ct)
```

**Request body** (`EnrollRequest`):
```json
{
  "patientId": null,           // Ignorado para el paciente (sale del JWT)
  "templateId": "uuid-opcional", // Null → usa plantilla por defecto (config)
  "timezone": "America/Bogota",  // IANA, requerido
  "startLocalDate": "2026-09-21" // Opcional
}
```

**Response**: `ProgramEnrollmentDto` con `id`, `patientId`, `templateId`, `status`, `xpBalance`, `streakCurrent`, `freezesRemaining`, etc.

**Comportamiento**:
- El `patientId` se resuelve SIEMPRE del JWT vía `IProgramActorContext.ResolvePatientProfileIdAsync()`.
- Sin perfil de paciente → `404`.
- Ya inscrito activo → `409` (índice único parcial).
- La plantilla por defecto viene de `Program:DefaultTemplate:Code` en config (fallback `default-83w`).

**Corrección vs documentación**: La sección §2.3 de `MOBILE-INTEGRATION.md` dice que el endpoint es idempotente. **Esto es parcialmente correcto**: si el paciente ya tiene inscripción activa, devuelve `409` (no idempotente en el sentido de "devuelve la existente"). La auto-inscripción solo funciona si NO hay inscripción activa.

### 2.2 Snapshot del programa

**Endpoint**: `GET /api/v1/program/me/snapshot`

**Response** (`ProgramSnapshotDto`):
```json
{
  "enrollmentId": "uuid",
  "template": {
    "id": "uuid",
    "code": "default-83w",
    "name": "Programa 83 semanas",
    "totalWeeks": 83,
    "currentWeekNumber": 12,
    "currentWeekStatus": "Active",
    "currentWeekStartDateLocal": "2026-09-21",
    "currentWeekEndDateLocal": "2026-09-27",
    "streakMinTasks": 1,
    "essentialTaskCodes": ["nut","ejercicio","nutribiotico"]
  },
  "todayLocalDate": "2026-09-24",
  "todayTasks": [
    {
      "taskCode": "podcast",
      "title": "Escuchar podcast",
      "short": "Biohacking y metabolismo · 8 min",
      "points": 80,
      "status": "Pending",
      "completedAt": null,
      "content": {
        "mediaId": "uuid",
        "title": "...",
        "durationSecs": 492,
        "thumbnailUrl": "..."
      }
    }
  ],
  "todayPoints": 200,
  "todayBonusAvailable": true,
  "todayPointsMax": 750,
  "xp": { "balance": 1620, "level": "Constante", "nextLevelAt": 3000 },
  "streak": {
    "current": 11,
    "longest": 27,
    "freezesRemaining": 2,
    "multiplierActive": 2.0,
    "multiplierEndsAt": "2026-09-25T11:14:08Z",
    "multiplierRemainingHours": 23,
    "nbStreak": 5,
    "nbLongestStreak": 12,
    "nbNextMilestone": { "days": 7, "xp": 50, "daysRemaining": 2 }
  },
  "nextMilestoneDays": 16,
  "calendar": [ /* 7 días */ ]
}
```

**Error**: `404 NO_ACTIVE_ENROLLMENT` cuando no hay inscripción activa.

### 2.3 Completación de tareas

**Endpoint**: `POST /api/v1/program/tasks/complete`

**Request** (`CompleteTaskRequest`):
```json
{
  "enrollmentId": "uuid",
  "localDate": "2026-09-24",
  "taskCode": "podcast",
  "clientRequestId": "ulid-01H...",
  "clientCompletedAt": "2026-09-24T11:14:08Z",
  "moodScore": 4,
  "barriers": null,
  "contentFingerprint": "sha256:..."
}
```

**Response** (`CompleteTaskResponseDto`):
```json
{
  "taskCompletionId": "uuid",
  "pointsAwarded": 80,
  "xpBalanceAfter": 1700,
  "isPerfectDay": false,
  "dailyBonusAwarded": 0,
  "streakCurrent": 11,
  "freezesRemaining": 2,
  "dayPoints": 280,
  "dayPointsMax": 750
}
```

**Idempotencia**: `clientRequestId` se persiste en `task_completions`. Replay con la misma clave → 200 con body existente, 0 XP nueva. Reutilizar la misma clave con otra fecha/tarea → `409 IDEMPOTENCY_KEY_REUSED`.

**Concurrencia**: `FOR UPDATE` en `program_enrollments` antes de escribir. 50 escrituras concurrentes → 1 fila, 1 otorgamiento.

### 2.4 Calendario

**Endpoint**: `GET /api/v1/program/calendar?from=2026-09-01&to=2026-09-30`

**Response**: `ProgramCalendarDto` con array de `CalendarDayDetailDto` (localDate, weekday, weekNumber, isPerfectDay, points, bonusAwarded, completedTaskCodes) + `CalendarSummaryDto` (perfectDays, missedDays, totalXp).

**Límite**: máx 92 días por request.

### 2.5 Sendero

**Endpoint**: `GET /api/v1/program/path`

**Response**: `ProgramPathDto` con array de `ProgramPathWeekDto` (weekNumber, status, isPerfectWeek, points, weekStartDateLocal, weekEndDateLocal).

### 2.6 Puntajes (Evolución)

**Endpoint**: `GET /api/v1/program/scores`

**Response**: `ScoresResponseDto` con Health Score + Transformation Score (compute-on-read, se recalcula si está vencido).

**Error**: `404` si no hay perfil de paciente o inscripción activa.

### 2.7 Nutrición granular

**Endpoint**: `POST /api/v1/program/nutrition/log`

**Request**: `{ mealCode: "des"|"alm"|"mer"|"cen"|"agua", localDate?: "2026-09-24" }`

**Response**: `NutritionLogResultDto` con mealCode, localDate, xpAwarded.

**Error**: `409 HABIT_ALREADY_LOGGED` si ya registró la misma comida el mismo día.

### 2.8 Notificaciones gamificadas

**Endpoint**: `GET /api/v1/program/notifications` (paginado) + `POST /api/v1/program/notifications/{id}/read`

### 2.9 Debilidades del paciente

**Endpoint**: `GET /api/v1/program/weaknesses` (paginado)

### 2.10 Intervenciones del paciente

**Endpoint**: `GET /api/v1/program/interventions` + `POST /api/v1/program/interventions/{id}/accept`

---

## 3. Autenticación y flujo de tokens

### 3.1 Estado actual del móvil

```typescript
// authApi.ts
const ACCESS_TOKEN_KEY = 'copp_access_token'

export function getAccessToken(): string | null {
  return sessionStorage.getItem(ACCESS_TOKEN_KEY)
}
```

- Login: `POST /api/auth/login` con `{ documentNumber, password, application: 'app', rememberMe }`.
- OTP: `POST /api/auth/id-lookup` → `POST /api/auth/send-otp` → `POST /api/auth/verify-otp`.
- Token se guarda en `sessionStorage` (no HttpOnly).
- Refresh cookie: `copp_refresh_token` (HttpOnly, `SameSite=Lax`, `Path=/api/auth`).
- En dev: Vite proxy redirige `/api/auth/*` al Auth Service (puerto 5123).

### 3.2 Patrón de refresh del ERP (referencia)

El ERP usa `apiFetch` (`coppaddresd-front/lib/api/http.ts`, 398 líneas) con:
- Single-flight refresh en 401 (evita carreras de refresh concurrente).
- `credentials: 'include'` para enviar la cookie HttpOnly.
- Timeout de 30s.

### 3.3 Lo que necesita el móvil

Crear `apiClient.ts` con:
- `apiFetch<T>(path, options)` con Bearer token de `sessionStorage`.
- Single-flight refresh en 401 (mismo patrón que ERP).
- `credentials: 'include'` para la cookie de refresh.
- Timeout de 15s (mobile networks).
- `ApiError` class compatible con RFC 7807 (ProblemDetails del backend).

### 3.4 Limitación conocida

El JWT del paciente **no incluye** `patient_id` claim todavía. El backend resuelve el paciente vía `ProgramActorContext.ResolvePatientProfileIdAsync()` (1 query a `app.patient_profiles.user_id`, memoizado por request). Esto funciona pero agrega latencia. El Auth Service debería emitir el claim en el futuro.

---

## 4. Acoplamiento actual del móvil y archivos que cambian

### 4.1 Fuentes de datos actuales

| Fuente mock | Archivo | Reemplazo |
|-------------|---------|-----------|
| `PROGRAM_TASKS` (6 tareas, estáticas) | `data/program.ts` | `snapshot.todayTasks[]` |
| `AppContext.completedSteps` (Set en memoria) | `context/AppContext.tsx` | `path[].completed` + `useCompleteTask()` |
| `streak = 12` (hardcoded) | `context/AppContext.tsx` | `snapshot.streak.current` |
| `HEALTH_SCORE = 86` | `data/program.ts` | `scores.healthScore` |
| `TRANSFORM_SCORE = 87` | `data/program.ts` | `scores.transformationScore` |
| `PROGRAM_LEVELS` (array local) | `data/program.ts` | `snapshot.xp.level` |
| `weekCheckins` (AppContext) | `context/AppContext.tsx` | `calendar[].isPerfectDay` |
| `programWeek` (AppContext) | `context/AppContext.tsx` | `snapshot.template.currentWeekNumber` |
| `pointsToday` / `pointsTotal` (AppContext) | `context/AppContext.tsx` | `snapshot.todayPoints` / `snapshot.xp.balance` |

### 4.2 Archivos que DEBEN cambiar

| Archivo | Cambios requeridos |
|---------|-------------------|
| `package.json` | Agregar `@tanstack/react-query` |
| `src/App.tsx` (o entry point) | Envolver con `QueryClientProvider` |
| `src/pages/ProgramPage.tsx` | Consumir `useProgram()` en vez de `AppContext` para campos de programa |
| `src/pages/program/TodayView.tsx` | Consumir `useProgramPath()` + `useCompleteTask()` |
| `src/pages/program/StreakView.tsx` | Consumir `useProgram()` para racha + `useProgramCalendar()` para mapa |
| `src/pages/program/EvolutionView.tsx` | Consumir `useProgramScores()` |
| `src/pages/program/Lessons.tsx` | Cada lección "completar" usa `useCompleteTask().mutate()` |
| `src/context/AppContext.tsx` | `completeStep` → wrapper que llama mutation; campos legacy con `// TODO: Remove after full migration` |
| `src/data/program.ts` | Agregar `// TODO: Remove mock fallback` markers, mantener como fallback |

### 4.3 Archivos NUEVOS a crear

```
antares-paciente/src/
├── utils/
│   └── apiClient.ts              ← Authenticated fetch wrapper
├── services/
│   └── program/
│       ├── types.ts              ← TS interfaces matching DTOs del backend
│       ├── program-service.ts    ← snapshot, path, calendar, enrollment, notifications
│       ├── tasks-service.ts      ← completeTask (POST con clientRequestId)
│       ├── scores-service.ts     ← Health Score, Transform Score
│       └── nutrition-service.ts  ← meal/hydration logging
└── hooks/
    ├── useProgram.ts             ← useQuery(/me/snapshot) + auto-inscripción
    ├── useProgramPath.ts         ← useQuery(/path) para TodayView
    ├── useCompleteTask.ts        ← useMutation + actualización optimista XP/streak
    ├── useProgramCalendar.ts     ← useQuery(/calendar) para StreakView
    ├── useProgramScores.ts       ← useQuery(/scores) para EvolutionView
    └── useNutritionLog.ts        ← useMutation para comidas
```

---

## 5. Contrato de idempotencia y retry seguro

### 5.1 Mecanismo del backend

- **Clave de cliente**: `clientRequestId` (string, recomendado ULID) en `task_completions.client_request_id`.
- **Unicidad parcial**: `(enrollment_id, local_date, task_code)` previene duplicación de tareas.
- **Deduper del libro mayor**: `(source_ref_type, source_ref_id, reason)` en `xp_ledger` previene doble XP.
- **Concurrencia**: `FOR UPDATE` en `program_enrollments` serializa otorgamientos.

### 5.2 Estrategia de retry del móvil

1. **Generar `clientRequestId` una vez** por intento de completar (ULID o UUID).
2. **Si la mutation falla por red**: reintentar con la **misma** `clientRequestId`. El backend devolverá 200 con el body existente (replay idempotente).
3. **Si se recibe 409 `IDEMPOTENCY_KEY_REUSED`**: la clave fue reutilizada con otra tarea/fecha. Generar nueva clave.
4. **Cola offline**: persistir en `localStorage` keyed por `clientRequestId`. Al reconectar, replay en orden. El server idempotency previene doble premio.

### 5.3 Optimistic update pattern

```typescript
// useCompleteTask.ts
onMutate: async (variables) => {
  // Cancelar queries en curso
  await queryClient.cancelQueries({ queryKey: ['program-snapshot'] })
  // Snapshot anterior
  const previous = queryClient.getQueryData(['program-snapshot'])
  // Actualizar optimistamente (XP, streak, etc.)
  queryClient.setQueryData(['program-snapshot'], (old) => updateOptimistic(old, variables))
  return { previous }
},
onError: (err, variables, context) => {
  // Rollback
  queryClient.setQueryData(['program-snapshot'], context.previous)
},
onSettled: () => {
  // Invalidar para refetch del server
  queryClient.invalidateQueries({ queryKey: ['program-snapshot'] })
}
```

---

## 6. Manejo de estados especiales

### 6.1 Inscripción pausada

- `snapshot` devuelve la inscripción con `status: "Paused"`.
- La UI debería mostrar "Tu programa está pausado. Contacta a tu equipo de salud."
- Las mutaciones de completar tarea fallan con `409 ENROLLMENT_INACTIVE`.

### 6.2 Inscripción retirada (Withdrawn)

- Misma semántica que "no hay inscripción" para el paciente.
- La auto-inscripción (`POST /enrollments/me`) NO debería dispararse (el servidor rechaza re-inscripción de pacientes retirados).
- UI: "Tu programa no está disponible. Contacta a tu equipo de salud."

### 6.3 Sin plantilla publicada

- La auto-inscripción falla con `422` si no hay plantilla `Active` disponible.
- UI: "Tu programa no está listo aún. Contacta a tu equipo de salud."

### 6.4 Comportamiento offline

| Escenario | Estrategia |
|-----------|-----------|
| Query sin red | TanStack Query sirve cache stale. Sin cache → fallback a mock. Sin toast de error. |
| Mutation sin red | Toast: "Sin conexión. Tu progreso se sincronizará cuando vuelvas a estar en línea." Cola en `localStorage`. |
| Reconexión | Replay de colas en orden. Server idempotency previene duplicación. Invalidar todas las queries del programa. |
| 401 durante API call | `apiClient.ts` maneja refresh transparentemente (single-flight). Si falla → redirect a login. |
| Server error (5xx) | Retry hasta 2 veces (TanStack default). Luego fallback mock para queries, toast de error para mutations. |

---

## 7. Migraciones

Las 12 migraciones del backend están generadas pero **no aplicadas** en la BD de desarrollo. Según las instrucciones de la fase, se asume que el usuario ya las aplicó. Si hay evidencia en contrario (errores de tabla no encontrada),Bloquear hasta su aplicación.

Las migraciones relevantes para la integración móvil ya están en el código:
- `AddProgramProgressCore` (P1 schema)
- `AddProgramProgressClinicalXp`, `AddProgramProgressMultiplier`, `AddProgramProgressStreakConfig`, `AddProgramProgressNutritionXp`, `AddProgramProgressNbStreak`, `AddProgramProgressNotifications`, `AddProgramProgressWeaknesses` (P1.5)

**No hay migraciones pendientes que bloqueen la integración móvil.**

---

## 8. Dependencias y workstreams paralelos seguros

### 8.1 Secuencia de trabajo

```
Fase 1: apiClient.ts + types.ts + QueryClientProvider
  ↓
Fase 2: program-service.ts + tasks-service.ts + scores-service.ts + nutrition-service.ts
  ↓
Fase 3: useProgram() + useCompleteTask() + useProgramCalendar() + useProgramPath() + useProgramScores() + useNutritionLog()
  ↓
Fase 4: Wiring por vista (ProgramPage → TodayView → StreakView → EvolutionView → Lessons)
```

### 8.2 Workstreams paralelos

| Workstream | Dependencia | Puede correr en paralelo |
|------------|-------------|-------------------------|
| `apiClient.ts` + `types.ts` | Ninguna | ✅ Sí |
| Service layer | `apiClient.ts` | ✅ Sí (cada service independiente) |
| Hooks | Service layer | ✅ Sí (cada hook independiente) |
| Wiring de vistas | Hooks | ❌ No (depende de hooks) |
| Tests de contract | Service layer | ✅ Sí |

### 8.3 Riesgo de paralelismo

Los services y hooks pueden desarrollarse en paralelo porque cada uno consume un endpoint distinto. Sin embargo, el wiring de vistas debe secuenciarse: ProgramPage (header) primero, luego las tabs.

---

## 9. Riesgos

| Riesgo | Probabilidad | Impacto | Mitigación |
|--------|-------------|---------|------------|
| JWT sin `patient_id` claim | Alta | Latencia extra por query de resolución | Funcional pero subóptimo. Resolución vía `app.patient_profiles.user_id` (1 query, memoizado). Documentar como OQ-1. |
| Refresh cookie no funciona en Capacitor nativo | Media | 401 persistente | En dev funciona vía Vite proxy. En producción nativo, necesita validación con Capacitor HTTP plugin o migrar a Secure Storage. Documentado como P2. |
| Mock fallback rompe tipado | Baja | Errores de runtime | Cada hook retorna tipo consistente; fallback mock tiene la misma forma que la respuesta del server. |
| Offline queue crece indefinidamente | Baja | Storage del dispositivo lleno | Limitar cola a 50 entradas; las más antiguas se descartan con toast de advertencia. |
| `EvolutionView` consume datos de `HEALTH_PILLARS` y `TRANSFORM_ROWS` que vienen de clinical measurements (no del endpoint de scores) | Media | Confusión de fuentes | `GET /scores` devuelve Health Score + Transformation Score con pillar breakdown. Las filas `TRANSFORM_ROWS` del mock son datos estáticos que el endpoint de scores reemplaza. Verificar el shape exacto de `ScoresResponseDto`. |
| Concurrent mutations (mismo paciente, múltiples dispositivos) | Baja | Doble XP | `FOR UPDATE` + unique constraint previene duplicación. Idempotencia por `clientRequestId`. |

---

## 10. Preguntas abiertas

| # | Pregunta | Estado | Recomendación |
|---|----------|--------|---------------|
| 1 | ¿El Auth Service ya emite `patient_id` en el JWT? | No verificado en código | Verificar `TokenService.cs` del Auth Service. Si no, el fallback de `ProgramActorContext` funciona. |
| 2 | ¿El endpoint `GET /scores` devuelve Health Score con pillar breakdown para `EvolutionView`? | DTO definido (`ScoresResponseDto`) pero no inspeccionado en detail | Inspeccionar `ScoresResponseDto` para confirmar que incluye dimensiones por pillar. |
| 3 | ¿La auto-inscripción es realmente idempotente o devuelve 409? | Verificado: devuelve 409 si ya hay inscripción activa | Ajustar la lógica del hook: si 404 → intentar enroll; si 409 → ignorar (ya inscrito). |
| 4 | ¿El endpoint `/me/snapshot` necesita `enrollmentId` en el request? | No: resuelve del JWT vía `ProgramActorContext` | Correcto para el móvil: el paciente nunca envía su enrollmentId. |
| 5 | ¿Los niveles XP del mock (`PROGRAM_LEVELS`) coinciden con `XpLevels` del backend? | Verificado: mismos rangos (0-499, 500-1499, etc.) | Sí, coinciden. El backend calcula el nivel en `XpInfoDto.Level` (string). |

---

## 11. Recomendación

### Enfoque recomendado: Slice-by-slice con mock fallback

1. **Primero**: `apiClient.ts` + `types.ts` + `QueryClientProvider` (infraestructura base).
2. **Segundo**: Service layer + hooks con mock fallback (cada endpoint independiente).
3. **Tercero**: Wiring por vista, empezando por `ProgramPage` (header) y `TodayView` (la más compleja).
4. **Cuarto**: `StreakView`, `EvolutionView`, `Lessons` (menos dependientes del backend).

**Cada vista debe pasar por 3 estados**:
1. Mock puro (estado actual)
2. Mock fallback (si el server falla, muestra mock)
3. Server real (con mock como fallback)

### Próximo artifact recomendado
- **Specs** (`sdd-spec`): definir los contratos TypeScript exactos y los criterios de aceptación por vista.
- **Design** (`sdd-design`): decidir la arquitectura de services/hooks y el patrón de mock fallback.

---

## 12. Artefactos y persistencia

| Artefacto | Ubicación | Estado |
|-----------|-----------|--------|
| Exploración (archivo) | `coppAddresdBack/docs/modules/program-progress/MOBILE-INTEGRATION-EXPLORATION.md` | ✅ Creado |
| Exploración (Engram) | `sdd/program-progress-mobile-integration/explore` | ✅ Persistido |
| Doc de referencia | `coppAddresdBack/docs/modules/program-progress/MOBILE-INTEGRATION.md` | ✅ Pre-existente |
