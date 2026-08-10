---
name: documentation
description: 'Documentación por módulo en CoppAddresd: estructura docs/, qué documentar, cuándo actualizar. Toda doc en español.'
---

# Documentación — Reglas del proyecto

Comentarios y docs en **español**. Toda feature/objeto de BD/decisión relevante se documenta. Sin doc → no termina la tarea.

## Estructura `docs/`

```text
docs/
├── README.md                      # índice general
├── architecture/README.md         # arquitectura, flujo, decisiones (ADRs)
├── modules/
│   └── <modulo>/                  # una carpeta por módulo de negocio (users, payments...)
│       ├── README.md              # objetivo + enlaces
│       ├── architecture.md        # entidades, flujo, dependencias
│       ├── queries.md             # queries importantes, paginación, índices usados
│       ├── database.md            # tablas, relaciones, transacciones, concurrencia
│       └── performance.md         # decisiones de rendimiento, mediciones
├── database/
│   ├── normalization.md
│   ├── indexes.md                 # cada índice documentado (ver skill database-indexes)
│   ├── views.md
│   ├── triggers.md
│   └── transactions.md
├── performance/
│   ├── linq.md
│   ├── pagination.md
│   ├── n-plus-one.md
│   └── concurrency.md
└── aws/
    └── production.md              # diagrama, recursos, runbook de diagnóstico
```

Estructura flexible: adaptar al módulo real, no crear carpetas vacías sin contenido.

## Contenido mínimo por módulo

```text
Objetivo | Arquitectura | Flujo | Entidades | DTOs | Endpoints
Queries | Commands | Repositories | Services | Base de datos
Índices | Relaciones | Transacciones | Concurrencia | CancellationToken
Riesgos | Rendimiento | Escalabilidad | Dependencias
```

Solo lo que exista: en esqueleto no inventar documentación de módulos inexistentes.

## Cuándo actualizar

- **Siempre que se modifica** código, esquema, queries, índices o decisiones: la doc del módulo se actualiza en la misma tarea (paso final del checklist).
- Nuevo índice → `docs/database/indexes.md`.
- Nueva query importante → `docs/modules/<mod>/queries.md`.
- Decisión de arquitectura (qué capa, patrón, trade-off) → ADR corto en `docs/architecture/` con: contexto, decisión, consecuencias.
- Cambio de stack/versiones → `README.md` raíz.

## Formato

- Markdown, conciso, tablas para objetos estructurados.
- Comentarios de código solo donde aportan (reglas de negocio no obvias); el código se explica solo, la doc explica decisiones y contratos.
- Código de ejemplo en bloques fenced con lenguaje.
- En español (convención del repo).

## Regla fundamental

Documentar el PORQUÉ (decisión, trade-off), no re-narrar el código.
