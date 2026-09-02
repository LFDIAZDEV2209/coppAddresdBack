# Diseño — Integración móvil del Progreso del Programa

## Enfoque técnico

La app `antares-paciente` conservará su máquina de navegación en `AppContext`, su UI actual y sus componentes Ionic, pero incorporará una frontera de datos explícita: `apiClient` → servicios del programa → hooks de TanStack Query → vistas. El backend sigue siendo la autoridad para identidad, fechas locales, tareas, XP, rachas y estados clínicos. Los mocks solo serán una degradación de lectura para demo cuando el fallo sea de transporte o servidor; nunca ocultarán errores de negocio.

Hay dos correcciones obligatorias frente a documentación antigua: la ruta autoritativa es `POST /api/v1/program/enrollments/me`, que actualmente responde `200` al crear y `409 PATIENT_ALREADY_ENROLLED` si ya existe una activa; y un replay de `clientRequestId` devuelve el mismo cuerpo sin XP adicional. No se debe tratar el `409` como una respuesta que contiene la inscripción existente.

## Decisiones de arquitectura

| Decisión | Elección | Alternativa rechazada y motivo |
|---|---|---|
| Transporte | `apiClient.ts` será el único wrapper de `fetch`; los servicios no conocerán tokens ni refresh. | `fetch` directo en cada hook: duplica errores, timeouts y carreras de refresh. |
| Estado remoto | TanStack Query con claves centralizadas, cache stale y mutaciones optimistas. | `useEffect` + estado local: no resuelve bien cache offline, invalidación ni rollback. |
| Estado de navegación | Mantener `AppContext` y el router por `flow/screen`; durante la migración solo sus campos de programa pasan a ser legacy. | Introducir `IonReactRouter`/`IonRouterOutlet`: cambia una arquitectura expresamente fuera de alcance. |
| Contratos | Tipos wire reflejan literalmente los DTO actuales; fechas se transportan como strings. | Reutilizar `ProgramTaskId` o inventar un DTO móvil: perdería campos, nulabilidad y nombres `snake_case` de scores. |
| Recompensa | Optimismo visual local, reconciliación siempre desde la respuesta y posterior snapshot. | Sumar XP “definitiva” en el cliente: multiplicadores, bonus y topes solo los conoce el servidor. |

## Arquitectura y flujo de datos

```text
ProgramPage
   │ hooks
   ▼
TanStack Query ──→ program/tasks/scores/nutrition-service
   │                         │
   │ cache + rollback         ▼
   └────────────────────── apiClient
                              │ Bearer / refresh
                              ▼
             API .NET /api/v1/program/*
```

### Carga inicial de `ProgramPage`

1. `useProgram()` consulta `GET /api/v1/program/me/snapshot` sin `patientId` ni `enrollmentId`; el backend los resuelve desde el JWT. `QueryClientProvider` debe envolver el `IonApp` en `App.tsx`, con un `QueryClient` estable.
2. `200`: el snapshot alimenta header, tareas, XP, racha y semana. `todayTasks` es el catálogo renderizable; la vista no reconstruye las seis tareas desde mocks.
3. `404 NO_ACTIVE_ENROLLMENT`: ejecutar una sola auto-inscripción por montaje con `timezone = Intl.DateTimeFormat().resolvedOptions().timeZone` y `{ patientId: null, templateId: null, timezone }`. Aceptar `200` del endpoint y refetchear snapshot.
4. Si el POST devuelve `409 PATIENT_ALREADY_ENROLLED` (carrera entre dispositivos/montajes), ignorar el conflicto, no leer su cuerpo como enrollment y refetchear snapshot una vez. Si el refetch aún es `404`, mostrar estado recuperable y no entrar en bucle.
5. Plantilla no publicada: el código verificado actual es `TEMPLATE_NOT_ACTIVE` mapeado a `409`; el contrato nuevo prevé `422`. Ambos deben producir “Tu programa no está listo aún. Contacta a tu equipo de salud.” y no reintentarse en el mismo montaje.
6. La UI debe soportar `Active`, `Paused`, `Withdrawn` y “sin plantilla” como estados de producto, no como la palabra “inscripción”. Sin embargo, el DTO actual de snapshot no contiene `enrollmentStatus` y `ResolveActiveEnrollmentIdAsync` filtra solo `Active`; por eso un programa pausado hoy puede verse como `404`, igual que uno inexistente. Este desfase es un gate de contrato antes del rollout: no implementar auto-inscripción ciega para un `404` hasta confirmar cómo el backend expone `Paused/Withdrawn` en el entorno objetivo. El cliente reconocerá códigos explícitos futuros (`ENROLLMENT_PAUSED`, `ENROLLMENT_WITHDRAWN`) sin inventar campos wire.

