---
name: database-schema
description: Organización de esquemas PostgreSQL en CoppAddresd — un esquema por servicio/producto. Mapeo servicio→schema, reglas de decisión al crear tablas, referencias cross-service y desviaciones conocidas. Cargar ANTES de crear o modificar tablas, configuraciones EF (`IEntityTypeConfiguration`) o migraciones.
---

# Esquemas de base de datos — regla por servicio

## Regla de oro

> **Cada servicio/producto tiene su propio esquema.** Un módulo o feature NO crea un esquema nuevo: sus tablas viven en el esquema del servicio al que pertenecen.

Un esquema no es un contenedor por feature, es el **límite de un servicio**. `store` y `inventory` no son servicios: son features del ERP, por lo que sus tablas van en `erp`. `auth` y `agents` sí son servicios, por eso tienen esquema propio.

## Mapeo servicio → schema

| Servicio / producto | Schema | Tablas (ejemplos) |
|---|---|---|
| **Auth Service** (`src/Services/CoppAddresd.Auth`) | `auth` | users, roles, permissions, applications, user_applications, refresh_tokens |
| **App móvil** (pacientes, `app`) | `app` | patient_profiles, insurers, allergens, medications, vital_signs, countries, media_items, documents, document_categories |
| **ERP** (empleados, clínicas, inventario, tienda) | `erp` | employees, organizations, clinics, professionals, specialties, **products, inventory_entries, inventory_movements, store_items** |
| **Agentes / AI** | `agents` | agent_types, agent_type_versions, knowledge_bases, agent_instances |
| **Auditoría** (cross-cutting) | `audit` | activity_logs |
| **Reservado** | `public` | solo `__EFMigrationsHistory` |

> **Nota:** las tablas de inventario y tienda aparecen en el ejemplo de `erp` porque es SU destino correcto. Hoy viven en `inventory`/`store` (ver "Desviaciones").

## Proceso de decisión (al crear una tabla nueva)

1. Identificar el **servicio dueño** de la feature que se está modelando.
2. Usar el **schema de ese servicio** en `builder.ToTable("tabla", "schema")`.
3. **Nunca crear un schema nuevo por feature/módulo.** Un esquema nuevo solo se crea cuando nace un servicio nuevo (nuevo proceso/deployment con su propio ciclo de vida), y requiere autorización.
4. Ante duda sobre a qué servicio pertenece una tabla → **preguntar**, no adivinar.

## Referencias cross-service

- Las FKs entre servicios apuntan al schema del servicio dueño de la tabla referenciada:
  - `erp.employees.user_id → auth.users.id`
  - `app.patient_profiles.user_id → auth.users.id`
- La referencia cruzada se escribe con el nombre del schema calificado en la FK (`principalSchema` en la migración) y en queries con `schema.tabla`.
- No se copian tablas entre servicios; se referencian.

## Configuración EF Core

- El schema se fija en la configuración de la entidad:
  ```csharp
  builder.ToTable("products", "erp");
  ```
- Una migración que usa un schema por primera vez declara `EnsureSchema(name: "erp")` en `Up()`.
- La plantilla del modelo la genera `dotnet ef migrations add`, pero la elección del schema es del desarrollador en la configuración, no de la migración.
- Seguir la skill `migrations` para el flujo completo de migraciones y la skill `database` para naming, PK, tipos y normalización.

## Desviaciones conocidas (estado actual)

- **`inventory` y `store` son esquemas separados hoy** (`products`, `inventory_entries`, `inventory_exits`, `inventory_movements` en `inventory`; `store_items` en `store`). Según esta regla deben **consolidarse en `erp`**:
  - `inventory.products` → `erp.products`
  - `inventory.inventory_*` → `erp.inventory_*`
  - `store.store_items` → `erp.store_items`
- Esta consolidación es una migración **breaking** (mueve tablas + FKs + datos): **no ejecutarla sin autorización explícita**. Documentar el plan en `docs/database/` antes de hacerla.
- No propagar la desviación: cualquier tabla NUEVA de inventario/tienda va directamente a `erp`.

## Definition of Done — cambiar un schema

```text
[ ] Identifiqué el servicio dueño de la feature.
[ ] La tabla nueva vive en el schema de ese servicio.
[ ] No creé un schema nuevo por feature.
[ ] Las FKs cross-service apuntan al schema correcto (schema.tabla).
[ ] La configuración EF usa ToTable("tabla", "schema").
[ ] No toqué la consolidación inventory/store → erp sin autorización.
[ ] La migración declara EnsureSchema solo para servicios nuevos.
[ ] `dotnet build` pasa y la migración se probó contra PostgreSQL.
```