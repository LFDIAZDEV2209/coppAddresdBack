# Vistas — Registro

Reglas del proyecto (skill `database`). Una vista se crea solo con razón concreta (complejidad reutilizable, reportería, seguridad, desacople de lectura). **No por moda.**

## Registro

| Nombre | Objetivo | Tablas | Campos | Índices relacionados | Motivo | Impacto esperado | Consideraciones de rendimiento |
|---|---|---|---|---|---|---|---|
| — | — | — | — | — | — | — | — |

_(Vacío por esqueleto.)_

## Reglas

- Documentar antes de crear.
- Vistas de reportería pesada → evaluar Materialized View con `REFRESH MATERIALIZED VIEW CONCURRENTLY` (requiere índice único) y tolerancia a datos viejos.
- Toda vista revisada contra el plan de ejecución (¿los joins usan índices?).
- Nombres: `v_<descripcion>`.
- Preferir proyección EF (`Select`) como primera opción; la vista es para reutilización/seguridad/agregaciones complejas.
