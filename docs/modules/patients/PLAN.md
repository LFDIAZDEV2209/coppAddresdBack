# Módulo de Pacientes — Plan Maestro de Evolución

Plan aprobado para evolucionar el módulo actual de pacientes hacia un **Patient
Management / Clinical Management Module** de ERP médico (obesidad y prevención),
multi-organización, multi-clínica, multi-sede, con permisos por contexto.

> Fuente del requerimiento: `PROMPTPATIENTS_MANAGEMENT_PLAN.md` (raíz del workspace).
> Este documento es la fuente de verdad del plan; cada fase actualiza su estado.
> Comentarios y docs en español (convención del repo).

## Estado

| Fase | Contenido | Estado |
|---|---|---|
| 0 — Discovery | Diagnóstico completo del módulo actual | ✅ Completado |
| 1 — Domain foundation | organizations, clinics, locations, professional types, specialties, employees + extensión professionals, licencias, seeds | ✅ Completado |
| 2 — Authorization con scopes | Tablas auth scoped, introspección, políticas API, switcher de contexto | ✅ Completado (backend) — UX de gestión pendiente |
| 3 — Professional onboarding | Invitaciones, email infra, wizard primer acceso + perfil | ✅ Completado (backend + wizard) — UX admin pendiente |
| 4 — Patient scoping | clinic_id/location_id, soft delete, created_by/updated_by, filtros por contexto, página de detalle | ✅ Completado |
| 5 — Documents | Repositorio documental + upload UI + **storage S3** | ✅ Backend + S3 completo — tab de documentos en frontend pendiente |
| 6 — Clinical history | encounters, clinical_notes, measurements (IMC derivado), goals | ⏳ |
| 7 — Appointments + Prescriptions reales | Reemplazo de mocks | ⏳ |
| 8 — Bulk operations | Jobs infra, import/export, acciones masivas (pacientes y profesionales) | ⏳ |
| 9 — Hardening | Auditoría con actor real, índices documentados, tests IDOR, observabilidad | ⏳ |

## Decisiones arquitectónicas clave

