# Módulos — Documentación por módulo

Skill: `documentation`. Regla: **cada módulo de negocio se documenta en `docs/modules/<modulo>/`**.

## Estructura por módulo

```text
docs/modules/<modulo>/
├── README.md              # objetivo del módulo + enlaces a lo demás
├── architecture.md        # entidades, flujo, dependencias, decisión de diseño
├── queries.md             # queries importantes, paginación, índices usados
├── database.md            # tablas, relaciones, transacciones, concurrencia
└── performance.md         # decisiones de rendimiento + mediciones
```

## Contenido mínimo obligatorio

```text
Objetivo | Arquitectura | Flujo | Entidades | DTOs | Endpoints
Queries | Commands | Repositories | Services | Base de datos
Índices | Relaciones | Transacciones | Concurrencia | CancellationToken
Riesgos | Rendimiento | Escalabilidad | Dependencias
```

## Reglas

- La doc del módulo se crea con la primera feature del módulo (no antes — sin inventar contenido).
- Se actualiza en la misma tarea que modifica código/esquema/queries.
- Decisiones de arquitectura transversales → `docs/architecture/README.md` (ADR).

## Módulos implementados

| Módulo | Descripción | Documentación |
|--------|-------------|---------------|
| **Auth** | Autenticación JWT, autorización por permisos, gestión de usuarios/roles | [auth/README.md](auth/README.md) |
| **Chat/AI** | Integración con AI Service (Python), chat síncrono + streaming SSE | [chat/README.md](chat/README.md) |
| **Activity Log** | Auditoría automática vía triggers PostgreSQL, schema `audit.` | [activity-log/README.md](activity-log/README.md) |
| **Storage** | Abstracción de almacenamiento de objetos (S3-like), implementación local por filesystem | [storage/README.md](storage/README.md) |

## Pendientes de implementación

Próximos módulos de negocio (dependen de requerimientos):
- Pacientes
- Citas
- CRM
- Agentes IA (configuración)