Las lecturas `calendar` (rango local de hasta 92 días), `path` y `scores` se habilitan después de disponer del snapshot. `scores` usa `health_score` y `transformation_score` (nombres `snake_case` explícitos). El DTO actual no expone `calculatedAt`; la fecha de cálculo no puede inventarse. Mostrarla requiere confirmar un campo/endpoint backend o dejar una etiqueta de frescura basada solo en el header existente.

### Mutaciones y reconciliación

`useCompleteTask()` recibe el `taskCode` de la tarea del snapshot y toma de ese mismo snapshot `enrollmentId` y `todayLocalDate`; nunca usa IDs o fecha introducidos por la UI. Genera un UUID una sola vez antes del envío, persiste la solicitud completa si queda offline y reutiliza esa clave en todos los retries.

```ts
type CompleteTaskInput = {
  enrollmentId: string; localDate: string; taskCode: TaskCode
  clientRequestId: string; clientCompletedAt?: string
  moodScore?: number; barriers?: string; contentFingerprint?: string
}
```

En `onMutate`: cancelar queries de programa, guardar snapshots previos, marcar la tarea como completada y aplicar una estimación de XP/puntos (y de racha solo cuando el umbral local sea determinable). Disparar confeti/popup una sola vez por `clientRequestId`, antes de la respuesta. En `onError`, restaurar todos los caches modificados. En `onSuccess`, usar `xpBalanceAfter`, `streakCurrent`, `dayPoints` y `dailyBonusAwarded` de la respuesta; en `onSettled`, invalidar snapshot, path y calendario. El cuerpo de replay es idéntico al de creación y no incluye una bandera `replayed`, así que la deduplicación de animación debe ser local por clave, marcada antes del primer envío.

`TASK_NOT_SCHEDULED` y `DATE_OUTSIDE_ACTIVE_WEEK` no se reintentan: invalidan snapshot. `ENROLLMENT_INACTIVE` tampoco se encola; invalida snapshot y muestra el estado pausado cuando el contrato lo permita. `IDEMPOTENCY_KEY_REUSED` genera una clave nueva y reintenta una sola vez con la misma acción; un segundo fallo termina la operación. `useNutritionLog()` trata `409 HABIT_ALREADY_LOGGED` como “ya registrado”, sin XP adicional, revierte el optimismo y refresca el estado; el log granular no completa automáticamente la tarea `nut`.

La cola offline se limitará a 50 entradas FIFO por usuario, con límite de tamaño total y payloads sin PHI. Solo se encolan timeout/network/5xx; 4xx de negocio se eliminan y se traducen por código. Un listener `online` y el resume de la app reproducen en orden con la misma clave. Tras cada lote exitoso se invalidan todas las queries de programa. Una entrada que sigue fallando por red permanece; una que falla por regla de negocio se descarta con aviso. No se implementa sincronización background nativa en esta fase.

## Cliente API, seguridad y configuración

`apiClient.ts` leerá `copp_access_token` mediante `getAccessToken()` de `authApi.ts` y enviará `Authorization: Bearer`. Todas las llamadas usarán `credentials: 'include'`, necesario para la cookie HttpOnly `copp_refresh_token`. Ante `401`, un único `refreshPromise` compartido llamará `POST /api/auth/refresh` sin retry ni Bearer; todas las requests concurrentes esperan el mismo resultado y luego cada una reintenta su request original exactamente una vez. Nunca se reintenta `/refresh` ni se recurre a bucles.

Cada operación tendrá `AbortController`, señal externa de Query y timeout configurable de 15 segundos. Un `AbortError` por timeout se convierte en `ApiError` de tipo `timeout`; desconexión en `network`. Las respuestas 204 se aceptan y los JSON se validan superficialmente. El parser acepta RFC 7807 (`detail`, `title`, `status`, `correlationId`, `errors`) y el fallback actual `{ message }`; extrae un código inicial como `NO_ACTIVE_ENROLLMENT:` cuando no existe propiedad `code`. `ApiError` conservará `status`, `title`, `detail`, `code`, `correlationId` y errores de validación, pero la UX usará una whitelist de mensajes por código, nunca el `detail` crudo.

Si refresh devuelve `X-Refresh-Status: invalid`, limpiar token/cookie local y notificar a `AppContext` mediante un callback/evento sin llamar logout de forma recursiva; volver a `flow = login`, `screen = home`. `missing` no debe presentarse como “sesión expirada”. No se registran tokens, cookies, nombres ni valores clínicos.

