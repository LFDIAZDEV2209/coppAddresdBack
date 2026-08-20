# Módulo de Pacientes — Plan Maestro de Evolución

Plan aprobado para evolucionar el módulo actual de pacientes hacia un **Patient
Management / Clinical Management Module** de ERP médico (obesidad y prevención),
multi-organización, multi-clínica, multi-sede, con permisos por contexto.

> Fuente del requerimiento: `PROMPTPATIENTS_MANAGEMENT_PLAN.md` (raíz del workspace).
> Este documento es la fuente de verdad del plan; cada fase actualiza su estado.

## Estado

| Fase | Contenido | Estado |
|---|---|---|
| 0 — Discovery | Diagnóstico completo del módulo actual | ✅ Completado |
| 1 — Domain foundation | organizations, clinics, locations, professional types, specialties, employees + extensión professionals, licencias, seeds | ✅ Completado |
| 2 — Authorization con scopes | Tablas auth scoped, introspección, políticas API, switcher de contexto | ✅ Completado |
| 3 — Professional onboarding | Invitaciones, email infra, wizard primer acceso + perfil | ✅ Completado |
| 4 — Patient scoping | clinic_id/location_id, soft delete, created_by/updated_by, filtros por contexto, página de detalle | ✅ Completado |
| 5 — Documents | Repositorio documental + upload UI | ⏳ |
| 6 — Clinical history | encounters, clinical_notes, measurements (IMC derivado), goals | ⏳ |
| 7 — Appointments + Prescriptions reales | Reemplazo de mocks | ⏳ |
| 8 — Bulk operations | Jobs infra, import/export, acciones masivas | ⏳ |
| 9 — Hardening | Auditoría con actor real, índices documentados, tests IDOR, observabilidad | ⏳ |

## Decisiones arquitectónicas clave

| Decisión | Elección | Justificación |
|---|---|---|
| Modelo de autorización | Híbrido: permisos globales en claims JWT + introspección con scope para permisos clínicos | Claims explotan con N clínicas; cache keyed por security_stamp invalida naturalmente |
| Niveles de scope | Global → Organization → Clinic → Location | Clinic = frontera principal; Location justificado por "admin por sede"; Department/CareTeam/Patient diferidos (el modelo los acepta como scope_type) |
| Clinic vs Organization | Entidades separadas | El dominio distingue proveedor (organización) de clínica; una org tiene N clínicas, una clínica N sedes |
| Empleado vs profesional | `employees` = núcleo HR para TODA persona (finanzas, recepción, clínicos); `professionals` = extensión clínica 1:0..1 con `employee_id` | El ERP tiene empleados no clínicos; el "ser profesional" es un rol de datos extensible, no una entidad separada |
| Profesión ≠ especialidad ≠ credencial | 3 entidades: professional_types (catálogo), specialties (catálogo N:N), professional_licenses | Requerimiento explícito; todo catálogo-driven, extensible sin código |
| Jobs de background | `app.background_jobs` + dispatcher propio, interfaz `IJobDispatcher` | Sin infra de jobs hoy; KISS + swap futuro a Hangfire/SQS sin tocar features |
| Email | `IEmailSender` (SMTP dev / AWS SES prod) | No existe infra hoy; SES según skill aws-production |
| Invitaciones | Token hasheado (SHA-256), uso único, expira 72h, revocable, auditable | Nunca enviar passwords por correo |
| Documentos | Metadata en BD + binario en storage (`IObjectStorageService`, hoy Local → S3) | Storage existente con URLs firmadas; proveedor intercambiable |
| IMC y derivados | Derivados en lectura (DTO), no almacenados | Regla de normalización: no columnas calculadas sincronizadas a mano |
| Notas clínicas | Append-only con enmiendas | Estándar de registros médicos |
| Soft delete | Solo datos clínicos/directorio; hard delete administrativo | Trazabilidad y requerimientos PHI |

## Modelo de datos objetivo (resumen)

```
erp.organizations 1─N erp.clinics 1─N erp.locations
erp.employees (núcleo HR) N─1 erp.organizations
erp.professionals 1:0..1 erp.employees (extensión clínica)
erp.professional_types 1─N erp.professionals
erp.professionals N─N erp.specialties (erp.professional_specialties)
erp.employees N─N erp.clinics (erp.employee_clinics)
erp.professionals N─N erp.locations (erp.professional_locations)
erp.professionals 1─N erp.professional_licenses
erp.professional_types N─N erp.specialties (catálogo professional_type_specialties)
erp.employees 1:1 auth.users (nullable hasta invitación)
app.patient_profiles N─1 erp.clinics / erp.locations (clinic_id/location_id nullable — Fase 4, sin backfill del directorio legacy)
auth: user_role_scoped_assignments + user_permission_overrides (Fase 2)
auth.invitations (Fase 3)
app.documents + catálogos (Fase 5)
app.encounters / clinical_notes / patient_measurements / patient_goals (Fase 6)
app.appointments / prescriptions + items (Fase 7)
```

## Fase 4 — Patient scoping (implementada)

Decisiones tomadas en la implementación:

- **Frontera de datos = clínica activa** (`X-Clinic-Id` → `ICurrentContext.ActiveClinicId`). El listado filtra por `clinic_id`; el detalle/update/delete de un paciente de otra clínica responde 404 (no se filtra existencia entre clínicas). Sin contexto activo se ve el directorio completo (contexto global).
- **Permisos por acción**: `PatientsController` usa `HasPermissionAsync("Patients.{View,Create,Update,Delete}")` (claims globales OR introspección scoped) en lugar de `[RequirePermission]`, porque el handler de claims no evalúa permisos scoped de clínica.
- **`clinic_id`/`location_id` nullable**: el directorio importado (~200k registros) no tiene clínica; sin backfill (decisión documentada). La creación asigna la clínica activa del contexto (nunca se acepta del cuerpo); `location_id` queda en el modelo sin exponerlo en la API (el contexto activo de sede no existe aún).
- **Soft delete**: columna `deleted_at` (timestamptz). Listado/detalle excluyen eliminados; `DELETE` solo marca la columna (trazabilidad PHI; las filas hijas se conservan). El chequeo de MRN único excluye eliminados.
- **Auditoría de actor**: `created_by`/`updated_by`/`deleted_by` apuntan a `auth.users` (FK por SQL en la migración, patrón de `user_id`). El actor viene de `ICurrentContext.UserId`; nunca del cuerpo.
- **Índices**: `ix_patient_profiles_clinic_id`, `ix_patient_profiles_location_id`, `ix_patient_profiles_deleted_at` (regla anti-retenancy + FKs indexadas).
- **Frontend**: columna "Clínica" en el listado; la fila navega a la nueva página de detalle `/patients/[id]` (reemplaza el dialog de detalle) con secciones: resumen, datos personales, contacto, cobertura, clínica/sede, estilo de vida, diagnósticos, medicamentos, alergias y signos vitales.

## Reglas de implementación por fase

- Toda tabla clínica/directorio exige `clinic_id` indexado (regla anti-retenancy).
- FKs siempre indexadas (PostgreSQL no las crea automáticamente).
- Migraciones aditivas con backfill documentado; nunca reescribir `patient_profiles` de golpe.
- Cada fase termina con: `dotnet build` + `dotnet test` + migración validada + docs actualizados.
- Comentarios y docs en español.
