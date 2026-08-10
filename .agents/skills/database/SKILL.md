---
name: database
description: 'Diseño de base de datos en CoppAddresd: PostgreSQL 16, esquema, normalización, vistas, triggers, documentación de objetos.'
---

# Base de datos — Reglas del proyecto

PostgreSQL 16+ vía Npgsql 10.0.3. Sin base de datos real todavía (esqueleto): toda decisión de diseño debe quedar documentada en `docs/database/`.

## Convenciones de esquema

- Nombres snake_case: `users`, `order_items`, `created_at`.
- PK: `uuid` por defecto (generadas en app con `Guid.NewGuid()`) o `bigint identity` para tablas de altísimo volumen de escritura.
- `timestamptz` para tiempos (nunca `timestamp` local). Fechas/horas de dominio como `DateTimeOffset` en C#.
- `numeric` con precisión para dinero; `jsonb` solo cuando el modelado relacional justifique perder estructura.
- FKs con índice sobre la columna (PostgreSQL no los crea automáticamente).
- `ON DELETE` explícito (RESTRICT/CASCADE según semántica del dominio).

## Normalización (1NF–5NF)

Ver `docs/database/normalization.md`. Base: 3NF. Regla:

- 1NF: valores atómicos, sin listas/repetidos en una celda.
- 2NF: no depender de parte de la clave compuesta.
- 3NF: no depender de columnas no clave.
- 4NF/5NF: solo cuando el análisis de dependencias multivaluadas lo justifique.
- **Desnormalizar solo con justificación documentada**: problema → motivo → trade-off → impacto lectura/escritura → consistencia → sincronización → estrategia de actualización. Si se desnormaliza, la estrategia de sync es obligatoria (código, job o evento — nunca trigger por defecto).

## Vistas

Crear una View solo con razón concreta: complejidad de query reutilizable, reportería, seguridad (ocultar columnas), desacople de lógica de lectura. **No por moda.** Cada vista documentada en `docs/database/views.md`:

```
Nombre | Objetivo | Tablas | Campos | Índices relacionados | Motivo | Impacto esperado | Consideraciones de rendimiento
```

Materialized Views: solo si la tolerancia a datos desactualizados lo permite; planificar refresh (`REFRESH MATERIALIZED VIEW CONCURRENTLY` + índice único).

## Triggers

**No crear triggers salvo razón arquitectónica válida.** Antes de usar uno evaluar (y documentar la decisión):

1. ¿Se resuelve en Application/Domain?
2. ¿Con evento de dominio?
3. ¿Con proceso background?
4. Efectos secundarios ocultos, bloqueos, impacto en transacciones, rendimiento, debugging, comportamiento inesperado desde EF Core (registros modificados fuera de contexto de tracking, valores no reflejados en entidades).

Si aun así es necesario: documentar completamente en `docs/database/triggers.md` (qué hace, cuándo, tablas, transacciones, pruebas).

## Procesos costosos

- UPDATE/DELETE masivos: evaluar batch (`ExecuteUpdateAsync`/`ExecuteDeleteAsync`), chunking, ventana de mantenimiento, locks (ver skills `concurrency`, `transactions`, `migrations`).
- Toda consulta importante analizada contra índices (skill `database-indexes`).

## Documentación obligatoria

Cada objeto relevante (tabla grande, vista, índice, trigger, vista materializada) documentado en `docs/database/`. Sin doc → no entra a producción.