En desarrollo, `vite.config.ts` debe mantener `/api/auth → http://localhost:5123` y agregar `/api/v1 → http://localhost:5122`. En navegador se usa base relativa por defecto. En Capacitor nativo no existe el proxy: `VITE_API_BASE_URL` y, si fuera necesario, `VITE_AUTH_BASE_URL` deben apuntar a HTTPS público; preferentemente ambos bajo el mismo gateway/origen para que la cookie `Path=/api/auth`, `SameSite=Lax` y CORS funcionen. Nunca hardcodear URLs productivas ni secretos. La transición a Secure Storage queda fuera de este cambio.

## Tipos y límites de validación

`types.ts` debe modelar `ProgramSnapshotDto`, `TodayTaskDto`, `TodayTaskContentDto`, `XpInfoDto`, `StreakInfoDto`, `NbNextMilestoneDto`, `ProgramCalendarDto`, `ProgramPathDto`, `CompleteTaskResponseDto`, `ProgramEnrollmentDto`, `ScoresResponseDto`, `HealthScoreDto`, `TransformationScoreDto`, `IndicatorDetailDto` y `NutritionLogResultDto`. `DateOnly` será `YYYY-MM-DD`; `DateTime` será ISO string nullable. `TaskCode` será la unión exacta de `podcast | vitals | nut | ejercicio | nutraceutico | emocional`; estados conocidos serán unions de strings, con guardas para valores desconocidos.

Los campos aditivos (`streakMinTasks`, `essentialTaskCodes`, `nb*`, `multiplier*`, `contentUnavailable`, contenido nullable) se leerán con defaults seguros. JSON desconocido se ignora. La validación de formato en el borde comprueba códigos, UUID, rango de mood `1..5`, fechas ISO, ventana de calendario y `mealCode` (`des|alm|mer|cen|agua`); las reglas de autorización, agenda, zona local, fingerprint, XP y concurrencia quedan en el servidor. Nunca convertir una fecha local con `new Date('YYYY-MM-DD')`; usar strings y utilidades locales existentes. `completedAt`/`multiplierEndsAt` se formatean como instantes con timezone del dispositivo solo para presentación.

## Query keys, defaults e invalidación

```text
['program', 'snapshot']
['program', 'calendar', from, to]
['program', 'path']
['program', 'scores']
```

Defaults: `staleTime: 5 min`, `retry: 2` solo para fallos transport/server, `refetchOnWindowFocus: true`, `networkMode: 'online'` para no enviar automáticamente una mutación offline. No se debe convertir un 404 semántico en mock. Matriz: completar/registrar nutrición invalida `snapshot`, `path`, `calendar` y `scores` cuando el cambio pueda alterar XP; enrollment invalida todo; focus/reconnect refresca datos stale. Si hay cache stale se muestra primero; si no existe y el fallo es elegible, se usa el mock con marcador `// TODO: Remove mock fallback`. No se muestra toast por fallos de query.

## Responsabilidades y paralelismo

| Workstream | Archivos | Dependencia |
|---|---|---|
| A — contrato/infraestructura | `apiClient.ts`, `authApi.ts`, `vite.config.ts`, `types.ts` | Gate de estados/status codes; A y tipos pueden avanzar en paralelo. |
| B — transporte de dominio | cuatro `services/program/*`, `offline-queue.ts` | A + tipos; cada servicio puede trabajarse en paralelo. |
| C — cache | `App.tsx`, seis hooks, query keys | B para los hooks; provider y defaults pueden prepararse con A. |
| D — puente de estado | `AppContext.tsx`, `data/program.ts` | C; conservar acciones no relacionadas. |
| E — UI | `ProgramPage`, `TodayView`, `StreakView`, `EvolutionView`, `Lessons`, `NutritionPage` | C/D; secuenciar header → Hoy → Racha → Evolución → lecciones/nutrición. |

## Integración visual Ionic

Conservar `Screen`, `Scroll`, `IonSegment`, `IonSegmentButton`, `IonModal`, `IonButton`, `IonProgressBar` y las animaciones actuales; no introducir router ni rediseñar cards. Añadir estados de carga, vacío, error recuperable y pausado usando `IonLoading`/`IonSpinner`, `IonToast` y botones Ionic existentes. Los nodos del sendero y chips de dominio pueden conservar su CSS propietario para evitar regresión visual; las nuevas acciones generales deben usar `IonButton`. `TodayView` renderiza exactamente los nodos `Locked/Active/Completed` de `path`; `StreakView` usa `days[].isPerfectDay`; `EvolutionView` etiqueta ambos valores como “Índice”, no XP, y conserva pilares/filas solo cuando el DTO los provea. `AppContext.completeStep` queda como wrapper legacy que delega al hook; ningún campo local debe ganar a una respuesta del servidor.

