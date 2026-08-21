# api-gateway-yarp — Exploration

**Cambio**: `api-gateway-yarp` — API Gateway local con YARP + plan de migración a AWS API Gateway.
**Modo de artefactos**: hybrid (OpenSpec + Engram). Engram project: `coppaddresdback`.
**Fecha**: 2026-08-21.

## Estado actual

Hoy el frontend (`coppaddresd-front`) y la app móvil (`antares-paciente`) consumen **tres URLs de backend distintas** apuntando a tres procesos .NET separados:

- Auth Service: `http://localhost:5123` (`NEXT_PUBLIC_AUTH_API_URL`)
- API principal (ERP): `http://localhost:5122` (`NEXT_PUBLIC_API_URL`)
- Telemedicina: `http://localhost:5130` (`NEXT_PUBLIC_TELEMEDICINE_API_URL`)

**No existe** ningún API Gateway, reverse proxy, ni servicio de agregación de rutas en el repo (`grep` por `yarp|nginx|caddy|traefik|haproxy|envoy|api.gateway` no encuentra resultados). Los únicos "proxies" del backend son internos a un mismo proceso (storage S3, AI Service executions, códigos postales).

Cada servicio es un `WebApplication` .NET 10 independiente, con su propio `launchSettings.json`, su propio puerto y su propia base de datos PostgreSQL (un único contenedor `pgvector/pgvector:pg18` con schemas `auth.`, `app.`, `erp.`, `audit.`, `tele.`, `public.`).

## Solución y formato `.slnx`

`coppAddresdBack/CoppAddresd.slnx` es el nuevo formato XML de soluciones (no existe `.sln`). Estructura:

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/CoppAddresd.Api/CoppAddresd.Api.csproj" />
    <Project Path="src/CoppAddresd.Application/CoppAddresd.Application.csproj" />
    <Project Path="src/CoppAddresd.Domain/CoppAddresd.Domain.csproj" />
    <Project Path="src/CoppAddresd.Infrastructure/CoppAddresd.Infrastructure.csproj" />
  </Folder>
  <Folder Name="/src/Services/">
    <Project Path="src/Services/CoppAddresd.Auth/CoppAddresd.Auth.csproj" />
    <Project Path="src/Services/CoppAddresd.Telemedicine/CoppAddresd.Telemedicine.csproj" />
  </Folder>
  <Folder Name="/tests/">
    <Project Path="tests/CoppAddresd.IntegrationTests/CoppAddresd.IntegrationTests.csproj" />
    <Project Path="tests/CoppAddresd.Telemedicine.IntegrationTests/CoppAddresd.Telemedicine.IntegrationTests.csproj" />
    <Project Path="tests/CoppAddresd.Telemedicine.UnitTests/CoppAddresd.Telemedicine.UnitTests.csproj" />
    <Project Path="tests/CoppAddresd.UnitTests/CoppAddresd.UnitTests.csproj" />
  </Folder>
</Solution>
```

**Cómo agregar `CoppAddresd.Gateway` (snippet consistente)**:

```xml
<Solution>
  <Folder Name="/src/Services/">
    <Project Path="src/Services/CoppAddresd.Auth/CoppAddresd.Auth.csproj" />
    <Project Path="src/Services/CoppAddresd.Gateway/CoppAddresd.Gateway.csproj" />
    <Project Path="src/Services/CoppAddresd.Telemedicine/CoppAddresd.Telemedicine.csproj" />
  </Folder>
  ...
