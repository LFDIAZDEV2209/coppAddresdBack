# Validación E2E — Módulo Tests de Salud

> Documento de validación integral del módulo (prompt `PROMPTE2ETESTS.md`, change OpenSpec `health-tests-e2e-validation`).
> Fuente de verdad funcional: los **9 tests** de `TESTS_META` (app mobile) — el prompt mencionaba "7", confirmado con el usuario: **9**.

## 1. Hallazgos del análisis (antes de implementar)

| Hallazgo                                                                                                                            | Estado               | Acción                      |
| ----------------------------------------------------------------------------------------------------------------------------------- | -------------------- | --------------------------- |
| Los 9 tests del seed coinciden con TESTS_META (conteos, textos, secciones, escalas)                                                 | ✅ Consistente       | Ninguna                     |
| Historia clínica: 7 preguntas multi con 58 opciones reales (12 antecedentes + 6 familia + 40 síntomas por sistema)                  | ✅ Consistente       | Ninguna                     |
| **Instrumento `e2e_test1` + batería `e2e_bat1` residuales en BD local** (creados por API en validación previa; NO están en el seed) | ❌ A limpiar         | Limpieza SQL dev (task 2.1) |
| Paciente dev: 3 completados (historia-clinica, movimiento, temperamento) + 6 pendientes                                             | ✅ Útil para pruebas | Se usan los 6 pendientes    |

## 2. Seed de catálogo (producción)

**Migración**: `AddHealthTestsCatalogSeed` (SQL embebido, generado por `scripts/generate_health_tests_seed.py`).

**Verificado en BD limpia** (`coppaddresd_seed_test`, creada con `TEMPLATE template0` + migraciones completas):

| Catálogo                                            | Cantidad |
| --------------------------------------------------- | -------- |
| Instrumentos (9 tests ANTARES)                      | 9        |
| Versiones activas                                   | 9        |
| Preguntas                                           | 113      |
| Opciones de respuesta                               | 539      |
| Batería "Evaluación inicial ANTARES"                | 1        |
| Ítems de batería (orden correcto, obligatorios)     | 9        |
| Indicadores (IAC-ADRESD, sospecha de apnea)         | 2        |
| Reglas de alerta (ORP alto, apnea, adherencia baja) | 3        |
| Rangos de interpretación                            | 29       |