## Tabla de cambios

### `antares-paciente` (implementación futura)

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `package.json`, `package-lock.json` | Modificar | Agregar `@tanstack/react-query`. |
| `src/utils/apiClient.ts` | Crear | Bearer, refresh single-flight, timeout, ProblemDetails, base URL. |
| `src/services/program/{types,program-service,tasks-service,scores-service,nutrition-service,offline-queue}.ts` | Crear | DTOs, endpoints y cola; sin lógica de vista. |
| `src/hooks/useProgram*.ts`, `useCompleteTask.ts`, `useNutritionLog.ts` | Crear | Queries, fallback elegible, mutaciones, optimistic update e invalidación. |
| `src/App.tsx`, `src/utils/authApi.ts`, `vite.config.ts` | Modificar | Provider, puente de sesión y proxies/base URLs. |
| `src/context/AppContext.tsx`, `src/data/program.ts` | Modificar | Wrapper legacy y mocks marcados como fallback. |
| `src/pages/ProgramPage.tsx`, `src/pages/program/{TodayView,StreakView,EvolutionView,Lessons}.tsx`, `src/pages/NutritionPage.tsx` | Modificar | Wiring sin alterar navegación ni diseño. |

### `coppAddresdBack` (solo documentación en esta fase)

| Archivo | Acción | Responsabilidad |
|---|---|---|
| `docs/modules/program-progress/MOBILE-INTEGRATION-DESIGN.md` | Crear | Este diseño, gates de contrato y coordinación de workstreams. |
| Código, migraciones y demás docs | Sin cambios | Las migraciones se consideran aplicadas; `dotnet ef database update` solo es fallback operativo si evidencia concreta muestra tablas ausentes. |

## Estrategia de pruebas y rollout

Unitarias: parser `ApiError`, timeout/abort, construcción de fechas, guardas DTO, claves, cola FIFO y deduplicación de animación. Servicios: MSW/fetch mock para rutas, bodies, `credentials`, Bearer, `200/404/409/422/503` y nombres de scores. Hooks: auto-enrollment, carrera `409`, rollback, invalidación, replay y no-encolado de errores de negocio. Componentes: cada vista con server, cache stale, mock fallback, loading, estado vacío, pausado y error recuperable; emoción exige `moodScore`. Integración: Vite proxy con API/Auth reales y verificación de que no hay llamada al `ai-service`. E2E: login → snapshot, 404 → enroll → refetch, completar → refrescar y conservar XP, duplicar tarea/comida, cortar red → cola → reconectar, y validar `Locked/Active/Completed`.

Verificación móvil: `npm run build` y `npm run lint` dentro de `antares-paciente`; smoke con `npm run dev` y Network tab. No ejecutar cambios de backend ni migraciones en esta fase. Si la API evidencia tablas inexistentes, detener el flujo y operar `dotnet ef database update` con el proyecto/startup documentados por `coppAddresdBack`, nunca como paso automático.

Feature safety: habilitar por `VITE_PROGRAM_API_ENABLED`, con mocks como continuidad cuando esté deshabilitado o haya fallo elegible. Telemetría mínima registra endpoint, status, código, correlation ID, latencia, fallback y tamaño de cola, sin tokens ni PHI. Recuperación: refresh inválido vuelve a login; carrera de enrollment refetchea; 404/paused y plantilla ausente muestran CTA de reintento/contacto; cola persistente conserva acciones de red y reconcilia desde servidor.

## Preguntas abiertas / gates

- [ ] Confirmar un contrato observable para distinguir `Paused` y `Withdrawn` de `NO_ACTIVE_ENROLLMENT`; el código actual filtra `Active` y el snapshot no tiene `enrollmentStatus`.
- [ ] Alinear `TEMPLATE_NOT_ACTIVE` actual (`409`) con el `422` descrito por la especificación; el cliente manejará ambos sin retry.
- [ ] Confirmar cómo exponer `calculatedAt` para cumplir la fecha visible de scores; no inventar ese campo en TypeScript.
- [ ] Validar refresh cookie en una build Capacitor HTTPS real; el proxy de Vite solo prueba navegador.
- [ ] Resolver si el programa debe quedar habilitado por variable de build o por configuración remota antes de producción.