</Solution>
```

## Tabla de ruteo (estado actual)

| Prefijo de ruta | Servicio .NET | Puerto http | Puerto https | Notas |
|---|---|---|---|---|
| `/api/auth/*` (login, refresh, logout, change-password, id-lookup, send-otp, verify-otp) | `CoppAddresd.Auth` (AuthController) | 5123 | 7230 | Público; cookies HttpOnly `copp_refresh_token` (`Path=/api/auth`). |
| `/api/me` | `CoppAddresd.Auth` (MeController) | 5123 | 7230 | `[Authorize]`; sesión actual. |
| `/api/users/*` (+ `/scoped/roles`, `/scoped/permissions`) | `CoppAddresd.Auth` (UsersController, ScopedAssignmentsController) | 5123 | 7230 | Permisos granulares `Users.*`. |
| `/api/roles/*` (+ `user/{userId}`) | `CoppAddresd.Auth` (RolesController) | 5123 | 7230 | Permisos `Roles.*`. |
| `/api/permissions/*` (+ `role/{roleId}`, `user/{userId}`, `code/{code}`) | `CoppAddresd.Auth` (PermissionsController) | 5123 | 7230 | Permisos `Permissions.*`. |
| `/api/invitations/*` (validate, accept, resend, revoke) | `CoppAddresd.Auth` (InvitationsController) | 5123 | 7230 | Onboarding profesional; validate/accept públicos. |
| **`/api/auth/internal/*`** (authorize, scoped-permissions, scoped-assignments, invitations) | `CoppAddresd.Auth` (InternalAuthorizationController, InternalScopedAssignmentsController, InvitationsController con `[HttpPost("api/auth/internal/invitations")]`) | 5123 | 7230 | **`[AllowAnonymous] + [RequireInternalKey]` (header `X-Internal-Key`). NO exponer al exterior.** Consumido por Telemedicine/Api vía `BackendServiceSettings`. |
| `/api/v1/chat` (+ `/stream` SSE) | `CoppAddresd.Api` (ChatController) | 5122 | 7258 | MediatR → AI Service. Streaming SSE. |
| `/api/v1/agents/*` (types, knowledge-bases, documents, instances, executions) | `CoppAddresd.Api` (AgentsController) | 5122 | 7258 | Proxy interno al AI Service para executions. |
| `/api/v1/patients/*` | `CoppAddresd.Api` (PatientsController) | 5122 | 7258 | |
| `/api/v1/employees/*` | `CoppAddresd.Api` (EmployeesController) | 5122 | 7258 | |
| `/api/v1/organizations/*` (tree, clinics, locations) | `CoppAddresd.Api` (OrganizationsController) | 5122 | 7258 | |
| `/api/v1/professionals*` | `CoppAddresd.Api` (ProfessionalsController) | 5122 | 7258 | |
| `/api/v1/professional-types`, `/api/v1/specialties`, `/api/v1/professionals-catalog` | `CoppAddresd.Api` (ProfessionalCatalogsController) | 5122 | 7258 | |
| `/api/v1/catalogs/*` (countries, states, cities, postal-codes, blood-types, document-types, icd10-codes, ethnicities, medications, allergens) | `CoppAddresd.Api` (CatalogsController) | 5122 | 7258 | |
| `/api/v1/media/*` (+ upload-intent, sign) | `CoppAddresd.Api` (MediaController) | 5122 | 7258 | S3 / Local proxy. |
| `/api/v1/storage/*` (sign, `{**key}` GET/PUT) | `CoppAddresd.Api` (StorageController) | 5122 | 7258 | Proxy del storage. |
| `/api/v1/documents/*` | `CoppAddresd.Api` (DocumentsController) | 5122 | 7258 | |
| `/api/v1/insurers` | `CoppAddresd.Api` (InsurersController) | 5122 | 7258 | |
| `/api/v1/inventory/*` (entries, exits, movements, products, analytics) | `CoppAddresd.Api` (InventoryControllers) | 5122 | 7258 | |
| `/api/v1/store/*` (items, stats) | `CoppAddresd.Api` (StoreItemsController) | 5122 | 7258 | |
| `/api/v1/legal-documents/*` | `CoppAddresd.Api` (LegalDocumentsController) | 5122 | 7258 | |
| `/api/v1/wellness/*` (nutrition-plans, exercise-routines, routine-assignments) | `CoppAddresd.Api` (WellnessController) | 5122 | 7258 | |
| `/api/v1/me/*` (context, profile) | `CoppAddresd.Api` (MyContextController, MyProfileController) | 5122 | 7258 | |
| **`/api/v1/internal/telemedicine/*`** (professionals/{id}, patients/{id}, specialties/{id}, locations/{id}, by-user/{userId}) | `CoppAddresd.Api` (TelemedicineReferenceController) | 5122 | 7258 | **`[RequireInternalKey]`. NO exponer al exterior.** Consumido por el microservicio Telemedicine (`BackendReferenceDataService`). |
| `/api/v1/telemedicine/*` (requests, appointments, alerts, encounters, sessions, admin/*, me) | `CoppAddresd.Telemedicine` | 5130 | 7130 | Microservicio standalone (Clean Architecture por carpetas). |
| `/api/v1/telemedicine/webhooks/twilio` | `CoppAddresd.Telemedicine` (WebhooksController) | 5130 | 7130 | Firma Twilio; idempotente. |

**Endpoints que NO deben exponerse al exterior** (consumidos internamente vía `X-Internal-Key`):

- `/api/auth/internal/*` (Auth)
- `/api/v1/internal/telemedicine/*` (Api)

**Cookie de refresh**: `copp_refresh_token` — `Path=/api/auth` (Auth Service). Si el gateway rutea `/api/auth/*`, el `Path` se respeta (mismo origen) y la cookie sigue funcionando.

## Consumo de URLs en el frontend

**Archivo único de configuración** (`coppaddresd-front/lib/config/env.ts`):

```ts
export const env = {
  authApiUrl:      process.env.NEXT_PUBLIC_AUTH_API_URL ?? "http://localhost:5123",
  apiUrl:          process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5122",
  telemedicineApiUrl: process.env.NEXT_PUBLIC_TELEMEDICINE_API_URL ?? "http://localhost:5130",
  applicationCode: process.env.NEXT_PUBLIC_APPLICATION_CODE ?? "erp",
  sessionIdleMinutes: Number(process.env.NEXT_PUBLIC_SESSION_IDLE_MINUTES ?? "30"),
} as const;
```

**Puntos de uso** (todos consumen `env.authApiUrl`, `env.apiUrl` o `env.telemedicineApiUrl`):

| Archivo | URLs que usa | Cómo se usa |
|---|---|---|
| `lib/api/http.ts` | `env.authApiUrl` | Único punto que llama `POST /api/auth/refresh` con `credentials: "include"`. **Crítico**: el `refresh` requiere que la cookie `copp_refresh_token` siga siendo SameSite=Lax sobre el mismo dominio → el gateway debe enrutar `/api/auth/*` sin reescribir. |
| `lib/api/auth-service.ts` | `env.authApiUrl` | `POST /api/auth/login`, `GET /api/me`, `POST /api/auth/logout`. |
| `lib/api/invitation-service.ts` | `env.authApiUrl` | `GET /api/invitations/validate?token=…`, `POST /api/invitations/accept`. |
| `lib/api/context-service.ts` | `env.apiUrl` | `GET /api/v1/me/context`, `GET/PUT /api/v1/me/profile`. |
| `lib/config/env.ts` | — | Definición. Es **el único archivo a tocar** para colapsar las 3 URLs en una sola. |
| `features/users/services/users-service.ts` | `env.authApiUrl` | `${env.authApiUrl}/api/users`, `/api/roles/user/{userId}`. |
| `features/roles/services/roles-service.ts` | `env.authApiUrl` | `${env.authApiUrl}/api/roles`. |
| `features/permissions/services/permissions-service.ts` | `env.authApiUrl` | `${env.authApiUrl}/api/permissions`. |
| `features/agents/services/agents-service.ts` | `env.apiUrl` | `${env.apiUrl}/api/v1/agents`. |
| `features/agents/services/chat-service.ts` | `env.apiUrl` | `${env.apiUrl}/api/v1/chat`, `/api/v1/chat/stream` (SSE). |
| `features/agents/services/upload-agent-document.ts` | `env.apiUrl` | `PUT /api/v1/storage/{key}`, `GET /api/v1/storage/sign`. |
| `features/wellness/services/*.ts` | `env.apiUrl` | `/api/v1/wellness/*`, `/api/v1/patients`. |
| `features/patients/services/patients-service.ts` | `env.apiUrl` | `/api/v1/patients`, `/api/v1/insurers`. |
| `features/patients/services/catalogs-service.ts` | `env.apiUrl` | `/api/v1/catalogs`. |
| `features/professionals/services/employees-service.ts` | `env.apiUrl` | `/api/v1/employees`, `/api/v1/organizations/tree`, `/api/v1/professionals`. |
| `features/professionals/services/professional-catalogs-service.ts` | `env.apiUrl` | `/api/v1/professional-types`, `/api/v1/specialties`. |
| `features/inventory/services/inventory-service.ts` | `env.apiUrl` | `/api/v1/inventory`. |
| `features/store/services/store-service.ts` | `env.apiUrl` | `/api/v1/store/items`, `/api/v1/inventory/products`. |
| `features/media/services/media-service.ts` | `env.apiUrl` | `/api/v1/media`, `/api/v1/storage/sign`. |
| `features/media/services/upload-service.ts` | `env.apiUrl` | `/api/v1/media/upload-intent`. |
| `features/telemedicine/services/telemedicine-service.ts` | `env.telemedicineApiUrl` | `${env.telemedicineApiUrl}/api/v1/telemedicine`. |
| `features/telemedicine/services/reference-service.ts` | `env.apiUrl` | `/api/v1/professionals-catalog`, `/api/v1/specialties`, `/api/v1/organizations/tree`, `/api/v1/patients`. |
| `features/agents/hooks/use-agent-knowledge.ts` | indirecto vía `agents-service.ts` | — |
| `features/store/hooks/use-store.ts` | `env.apiUrl` | `/api/v1/inventory/products?pageSize=200`. |
| `features/settings/services/legal-documents-service.ts` | `env.apiUrl` | `/api/v1/legal-documents`. |

**Conclusión frontend**: 24+ archivos consumen las 3 URLs pero **todos pasan por `env`**. Cambiar `lib/config/env.ts` a una única variable (ej. `NEXT_PUBLIC_GATEWAY_URL ?? "http://localhost:5080"`) colapsa el cambio en un solo punto.

## Consumo de URLs en la app móvil

**`antares-paciente/src/utils/authApi.ts`** consume **rutas relativas** (`/api/auth/login`, `/api/auth/id-lookup`, etc.). El proxy se resuelve en `vite.config.ts`:

```ts
// antares-paciente/vite.config.ts
server: {
  host: true,
  port: 5173,
  proxy: {
    '/api/auth': {
      target: 'http://localhost:5123',
      changeOrigin: true,
    },
  },
}
```

**Hoy** la móvil solo proxya `/api/auth/*` hacia Auth (su flujo actual es login OTP). No hay consumo de `apiUrl` ni `telemedicineApiUrl` aún.

**Para colapsar a gateway**: actualizar `vite.config.ts` para que `/api` → `http://localhost:5080` (o el host/puerto del gateway), y eliminar la entrada específica de `/api/auth`. En producción, Capacitor apunta a una URL absoluta (`capacitor.config.ts` + `server.url`) — debe configurarse al dominio del API Gateway / AWS API Gateway.

## docker-compose.yaml

`coppAddresdBack/docker-compose.yaml` define **un solo servicio hoy**: `postgres` (`pgvector/pgvector:pg18`, puerto 5432, BD `coppaddresd`, usuario `app_user`, password `CoppaddresdDev!2026` — **dev only**, debe ir a Secrets Manager en prod). Volumen `pgdata`. Healthcheck `pg_isready`.

Hay un `docker-compose.yaml.bak` (probable respaldo). No hay servicios de los proyectos .NET orquestados todavía.

## Convenciones de docs (repo)

Estructura `coppAddresdBack/docs/` (regla por skill `documentation`):

```
docs/
├── README.md
├── architecture/        # ADRs, decisiones
├── database/            # normalization, indexes, views, triggers, transactions
├── modules/<modulo>/    # una carpeta por módulo
│   ├── README.md
│   ├── architecture.md
│   ├── queries.md
│   ├── database.md
│   └── performance.md
├── performance/         # linq, pagination, n-plus-one, concurrency
└── aws/production.md    # diagrama, recursos, runbook
```

**Reglas**:

- Documentación en **español** (convención del repo). Comentarios de código también.
- Markdown conciso, tablas para datos estructurados.
- "Por qué" no "qué": decisiones y trade-offs, no re-narrar el código.
- Actualizar doc del módulo en la misma tarea que cambia el código.
- Un ADR en `docs/architecture/` por decisión arquitectónica (contexto, decisión, consecuencias).

**Recomendación para la doc del gateway**: crear `docs/architecture/gateway.md` (o `docs/modules/gateway/README.md`) con:

- Diagrama: cliente → YARP (puerto 5080) → Auth (5123) / Api (5122) / Telemedicine (5130).
- Tabla de rutas con match/predicados de YARP (`/api/auth/{**catch-all}` → cluster `auth`, etc.).
- Estrategia para `/api/auth/internal/*` y `/api/v1/internal/*`: bloquear en el gateway o pasar con header `X-Internal-Key` (validar).
- Plan de AWS: mapeo de rutas YARP → API Gateway (resource paths, custom domain, throttling, IAM auth o JWT authorizer con el secret compartido con Auth Service, VPC link si quedan en ECS).

## Endpoints internos que NO deben exponerse al exterior

| Ruta | Servicio | Header de protección | Quién lo consume |
|---|---|---|---|
| `/api/auth/internal/authorize` | Auth | `X-Internal-Key` (`[AllowAnonymous] + [RequireInternalKey]`) | Hoy nadie (planeado para ERP). |
| `/api/auth/internal/scoped-permissions` | Auth | `X-Internal-Key` | Idem. |
| `/api/auth/internal/scoped-assignments` | Auth | `X-Internal-Key` | Idem. |
| `/api/auth/internal/invitations` (POST, revoke) | Auth | `X-Internal-Key` | Idem. |
| `/api/v1/internal/telemedicine/*` (6 endpoints) | Api | `X-Internal-Key` (`[RequireInternalKey]`) | Microservicio Telemedicine (`BackendReferenceDataService` con `BackendServiceSettings.BaseUrl` + `InternalApiKey`). |

**Implicación para el gateway**: estas rutas deben **NO** mapearse al gateway público. Si el gateway tiene que soportarlas (porque Telemedicine corre fuera del VPC en dev), deben filtrarse por path Y conservar la validación `X-Internal-Key`. En AWS API Gateway, esto se resuelve con un resource path separado y un usage plan + API key o un authorizer custom.

## Recomendación de ubicación del proyecto `CoppAddresd.Gateway`

**Path recomendado**: `coppAddresdBack/src/Services/CoppAddresd.Gateway/` (paralelo a `CoppAddresd.Auth` y `CoppAddresd.Telemedicine`).

**Justificación**:

1. Consistencia con el resto: los servicios "que no son Clean Architecture por capas" (Auth, Telemedicine) viven en `src/Services/`. El gateway tampoco comparte Domain/Application/Infrastructure con la API principal.
2. **Standalone**: al igual que Auth y Telemedicine, no debe tener `ProjectReference` a `CoppAddresd.Api`/`Application`/`Domain`/`Infrastructure`. Solo paquetes NuGet (`Yarp.ReverseProxy`, `Microsoft.AspNetCore.Authentication.JwtBearer` si valida tokens en el gateway — opcional, recomendación: **delegar validación a cada backend** para no duplicar el `Issuer/Audience/Secret` y mantener el gateway "tonto"). Ver skill `architecture` § "Auth → cualquier proyecto del repo: nunca (autónomo)".
3. Puerto sugerido local: **5080** (rango libre; actual 5122/5123/5130 están todos ocupados). URL pública del gateway: `http://localhost:5080`.
4. **No exponer** a `localhost:0` ni en producción: en AWS, el gateway vive en su propio servicio ECS con ALB/API Gateway delante (skill `aws-production`).

**Archivos a tocar para apuntar el frontend al gateway**:

1. `coppaddresd-front/lib/config/env.ts` — reemplazar las 3 variables por una sola (`NEXT_PUBLIC_API_URL ?? "http://localhost:5080"` y mantener `apiUrl` como la única usada por todos los servicios).
2. `antares-paciente/vite.config.ts` — proxy genérico `/api` → `http://localhost:5080`.
3. `antares-paciente/capacitor.config.ts` — `server.url` apuntando al gateway público en prod.

**Archivos a NO tocar** (los servicios backend mantienen su contrato):

- `src/Services/CoppAddresd.Auth/**` (sigue escuchando en 5123).
- `src/CoppAddresd.Api/**` (sigue escuchando en 5122).
- `src/Services/CoppAddresd.Telemedicine/**` (sigue escuchando en 5130).

El gateway es aditivo, no rompe a los servicios.

## Aproximaciones consideradas

1. **YARP standalone (recomendada)**: nuevo proyecto `src/Services/CoppAddresd.Gateway`, paquete `Yarp.ReverseProxy`, configuración en `appsettings.json` con `ReverseProxy:Routes` + `Clusters`. Pros: contrato YARP ≈ AWS API Gateway (path-based routing, transforms, headers, health checks, rate limiting), migración casi 1:1. Cons: añadir un proceso más a levantar en dev (5 en vez de 4: Api, Auth, Telemedicine, Gateway, postgres). Esfuerzo: medio.
2. **YARP dentro de `CoppAddresd.Api`**: embeber el gateway en la API principal. Pros: un proceso menos. Cons: rompe la autonomía del gateway (la API principal re-empezaría a conocer rutas de Auth/Telemedicine), la migración a AWS API Gateway ya no es 1:1, y la skill `architecture` prohíbe que un proyecto de Clean Architecture referencie servicios. Descartada.
3. **Nginx / Caddy / Traefik**: reverse proxy externo. Pros: battle-tested, declarativo. Cons: requiere binario adicional fuera del ecosistema .NET, dificulta "todo en una solución" y la migración a AWS API Gateway (otro modelo mental). Descartada para esta fase; **mantener como opción** si YARP muestra límites (SSE streaming, websockets).
4. **Solo AWS API Gateway sin gateway local**: el frontend habla con 3 URLs también en dev. Pros: nada que construir. Cons: el desarrollo local diverge de producción; las pruebas E2E con mocks/AI service no se pueden hacer contra API Gateway; rompe el principio de "dev = prod". Descartada.

## Riesgos

1. **CORS**: hoy cada backend tiene su propio `Cors:Origins` (default `http://localhost:3000`, con `AllowCredentials` para Auth). Si el gateway queda en `localhost:5080`, los 3 backends deben incluir `http://localhost:5080` en su whitelist, o el gateway debe manejar CORS y los backends relajarse a "confiar en gateway". Decisión de seguridad a documentar.
2. **SSE streaming** (`POST /api/v1/chat/stream`): YARP soporta streaming, pero hay que validar buffers/timeouts para no cortar el stream. AWS API Gateway tiene límites de integración (29 s por respuesta no-streaming; streaming con `application/x-amz-json-1.1` o `text/event-stream` requiere `payload format 2.0` o uso de VPC Link/ALB). Plan de AWS: probablemente **AWS ALB detrás de ECS** en vez de API Gateway para endpoints streaming.
3. **Cookies HttpOnly (`copp_refresh_token`)**: el `Path=/api/auth` y `SameSite=Lax` requieren que el navegador vea el response del **mismo origen** del Auth. Si el gateway reescribe paths pero mantiene el dominio, no hay problema. Si el gateway vive en otro subdominio (`gateway.coppaddresd.com` vs `auth.coppaddresd.com`), la cookie NO viaja. **Decisión**: una sola URL pública para todo (`api.coppaddresd.com` o `gateway.coppaddresd.com` con path-based routing).
4. **Webhooks Twilio** (`POST /api/v1/telemedicine/webhooks/twilio`): Twilio manda el webhook a una URL pública. Hoy es al Telemedicine Service. Si se enruta por el gateway, el gateway debe preservar la firma Twilio y NO añadir headers que invaliden la validación.
5. **Storage presigned URLs**: cuando `Storage:Provider=S3`, el backend devuelve URLs presigned a S3 (`cooppadresd-storage-prod`). Esas URLs van directo del navegador a S3 — **NO pasan por el gateway**. No hay riesgo, pero documentar para no introducir el gateway "para todo" y romper S3.
6. **`BackendServiceSettings.BaseUrl` en Telemedicine**: hoy apunta a `http://localhost:5122` para llamar `/api/v1/internal/telemedicine/*`. Si el gateway existe y los internal endpoints se enrutan, hay que decidir si Telemedicine sigue hablando con `Api` directo (recomendado: **sí**, servicios internos no pasan por el gateway público; el gateway es para tráfico de cliente).

## Listo para propuesta

Sí. La exploración cubre:

- Lista completa de rutas y prefijos (ruteo).
- Formato `.slnx` con snippet para `CoppAddresd.Gateway`.
- Puntos exactos de consumo de URLs en el frontend (un solo archivo `env.ts` + 24 servicios que pasan por `env`).
- Puntos de config en la móvil (`vite.config.ts` + `capacitor.config.ts`).
- Convenciones de docs (`docs/architecture/` para ADRs, español).
- Recomendación de ubicación + archivos a tocar.
- 4 aproximaciones evaluadas, recomendación justificada.
- 6 riesgos identificados (CORS, SSE, cookies, webhooks, storage, servicios internos).

Próximo paso (propuesta `sdd-propose`): redactar `proposal.md` con el alcance del cambio "YARP gateway local + plan AWS API Gateway", `requirements.md` con REQ- (rutas, headers preservados, internals bloqueados, CORS, SSE, plan AWS), `design.md` con la tabla de clusters/routes de YARP y el mapping a AWS, y `tasks.md` por fases.