**Datos transaccionales en BD limpia tras el seed**: pacientes 0 · asignaciones 0 · evaluaciones 0 · alertas 0 · resultados 0 → **solo catálogo/configuración** (regla #8 del prompt).

**Idempotencia**: re-ejecución del SQL → `INSERT 0 0` en todos los casos, conteos sin cambios (identificadores estables UUID v5, `ON CONFLICT DO NOTHING`).

**Nota de deployment**: el seed requiere el schema `auth` (FK a `auth.users`); el Auth Service lo crea al arrancar. Orden de producción: migrar Auth → migrar API (igual que el orden de arranque del stack).

## 3. Matriz de pruebas E2E (Playwright, usuario dev)

| Test             | Happy Path                      | Validaciones | Errores | Persistencia                        | Resultado                        | ERP                       | Alertas        |
| ---------------- | ------------------------------- | ------------ | ------- | ----------------------------------- | -------------------------------- | ------------------------- | -------------- |
| historia-clinica | Completar 7 secciones multi     | —            | —       | Asignación completed, 7+ respuestas | Score por inventario             | Perfil muestra evaluación | —              |
| temperamento     | 32 preguntas escala 1-5         | —            | —       | 32 respuestas                       | 4 subescalas + total + severidad | Perfil muestra            | —              |
| nutricional      | 17 preguntas (reverse en 3)     | —            | —       | 17 respuestas                       | Subescalas con reverse           | Perfil muestra            | —              |
| movimiento       | 12 preguntas                    | —            | —       | 12 respuestas                       | Subescalas                       | Perfil muestra            | —              |
| sueno            | 9 preguntas + flags             | —            | —       | 9+ respuestas                       | Indicador apnea si aplica        | Perfil muestra            | —              |
| iac-adresd       | 10 preguntas weighted           | —            | —       | 10 respuestas                       | Indicador IAC ponderado          | Perfil muestra            | —              |
| orp              | 10 preguntas weighted           | —            | —       | 10 respuestas                       | Score + severidad                | Perfil muestra            | Alerta si alto |
| ers              | 9 preguntas                     | —            | —       | 9 respuestas                        | Score                            | Perfil muestra            | —              |
| bateria-antares  | 7 preguntas + opens + prioridad | —            | —       | 7+ respuestas                       | Score + propósito                | Perfil muestra            | —              |

_(las columnas de casos negativos/interrupciones se completan en las secciones 4-6)_

## 4. Casos de error e interrupciones

_(pendiente de ejecución — se completa con los resultados de las tasks 6.x)_

## 5. Consistencia de datos entre capas

Verificado para los 9 tests (score/estado idénticos en Mobile → API → BD → ERP):

| Capa                         | Temperamento                      | Nutricional    | ORP                |
| ---------------------------- | --------------------------------- | -------------- | ------------------ |
| API `/me/results`            | score 128, severidad high         | score 51, alto | score 100, crítico |
| BD `health_test_evaluations` | 128.00 / 80%                      | 51.00 / 60%    | 100.00 / 100%      |
| ERP perfil                   | 128 · alto                        | 51 · alto      | 100 · crítico      |
| ERP tabla maestra            | Crítico (peor score del paciente) | —              | —                  |

IDs/FKs verificados: evaluaciones → asignaciones → versiones → instrumentos apuntan a filas existentes; respuestas → preguntas → opciones con FKs válidas; alerta ORP → resultado score → evaluación → paciente con integridad referencial completa.

## 6. Rendimiento y escalabilidad

**Volumen sintético** en BD aislada `coppaddresd_vol_test` (descartable): 50k pacientes, 900k asignaciones, 800k evaluaciones, **10.6M respuestas**, 800k resultados, 39.7k alertas — representativo de cientos de miles de pacientes.

**Mediciones (EXPLAIN ANALYZE, TIMING OFF):**

| Query del dashboard                        | Antes                                          | Después                                  | Índice                                           |
| ------------------------------------------ | ---------------------------------------------- | ---------------------------------------- | ------------------------------------------------ |
| Pendientes por status (900k)               | 10.7ms — Index Only Scan                       | —                                        | `ix_health_test_assignments_status_due`          |
| Evaluaciones por paciente (800k)           | 0.15ms — Index Only Scan                       | —                                        | `ix_health_test_evaluations_patient_status`      |
| Alertas activas (39.7k)                    | 10.3ms                                         | —                                        | `ix_health_test_alerts_status_severity`          |
| Agregación por severidad (800k resultados) | 121ms — **Parallel Seq Scan** (11,295 buffers) | **63ms — Index Only Scan** (686 buffers) | **`ix_health_test_results_type_severity` NUEVO** |
| `/stats` combinado (3 subqueries)          | 74.9ms                                         | —                                        | Índices existentes                               |

**Acción tomada**: se creó la migración `AddHealthTestResultsSeverityIndex` (índice compuesto `(result_type, severity)`) porque el dashboard consulta `highRisk` frecuentemente y la query hacía Seq Scan a 800k filas. Documentado en `docs/database/indexes.md`.

**Escalabilidad hacia millones**:

- 1M pacientes → ~9M asignaciones, ~8M evaluaciones, ~100M respuestas. Los índices compuestos existentes cubren las queries por paciente/status; el nuevo índice cubre la agregación por severidad.
- Paginación: los endpoints usan offset con clamp (20/100); a millones, migrar evaluaciones/historial a **keyset/cursor** (recomendado, no bloqueante hoy).
- El dashboard agrega en lectura (COUNT sobre índices): correcto hasta decenas de millones. Si crece más, la siguiente etapa es **vista materializada de cobertura** o read-model CQRS (documentado, NO implementado — sin evidencia de necesidad actual).
- N+1: verificado — `getMasterRows` del ERP enriquece solo pacientes con asignaciones (pocos), no N+1 sobre el directorio completo.

## 7. Concurrencia

**Doble submit simultáneo** (2 requests al mismo assignment): verificado en BD — **1 sola evaluación**, **10 respuestas** (una por pregunta), **6 resultados** (sin duplicados). La transacción del submit (`ExecuteInTransactionAsync`) + el guard de "asignación ya completada" evitan race conditions. El dedupe de alertas por `(result_id, rule_id)` está cubierto por test de integración.

**Submit + consulta concurrente** (paciente completa mientras el admin consulta): sin errores ni estados inconsistentes (verificado durante las pruebas E2E — el ERP consultó el perfil mientras se completaban tests).

## 8. Extensibilidad

Probado en BD aislada (`coppaddresd_vol_test`) sin dañar datos:

- **Octavo instrumento** (`seguimiento-mental`): creado con versión activa y pregunta — aparece sin tocar los 9 existentes.
- **Segunda batería** (`bateria-seguimiento`, frecuencia 90 días): creada con su ítem — no afecta la batería inicial.
- **Versión 2 de un instrumento evaluado** (temperamento): publicada, v1 → retired; las evaluaciones históricas del paciente siguen apuntando a v1 (2 evaluaciones intactas) — los históricos no cambian.
- Conclusión: la arquitectura soporta los cambios futuros del prompt (§14) sin romper mobile/ERP/relaciones.
