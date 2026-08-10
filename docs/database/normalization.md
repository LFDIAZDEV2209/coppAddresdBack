# Normalización — Base de datos

Reglas del proyecto (skill `database-normalization`). Base de diseño: **3NF**.

## Niveles aplicados

| Nivel | Regla | Aplicación en el proyecto |
|---|---|---|
| 1NF | Valores atómicos | Tablas hijas en vez de listas separadas por comas |
| 2NF | No dependencia parcial de clave compuesta | PK surrogate (`uuid`), no claves compuestas artificiales |
| 3NF | No dependencia de columnas no clave | El dato se guarda en su tabla; si se duplica, decisión explícita |
| 4NF/5NF | Solo con análisis de dependencias multivaluadas | Raro; evaluar caso a caso |

## Reglas de decisión

1. Atributo derivable de otro dato → derivarlo en query o computar; no almacenar.
2. Mismo dato en dos tablas → normalizar o desnormalización documentada.
3. Agregación frecuente (contadores, totales) → índice + agregación SQL primero; read model desnormalizado solo si no escala.

## Registro de desnormalizaciones

| Problema | Motivo | Trade-off | Impacto lectura | Impacto escritura | Consistencia | Sincronización | Estrategia de actualización |
|---|---|---|---|---|---|---|---|
| — | — | — | — | — | — | — | — |

_(Vacío por esqueleto. Toda desnormalización futura se registra aquí — obligatorio.)_

## Regla de sincronización

- Una única estrategia por campo: misma transacción, evento de dominio + consumidor, o job background. Nunca trigger por defecto.
- Reconciliación periódica si el riesgo de divergencia lo amerita.
