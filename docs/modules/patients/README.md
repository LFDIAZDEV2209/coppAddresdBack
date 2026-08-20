# Módulo de Pacientes / Profesionales — Documentación

Estado: **Fases 1, 2, 3 y 4 completadas** (domain foundation + authorization con
scopes + professional onboarding + patient scoping). El plan maestro vive en
[`PLAN.md`](PLAN.md) — leer antes de trabajar en este módulo.

## Alcance actual (Fase 1 + Fase 2 + Fase 3 + Fase 4)

### Estructura organizacional (schema `erp`)

```
organizations 1─N clinics 1─N locations
employees (núcleo HR) N─1 organizations
professionals (extensión clínica) 1:0..1 employees
employees N─N clinics (employee_clinics)
professionals N─N specialties (professional_specialties)
professionals N─N locations (professional_locations)
professionals 1─N professional_licenses
professional_types N─N specialties (professional_type_specialties, catálogo)
```

**Decisión de modelado**: el empleado es la entidad núcleo de RRHH (vale para
finanzas, recepción, administración y clínicos). `professionals` es una
extensión 1:0..1 con `employee_id` que solo existe para personal clínico — el
"ser profesional" es un rol de datos, no una entidad aparte. Los subtipos
futuros de empleado siguen el mismo patrón de extensión.

### Catálogos seedeados (idempotentes, código estable)

- **17 profesiones** (`erp.professional_types`): Physician, NP, PA, RN, LPN,
  Clinical Psychologist, Psychiatrist, LCSW, LPC, RD/RDN, Physical Therapist,
  Exercise Physiologist, Health Coach, Pharmacist, Care Coordinator, CHW,
  Medical Assistant.
- **29 especialidades** (`erp.specialties`) con categoría (Medicina, Nutrición,
  Salud mental, Enfermería, Terapia, Coordinación, Fitness).
- **70 mapeos** profesión → especialidades válidas (`professional_type_specialties`)
  que alimentan filtros de UI y validación de asignaciones.

El seed se genera con `scripts/generate_professional_catalogs_seed.py`
(codegen — editar el script, nunca el SQL generado).

### Vocabularios cerrados (códigos estables en inglés)

`ProfessionalOptions` en `Application/Features/Professionals/`:
- `EmployeeStatuses`: Invited / Active / Inactive
- `AssignmentStatuses`: Active / Inactive
- `LicenseTypes`: StateLicense / BoardCertification / DeaRegistration / Npi /
  CdrLicense / NbcHwcCertification / BlsAcls / Other
- `VerificationStatuses`: Pending / Verified / Expired / Revoked

## Endpoints (todos `[Authorize]`; permisos por scope llegan en Fase 2)

```
GET    /api/v1/organizations/tree        # árbol org → clínicas → sedes
POST   /api/v1/organizations             # crear organización
PUT    /api/v1/organizations/{id}
POST   /api/v1/organizations/clinics
PUT    /api/v1/organizations/clinics/{id}
POST   /api/v1/organizations/locations
PUT    /api/v1/organizations/locations/{id}
GET    /api/v1/professional-types        # catálogo con especialidades válidas
GET    /api/v1/specialties               # catálogo agrupado por categoría
GET    /api/v1/employees                 # lista paginada (search, status, org, clinic)
GET    /api/v1/employees/{id}            # detalle con clínicas + extensión profesional
POST   /api/v1/employees                 # crear (extensión profesional opcional)
PUT    /api/v1/employees/{id}            # PATCH semántico; listas = sync total
```

## Autorización por contexto (Fase 2)

Modelo scoped sobre el Auth Service (ver `PLAN.md` → Decisiones):

```
User
  ├─ Roles globales (claims JWT) → Permissions globales
  └─ Roles scoped (auth."ScopedRoleAssignments") → Permissions por clínica/org
     └─ Overrides Grant/Deny (auth."ScopedPermissionAssignments") — excepciones
```

- **Cadena de scopes**: Clinic → Organization → Global (más específico gana).
  La construye la API (conoce la jerarquía de clínicas) y la envía al Auth.
- **Introspección**: `GET /api/auth/internal/authorize` y
  `/api/auth/internal/scoped-permissions` (header `X-Internal-Key`). La API
  cachea en memoria keyed por `security_stamp`: al cambiar una asignación el
  Auth invalida el stamp → cache miss natural; TTL = vida del token (15 min).
- **Contexto activo**: header `X-Clinic-Id`/`X-Organization-Id` por request;
  `ICurrentContext` combina permiso global (claims) + scoped (introspección).
- **Switcher**: `GET /api/v1/me/context` devuelve org, clínicas (con sedes) y
  permisos efectivos por clínica — alimenta el selector del frontend.
- **Gestión**: `POST/DELETE /api/users/{id}/scoped/roles` y
  `/scoped/permissions` (exigen Roles.Assign / Permissions.Assign).
