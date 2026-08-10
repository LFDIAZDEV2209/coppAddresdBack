---
name: database-normalization
description: 'Normalización de base de datos en el proyecto: niveles, cuándo aplicarlos, cuándo desnormalizar con justificación documentada.'
---

# Normalización — Reglas del proyecto

Base del diseño: **3NF**. Los niveles superiores se aplican cuando el análisis lo justifica; la desnormalización es decisión consciente, nunca improvisación.

## Niveles

| Nivel | Regla | En la práctica |
|---|---|---|
| 1NF | Valores atómicos; sin repeticiones en una celda | No listas separadas por comas; tablas hijas |
| 2NF | No depender de parte de una clave compuesta | Evitar PKs compuestas artificiales; usar surrogate key |
| 3NF | No depender de columnas no clave | Facturar datos de una tabla a otra = columna calculada/denormalizada: moverla o derivarla |
| 4NF | Sin dependencias multivaluadas | raro; solo modelado complejo de entidad-relación |
| 5NF | Sin dependencias de join | teórico; aplicar solo con evidencia de redundancia de join |

Regla general: si la aplicación correcta requiere mantener el mismo dato en dos tablas, es candidata a normalizar o a una decisión de desnormalización explícita.

## Proceso de diseño de una tabla

1. Identificar entidad y PK (uuid por defecto).
2. Definir atributos → comprobar 2NF/3NF.
3. Relaciones: 1:N con FK indexada; N:N con tabla puente.
4. Tipos PostgreSQL correctos (skill `database`).
5. Validar contra las queries reales del módulo: ¿filtra/ordena por columnas no clave? → índice, no desnormalización.

## Cuándo desnormalizar (justificación obligatoria)

Aplicar solo cuando haya requisito real de rendimiento o lectura:

1. Query de lectura de alta frecuencia que requiere join de muchas tablas.
2. Tablas con millones de filas donde el join/agregación no escala dentro de presupuesto de latencia.
3. Reportería/read models separados (CQRS de lectura).

Antes de desnormalizar documentar en `docs/database/normalization.md`:

```text
Problema | Motivo | Trade-off | Impacto en lectura | Impacto en escritura | Consistencia | Sincronización | Estrategia de actualización
```

## Reglas de sincronización

- La estrategia de actualización es obligatoria y una sola: código en la transacción de la escritura (mismo commit), evento de dominio + consumidor, o proceso background (read model). **Nunca trigger por defecto** (skill `database`).
- Campos denormalizados de solo lectura de negocio → nombre documentado (p. ej. `denormalized_` no, mejor semántico) y nunca tratados como fuente de verdad.
- Validar consistencia con reconciliación periódica si el riesgo lo amerita (query de detección de divergencia + job de repair).

## Anti-patrones

- Desnormalizar "para que sea más rápido" sin medir.
- Columnas calculadas sincronizadas a mano en múltiples lugares (se desincronizan).
- Tablas redundantes sin estrategia de refresh.
- Normalizar en exceso tablas de logging/eventos (append-only puede ser tabla plana).
