# Registro autónomo de peso — FASE 4

## Contrato

`POST /api/v1/program/me/weight`, autenticado. Body: `{ "weightKg": 80, "date": "2026-09-11" }`.
Devuelve HTTP 201 con `{ id, weightKg, date, observedAt }` tras persistir.
No acepta patientId/userId/enrollmentId como autoridad: `IProgramActorContext` resuelve paciente, inscripción activa y autor desde la sesión. El handler vuelve a comprobar la pertenencia y estado de la inscripción.

Se inspeccionaron CompleteTask/Vitals, carga de laboratorio y creación de paciente. No existía una operación autónoma para registrar exclusivamente peso: completar una tarea también altera XP y progreso; importar laboratorio requiere archivo. Se añadió `RecordWeightCommandHandler`, reutilizando `IClinicalMeasurementRepository.GetActiveMetricsWithUnitsAsync` y `AddBatchAsync`. No hay repositorios nuevos, migraciones ni tablas paralelas.

Cada guardado crea un nuevo `ClinicalMeasurement` en `app.clinical_measurements`: UUID nuevo, patient_id, metric_id del catálogo activo `weight`, value, unit_id de `kg` activo, observed_at, recorded_at, created_at, created_by del token y source `patient`. Encounter/batch/archivo quedan nulos. El repositorio inserta y confirma antes de invalidar `CacheKeys.MetricsHistory(patientId)`. No se completa una tarea ni se dispara XP.

## Reglas

- Peso requerido entre 1 y 500 kg, límites existentes de CreatePatient y CompleteTask extraídos a `WeightInputLimits` y reutilizados sin alterar su comportamiento.
- Máximo dos decimales, coherente con `numeric(12,2)`; se rechaza el exceso, no se redondea silenciosamente.
- Fecha requerida y válida; no futura según la zona de la inscripción, con fallback UTC como el historial existente. Se conserva la fecha seleccionada y la hora local de captura; para hoy se guarda el instante UTC exacto. Se rechaza una hora inexistente por DST.
- Varias mediciones por día están permitidas: no hay índice único paciente/métrica/fecha ni política de sustitución. Cada POST intencional añade una fila. El historial existente sigue mostrando la observación más reciente por día (orden ObservedAt/Id); esto no elimina las anteriores.
- El modal bloquea doble clic mientras guarda. No se añadió idempotencia distribuida: tras un corte de red posterior al commit puede ser necesario consultar el historial antes de repetir un envío incierto. No hay reintentos automáticos de POST por errores de red en el cliente.
- El guardado invalida la caché de este paciente. Se conserva la política existente de caché best-effort; una indisponibilidad de infraestructura no revierte una escritura ya confirmada.

## Verificación local, 11/09/2026

POST real desde Ionic/Playwright contra la API y PostgreSQL local (`coppaddresd`, loopback), sin mocks de persistencia:

| Cuenta de desarrollo | Nuevo peso | Id de medición | Resultado |
|---|---:|---|---|
| Paciente Antares (paciente `a90bd89b-984f-4a96-8095-9da2fb2bc3da`) | 80 kg, 11/09/2026 14:22:34 -05 | `75863cc2-3397-42ac-8a87-274b2156167c` | Cuatro filas en BD; anteriores 90/94/86 intactas; GET muestra 90/94/80 por agrupación diaria |
| Avatar PRUEBA LOCAL primer peso (paciente `e24a04af-4764-4000-8000-000000000001`) | 90 kg, 11/09/2026 14:25:17 -05 | `e91998f3-102a-446f-b75e-604076a2f175` | Cero → una medición; referencia neutral |

La segunda cuenta se preparó exclusivamente en desarrollo siguiendo `scripts/seed_patients_demo.py`: perfil identificado como prueba, endpoint interno Development-only `seed-patient-demo` y autoinscripción `enrollments/me`. Su contraseña aleatoria se mantuvo solo en memoria. Usuario resultante `01a091ed-fd8f-716c-9885-17c8bbd941e3`; inscripción `ac6f0643-f00e-4994-9ff2-4b204ad7fafc`. Ninguna medición se insertó por SQL: ambas se guardaron desde el modal mediante el endpoint nuevo. Se conservan las cuentas y filas de desarrollo para inspección; no se borró historial.

SQL de comprobación de ambas inserciones: joins de clinical_measurements con patient_profiles, measurement_metrics y unit_of_measures; patient_id y created_by coinciden con los usuarios autenticados, code=weight, unit=kg y valores/fechas correctos.

Pruebas: 13 tests .NET de rango, precisión, fecha, append, aislamiento por paciente, IDs inyectados, fallo de persistencia e invalidación de caché. HTTP real: 0/501 kg y tres decimales → 400, fecha futura → 422, fecha imposible → 400, anónimo → 401. Todas sin inserciones. Builds API/frontend correctos; advertencias existentes.

Evidencia de UI y métricas en `antares-paciente/docs/avatar-weight-validation/`. El reporte funcional se encuentra en `antares-paciente/docs/avatar-weight-recording.md`.