- **Endpoints de la API** ahora exigen permiso global vía `[RequirePermission]`
  (policy por código): Organizations.*, Clinics.*, Locations.*, Employees.*.

## Onboarding del profesional (Fase 3)

```
Admin crea empleado (mínimo) → POST /api/v1/employees/{id}/invite
  → Auth: usuario sin password + acceso ERP + invitación (token SHA-256, 72h)
  → Correo con enlace /invitaciones?token=... (provider Log en dev / SMTP en prod)
Profesional abre enlace → establece su contraseña (accept, un solo uso)
  → Login → guard redirige a /onboarding (empleado en estado Invited)
  → Wizard: profesión + especialidades + bio + teléfono + licencia
  → PUT /api/v1/me/profile (CompleteOnboarding) → estado Active
```

- El token solo se persiste como hash SHA-256; nunca se envían credenciales por correo.
- `IEmailSender` con providers Log (dev, imprime el correo con el enlace) y
  Smtp (prod); migrar a SES detrás de la misma interfaz.
- El wizard y la edición de perfil usan `/api/v1/me/profile` (autogestión, sin
  exigir el permiso administrativo Employees.Update).
- Catálogos de profesiones/especialidades accesibles a cualquier usuario
  autenticado (datos de referencia para el wizard).
- Reenvío y revocación: `POST /api/invitations/{id}/resend|revoke` (exigen
  Users.Update). Reenviar revoca la pendiente y crea una nueva.

## Reglas del módulo

- **Email único por organización** (índice único `ix_employees_organization_email`);
  el chequeo en update excluye al propio empleado.
- **Update de empleado** = transacción reintentable: update dirigido de scalares +
  sync total de clínicas + reemplazo de extensión profesional (nunca huérfanos).
- **Onboarding**: el administrador crea datos mínimos; el resto lo completa el
  profesional. `OnboardingCompletedAt` marca el fin del wizard (Fase 3).
- **Sin usuario aún**: `employees.user_id` es nullable hasta la invitación
  (Fase 3); hoy los empleados nacen con `Status = Invited`.
- Toda tabla clínica/directorio futura exige `clinic_id` indexado (anti-retenancy).
- FKs siempre indexadas; borrado físico solo administrativo.

## Patient scoping (Fase 4)

- **Frontera de datos por clínica activa** (`X-Clinic-Id`): el listado filtra por
  `clinic_id`; detalle/update/delete de un paciente de otra clínica responde 404
  (sin fuga de existencia entre clínicas). Sin contexto activo se ve el
  directorio completo. `PatientsController` evalúa `Patients.{View,Create,Update,
  Delete}` con `HasPermissionAsync` (claims globales OR introspección scoped) —
  no usa `[RequirePermission]` porque ese handler no resuelve permisos scoped.
- **Soft delete**: `deleted_at` marca el paciente como eliminado; listado y
  detalle lo excluyen, y el chequeo de MRN único ignora eliminados. Nunca se
  borran físicamente los registros clínicos (trazabilidad PHI).
- **Auditoría de actor**: `created_by` / `updated_by` / `deleted_by` apuntan a
  `auth.users` (FK por SQL en la migración, patrón de `user_id`); el actor se
  toma de `ICurrentContext.UserId` — nunca del cuerpo de la petición.
- **`clinic_id`/`location_id` nullable**: el directorio legacy importado (~200k
  pacientes) no tiene clínica; sin backfill (decisión documentada). La creación
  asigna la clínica activa del contexto; `location_id` queda en el modelo sin
  exponerse aún en la API (no existe contexto activo de sede).
- **Endpoints**:
  ```
  GET    /api/v1/patients            # lista paginada filtrada por clínica activa
  GET    /api/v1/patients/{id}       # detalle completo (404 si es de otra clínica)
  POST   /api/v1/patients            # crea en la clínica activa del contexto
  PUT    /api/v1/patients/{id}       # actualiza (registra updated_by)
  DELETE /api/v1/patients/{id}       # soft delete (registra el actor)
  ```
- **Frontend**: columna "Clínica" en el listado; la fila navega a la nueva
  página de detalle `/patients/[id]` (reemplaza el dialog) con secciones de
  resumen, datos personales, contacto, cobertura, clínica/sede, estilo de vida,
  diagnósticos, medicamentos, alergias y signos vitales.

## Tests

- Unit: `tests/CoppAddresd.UnitTests/Features/Professionals/` — vocabularios,
  validadores (create/update), handlers con NSubstitute (email duplicado,
  organización inexistente, extensión profesional, normalización).
- Unit: `tests/CoppAddresd.UnitTests/Features/Patients/PatientScopingTests.cs` —
  asignación de clínica/actor al crear, `updated_by` en update, soft delete con
  actor en delete, filtro por clínica en el listado (con y sin contexto).
- Integración: pendiente — los repositorios nuevos aún no tienen tests contra
  PostgreSQL real (patrón: `COP_TEST_DB_CONNECTION`).
