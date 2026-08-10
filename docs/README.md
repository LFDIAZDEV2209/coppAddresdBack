# CoppAddresd — Documentación

Índice de documentación técnica del backend. Idioma: **español** (convención del repo).

## Estado del proyecto

Esqueleto: Clean Architecture + .NET 10, módulos de negocio aún no implementados. Documentación de arquitectura es el target/estándar a seguir cuando se implementen módulos.

## Secciones

| Sección | Contenido |
|---|---|
| [Arquitectura](architecture/README.md) | Mapa completo, flujo de petición, responsabilidades por capa, decisiones (ADR) |
| [Base de datos](database/) | Normalización, índices, vistas, triggers, transacciones |
| [Rendimiento](performance/) | LINQ, paginación, N+1, concurrencia |
| [AWS](aws/production.md) | Arquitectura de producción, secretos, monitoreo, runbook |
| [Módulos](modules/README.md) | Reglas de documentación por módulo de negocio |

## Reglas de documentación

- Toda doc en español (skill `documentation`).
- Cada objeto de BD (índice, vista, trigger), query importante y decisión de arquitectura documentada.
- Actualizar la doc del módulo en la misma tarea que cambia el código.
- Sin documentación → la tarea no está terminada.