| Decisión | Elección | Justificación |
|---|---|---|
| Modelo de autorización | Híbrido: permisos globales en claims JWT + introspección con scope para permisos clínicos | Claims explotan con N clínicas; cache keyed por security_stamp invalida naturalmente |
| Niveles de scope | Global → Organization → Clinic → Location | Clinic = frontera principal; Location justificado por "admin por sede"; Department/CareTeam/Patient diferidos (el modelo los acepta como scope_type) |
| Clinic vs Organization | Entidades separadas | El dominio distingue proveedor (organización) de clínica; una org tiene N clínicas, una clínica N sedes |
| Empleado vs profesional | `employees` = núcleo HR para TODA persona (finanzas, recepción, clínicos); `professionals` = extensión clínica 1:0..1 con `employee_id` | El ERP tiene empleados no clínicos; el "ser profesional" es un rol de datos extensible, no una entidad separada |
| Profesión ≠ especialidad ≠ credencial | 3 entidades: professional_types (catálogo), specialties (catálogo N:N), professional_licenses | Requerimiento explícito; todo catálogo-driven, extensible sin código |
| Catálogo de especialidades US | 17 professional_types + 29 specialties agrupadas (Medicina, Nutrición, Salud mental, Enfermería, Terapia, Coordinación, Fitness) con mapeo N:N por profesión | Lista profesional del mercado de EE. UU. (ABOM, RDN, LCSW, NBC-HWC...); agregar nuevas es dato, no código |
| Jobs de background | `app.background_jobs` + dispatcher propio, interfaz `IJobDispatcher` | Sin infra de jobs hoy; KISS + swap futuro a Hangfire/SQS sin tocar features |
| Email | `IEmailSender` (SMTP dev / AWS SES prod) | No existe infra hoy; SES según skill aws-production |
| Invitaciones | Token hasheado (SHA-256), uso único, expira 72h, revocable, auditable | Nunca enviar passwords por correo |
| Documentos | Metadata en BD + binario en storage (`IObjectStorageService`, Local dev / **S3 prod**) | Storage intercambiable por configuración; upload-intent devuelve presigned PUT URL real con S3 (subida directa del navegador al bucket) o el proxy del backend con Local |
| Storage S3 | Provider `S3` con `AWSSDK.S3` v4; credenciales por cadena por defecto del SDK (IAM role `cooppadresd-ec2-s3-access-role`, bucket `cooppadresd-storage-prod` en `us-east-2`) | Regla de oro: jamás Access Keys en código/config; el navegador sube/descarga directo al bucket con presigned URLs SigV4 |
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
erp.employees N─N erp.clinics (erp.employee_clinics)  ← asignación + rol/permisos scoped por clínica
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
app.background_jobs (+ colas) (Fase 8)
```

---

## Fase 4 — Patient scoping (implementada)

- **Frontera de datos = clínica activa** (`X-Clinic-Id` → `ICurrentContext.ActiveClinicId`). El listado filtra por `clinic_id`; el detalle/update/delete de un paciente de otra clínica responde 404 (no se filtra existencia entre clínicas). Sin contexto activo se ve el directorio completo (contexto global).
- **Permisos por acción**: `PatientsController` usa `HasPermissionAsync("Patients.{View,Create,Update,Delete}")` (claims globales OR introspección scoped) en lugar de `[RequirePermission]`, porque el handler de claims no evalúa permisos scoped de clínica.
- **`clinic_id`/`location_id` nullable**: el directorio importado (~200k registros) no tiene clínica; sin backfill (decisión documentada). La creación asigna la clínica activa del contexto (nunca se acepta del cuerpo).
- **Soft delete**: columna `deleted_at` (timestamptz). Listado/detalle excluyen eliminados; `DELETE` solo marca la columna (trazabilidad PHI). El chequeo de MRN único excluye eliminados.
- **Auditoría de actor**: `created_by`/`updated_by`/`deleted_by` apuntan a `auth.users` (FK por SQL). El actor viene de `ICurrentContext.UserId`; nunca del cuerpo.
- **Índices**: `ix_patient_profiles_clinic_id`, `ix_patient_profiles_location_id`, `ix_patient_profiles_deleted_at`.

## Fase 5 — Documents + S3 (backend implementado)

**Metadata en BD + binario en storage** (patrón ya usado por Media). El frontend nunca toca la BD: `upload-intent` valida paciente/tipo/extensión ANTES de subir, devuelve la clave (`documents/{patientId}/{guid}{ext}`) + URL de escritura (PUT), y `POST /documents` registra la metadata.

- **Tablas** (`app.`): `documents` (root + versiones), `document_categories` (9), `clinical_document_types` (29, catálogo con extensiones permitidas). Migración `20260820140637_AddPatientDocuments`. Seeding idempotente por script generado (`scripts/generate_document_catalogs_seed.py` → `AddDocumentCatalogs.sql`, recurso embebido).
- **Versionado**: un documento es una familia root (v1) + versiones hijas (`parent_document_id`). `GET /documents/{id}/versions` devuelve root + hijas. La clínica/paciente de una versión se hereda del root. Estado: `Draft/Ready/Archived`; `Update` solo metadata (el binario es inmutable — una nueva subida = nueva versión).
- **Permisos**: `Documents.View/Upload/Update/Delete`; frontera de datos = misma regla Fase 4.
- **Storage S3 (nuevo, implementado hoy)**: `S3ObjectStorageService` (`AWSSDK.S3` v4) registrado con `Storage:Provider=S3`. El contrato `IObjectStorageService` se amplió con `IsCloudStorage` y `GetPreSignedUploadUrlAsync`. Con S3 el `upload-intent`/`download` devuelven presigned URLs reales del bucket; con Local siguen usando el proxy del backend (`StorageSignatureService` HMAC). El `Content-Type` solo se firma en el PUT presigned cuando el llamador lo envía (evita mismatch de firma). Config por `appsettings` (gitignoreado) con fallback a variables `AWS_REGION`/`AWS_S3_BUCKET`/`AWS_S3_BUCKET_ARN`/`AWS_S3_OBJECT_ARN`/`AWS_S3_IAM_ROLE`. Ver `docs/modules/storage/README.md`.
- **Frontend**: `uploadToPresignedUrl` solo adjunta `Authorization: Bearer` cuando la URL apunta al backend (con S3 un header de autorización rompería la firma SigV4).
- **Pendiente**: tab de documentos en el detalle de paciente (upload con progreso + listado + versionado + descarga), siguiendo el patrón de `media-page`.

## Fase 3 — Professional onboarding (backend implementado; UX admin pendiente)

Flujo ya construido en el backend: creación de empleado → invitación (`POST /employees/{id}/invite` → usuario en Auth Service + token hasheado, uso único, 72h) → `GET /invitations/validate` + `POST /invitations/accept` (primer acceso: password propio) → wizard de perfil (`/onboarding` frontend) que completa profesión, especialidades, licencias, bio, foto (storage) y `onboarding_completed_at`.

**Pendiente para el objetivo profesional completo:**
- **UX de creación admin**: formulario de creación de profesional con pasos (datos mínimos → organización/clínicas/sedes → tipo de profesional + especialidades → rol por clínica con scope → permisos excepcionales → invitación). Hoy no existe UI de creación en el frontend (`/employees` solo lista e invita).
- **Gestión de scopes por clínica**: UI para asignar rol A en Clínica A y rol/permisos distintos en Clínica B (tabla por clínica en el detalle del empleado).
- **Auto-gestión**: el perfil del profesional debe ser editable por él mismo (mi perfil), no solo el wizard inicial.

---

## Fases pendientes — plan de implementación

### Fase 6 — Clinical history (obesidad y prevención)

Base del valor clínico del ERP. **No implementar entidades por moda**: solo lo que el dominio exige, normalizado.

1. **Dominio** (`app.` schema, regla anti-retenancy: toda tabla con `clinic_id` indexado, heredado del paciente/contexto):
   - `app.encounters` — visita/consulta del paciente (encounter_date, encounter_type [Initial, FollowUp, Telehealth...], provider = professional, status, clinic_id/location_id). N:1 patient_profiles, N:1 professionals (vía employee), notas asociadas.
   - `app.clinical_notes` — nota clínica **append-only con enmiendas** (note_type [SOAP, Progress, Nurse...], content, authored_by, superseded_by para enmiendas). N:1 encounter (opcional), N:1 patient.
   - `app.patient_measurements` — mediciones objetivas (measured_at, weight_kg, height_cm, bmi_derivado **en lectura**, waist_cm, blood_pressure, body_fat_pct, heart_rate...). **IMC no se almacena**: se calcula en el DTO desde weight/height (regla de normalización). N:1 patient.
   - `app.patient_goals` — metas del paciente (goal_type [Weight, Activity, Nutrition, Behavior], target_value, deadline, status, created_by). N:1 patient.
   - Catálogos mínimos: `encounter_types`, `clinical_note_types`, `measurement_types` (catálogo-driven, extensibles sin código).
2. **Endpoints** (`PatientsController`/nuevo `ClinicalController`): CRUD por paciente con frontera de clínica (Fase 4) + permisos `ClinicalRecords.View/Create/Update/Delete` (ya seedeados).
3. **Frontend**: secciones en el detalle de paciente — Historial clínico (encounters + notas), Mediciones (tabla + gráfica de peso/IMC), Metas.
4. **Tests**: unit (handlers, cálculo de IMC en DTO, append-only de notas) + integración (frontera de clínica, IDOR).

### Fase 7 — Appointments + Prescriptions reales

Reemplazar los mocks del frontend (`/patients/appointments`, `/patients/prescriptions`) por datos reales.

1. **Dominio** (`app.`):
   - `app.appointments` — cita (patient, professional, clinic/location, scheduled_at, duration_minutes, status [Scheduled, Confirmed, Completed, Cancelled, NoShow], reason, notes). Índices por paciente, profesional y ventana de tiempo.
   - `app.prescriptions` + `app.prescription_items` — receta (prescriber = professional, patient, prescribed_at, status, instructions) y ítems (medication del catálogo `app.medications` ya existente, dosage, frequency, duration, refills).
2. **Endpoints**: CRUD + agenda del profesional (rango de fechas) + estados; permisos `Appointments.*` / `Prescriptions.*` (nuevos a seedear).
3. **Frontend**: agenda por clínica/profesional, wizard de cita, recetario con catálogo de medicamentos.
4. **Tests**: unit + integración (conflicto de agenda, frontera de clínica).

### Fase 8 — Bulk operations

Acciones masivas **nunca como loops del frontend**; operaciones de gran volumen asíncronas vía jobs.

1. **Infra**: completar `app.background_jobs` + `IJobDispatcher` (colas en tabla; swap futuro a Hangfire/SQS sin tocar features). Idempotencia por job (job_id único).
2. **Import de pacientes**: importador CSV/Excel (patrón del script `import_patients_from_excel.py` llevado a un job con reporte de errores por fila y validación en lote).
3. **Export**: exportador paginado a CSV/Excel con permisos `Patients.Export` (ya seedeado).
4. **Acciones masivas de pacientes**: activar/desactivar, cambiar clínica/sede (con reasignación de contexto), asignar/desasignar profesional responsable, subir documentos en lote, actualización de atributos concretos. Endpoints `POST /patients/bulk/{action}` con request de ids + payload + transacción e idempotencia.
5. **Acciones masivas de profesionales** (requerimiento nuevo): invitar en lote, asignar/desasignar clínicas con su rol scoped, activar/desactivar, reenviar invitaciones.
6. **Frontend**: selección múltiple en listados (checkboxes) + barra de acciones + modal de confirmación con resumen y reporte de resultado.
7. **Tests**: unit (lote, validación parcial, idempotencia) + integración (transacción completa/rollback).

### Fase 9 — Hardening

1. **Auditoría con actor real**: `HttpAuditActorContext` hoy devuelve `ActorType=System` — conectarlo a `ICurrentContext` (userId) y ampliar `activity_logs` para registrar acciones de pacientes/documentos/profesionales con detalle before/after.
2. **Índices documentados**: revisar `docs/database/indexes.md` y documentar cada índice nuevo (regla del skill `database-indexes`).
3. **Tests IDOR/seguridad**: suite de integración que valida la frontera de clínica (paciente/documento de otra clínica → 404) y permisos scoped/globales por rol.
4. **Observabilidad**: Correlation ID ya en logs; agregar métricas por endpoint clínico (latencia, 4xx/5xx) y monitoreo de jobs de bulk.
5. **Validación de volumen**: pruebas con el directorio importado (200k pacientes) para listados, filtros y export (skills `query-performance`, `pagination`).

### Requerimientos transversales nuevos (integran las fases)

- **Seed de la estructura MediQuer** (organización proveedor principal + clínicas/sedes) como data seed idempotente, para que el equipo demuestre el flujo multi-clínica con scopes reales. Patrón: script generador → `AddOrganizationSeed.sql` (recurso embebido).
- **Gestión de scopes por clínica (UX)**: detalle del profesional con tabla de asignaciones por clínica (rol + permisos Grant/Deny), usando los endpoints de Auth (`/api/users/{id}/scoped/roles|permissions`) ya existentes.
- **Acciones masivas de profesionales** integradas con el onboarding (invitar N profesionales con sus clínicas/roles en un solo paso).
- **Documentos en detalle de paciente (frontend)**: completar la Fase 5 pendiente (tab de documentos con upload directo a S3, versionado y descarga firmada).

## Reglas de implementación por fase

- Toda tabla clínica/directorio exige `clinic_id` indexado (regla anti-retenancy).
- FKs siempre indexadas (PostgreSQL no las crea automáticamente).
- Migraciones aditivas con backfill documentado; nunca reescribir tablas existentes de golpe.
- Cada fase termina con: `dotnet build` + `dotnet test` + migración validada + docs actualizados.
- Catálogos (tipos de profesión, especialidades, tipos de documento, tipos de nota) siempre **catálogo-driven**: agregar valores es dato, no código.
- Acciones masivas con transacción explícita + idempotencia; sin loops de HTTP desde el frontend.
- Comentarios y docs en español.