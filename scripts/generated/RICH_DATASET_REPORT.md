# Informe: dataset rico de pruebas (rich_dataset_seed)

- Generado por: `scripts/generate_rich_dataset_seed.py`
- Archivo SQL: `scripts/generated/rich_dataset_seed.sql` (175,189 bytes)
- Alcance (scope): `demo`
- BD objetivo: docker `coppAddresd` → psql `-U app_user -d coppaddresd` (localhost:5432)
- Reglas cumplidas: sin pacientes/profesionales nuevos · sin borrados ·
  upserts idempotentes · `app.program_weeks` nunca escrito (semanas congeladas intactas).

## Qué genera

| Sección | Contenido                                                                                                                                                                                                                                                              |
| ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A       | 40 media_items Publicados (30 podcasts + 6 videos + 4 audios) en las 9 categorías; 8-25 min; chapters/takeaways JSON coherentes; day=1..83 y month=1..3 del programa 83 días/12 semanas; claves demo `media/podcasts/demo-copp.mp3` / `media/thumbnails/demo-copp.jpg` |
| B       | Asignación de podcasts a `weekly_day_templates` con la MISMA rotación por weekday de DevProgramContentSeeder (`pool[(weekday-1) % count]`); bump de `program_templates.version` solo si cambió algo                                                                    |
| C       | `patient_professionals` (Assigned/Active) solo para pacientes demo sin vínculo                                                                                                                                                                                         |
| D       | 4 `app.encounters` canónicos por paciente (3 meses) con motivo y notas clínicas coherentes                                                                                                                                                                             |
| E       | `clinical_measurements` (source=rich-seed): peso/cintura/FC semanales, TA quincenal, HbA1c/glucosa mensuales con tendencia de mejora; +13 `vital_signs` semanales por paciente                                                                                         |
| F       | Diagnósticos (ICD-10 reales), 2 alergias y 2 medicamentos por paciente                                                                                                                                                                                                 |
| G       | Tests de Salud: batería seguimiento COMPLETADA + cardiovascular EN CURSO (historia-clinica completa, orp a medias, ers pendiente) + adherencia PENDIENTE; respuestas deterministas, scores/subescalas/indicadores con severity de los rangos reales                    |
| H       | 2 rutinas de ejercicio activas por paciente                                                                                                                                                                                                                            |
| I       | 21 días de `habit_checks` + `nutrition_intake_logs` (5 comidas/día con macros coherentes)                                                                                                                                                                              |
| J       | 5 citas pasadas (3 Completed / 1 Cancelled / 1 NoShow) + 2 futuras Confirmed por paciente, ligando paciente ↔ profesional existentes                                                                                                                                   |
| K       | 8 tokens `fcm-seed-*` por plataforma (ios/android) para los usuarios demo prioritarios                                                                                                                                                                                 |

## Conteos antes / después (PRIMERA aplicación — creación del dataset)

| Métrica                                                     | Antes   | Después | Delta                      |
| ----------------------------------------------------------- | ------- | ------- | -------------------------- |
| media_items (total)                                         | 15      | 55      | **+40**                    |
| media_items demo (storage_key=media/podcasts/demo-copp.mp3) | 0       | 40      | **+40**                    |
| clinical_measurements (total)                               | 1289    | 3354    | **+2065**                  |
| clinical_measurements source=rich-seed                      | 0       | 2065    | **+2065**                  |
| health_test_results (total)                                 | 4643    | 5064    | **+421**                   |
| health_test_evaluations (total)                             | 1041    | 1133    | **+92**¹                   |
| health_test_responses (total)                               | 4000    | 4401    | **+401**                   |
| tele.appointments (total)                                   | 3926    | 4171    | **+245**                   |
| tele.appointments creadas por este seed                     | 0       | 245     | **+245**                   |
| nutrition_intake_logs source=rich_seed                      | 0       | 2434    | **+2434**                  |
| encounters creados por este seed                            | 0       | 140     | **+140**                   |
| device_tokens fcm-seed-*                                    | 32      | 48      | **+16** (8 por plataforma) |
| routine_assignments creados por este seed                   | 0       | 50      | **+50**²                   |
| program_weeks (NUNCA tocado por este seed)                  | 11776   | 11776   | **0**                      |
| program_templates (code/version)                            | v2 / v2 | v3 / v3 | bump único                 |

¹ Los pacientes demo que ya tenían asignaciones de baterías (seed_health_mass_direct) NO se duplican: la guarda `NOT EXISTS (patient, battery)` los respeta.
² Solo 25 pacientes recibieron rutinas nuevas: los otros 10 ya tenían `routine_assignments` previos y se respetan.

## Verificación de idempotencia (SEGUNDA ejecución, sin cambios)

Re-ejecutar `--apply` inmediatamente después produjo **0 deltas en todas las
métricas**, `program_weeks` intacto (11.776) y sin segundo bump de versión
(las plantillas se mantienen en v3).

| Métrica                                                     | Antes                                          | Después                                        | Delta |
| ----------------------------------------------------------- | ---------------------------------------------- | ---------------------------------------------- | ----- |
| media_items (total)                                         | 55                                             | 55                                             | 0     |
| media_items demo (storage_key=media/podcasts/demo-copp.mp3) | 40                                             | 40                                             | 0     |
| clinical_measurements (total)                               | 3354                                           | 3354                                           | 0     |
| clinical_measurements source=rich-seed                      | 2065                                           | 2065                                           | 0     |
| health_test_results (total)                                 | 5064                                           | 5064                                           | 0     |
| health_test_evaluations (total)                             | 1133                                           | 1133                                           | 0     |
| health_test_responses (total)                               | 4401                                           | 4401                                           | 0     |
| tele.appointments (total)                                   | 4171                                           | 4171                                           | 0     |
| tele.appointments creadas por este seed                     | 245                                            | 245                                            | 0     |
| nutrition_intake_logs source=rich_seed                      | 2434                                           | 2434                                           | 0     |
| encounters creados por este seed                            | 140                                            | 140                                            | 0     |
| device_tokens fcm-seed-*                                    | 48                                             | 48                                             | 0     |
| routine_assignments creados por este seed                   | 50                                             | 50                                             | 0     |
| program_weeks (NUNCA tocado por este seed)                  | 11776                                          | 11776                                          | 0     |
| program_templates (code/version)                            | program-coppaddresd-83-days v3; default-83w v3 | program-coppaddresd-83-days v3; default-83w v3 | -     |

## Consultas de muestra

### 1) Media demo por categoría (duración media en minutos)

```sql
SELECT category, count(*) AS items, round(avg(duration_secs) / 60.0, 1) AS minutos_prom
FROM app.media_items
WHERE storage_key = 'media/podcasts/demo-copp.mp3'
GROUP BY category
ORDER BY count(*) DESC;
```

```text
Habitos|7|10.1
SaludFisica|7|12.3
Nutricion|7|12.4
BienestarEmocional|5|10.0
Mindfulness|4|9.3
Motivacion|4|9.8
Psicologia|3|11.3
CrecimientoPersonal|2|11.0
Biologia|1|15.0
```

### 2) Rotación por weekday de podcasts en las plantillas (misma lógica del seeder)

```sql
SELECT pt.code AS plantilla, t.weekday, m.sort_order, m.title
FROM app.weekly_day_templates t
JOIN app.program_templates pt ON pt.id = t.template_id
JOIN app.media_items m ON m.id = t.media_id
WHERE t.task_code = 'podcast' AND pt.code = 'program-coppaddresd-83-days'
ORDER BY t.weekday;
```

```text
program-coppaddresd-83-days|1|8|Ep. 8: Índice glucémico y carga glucémica
program-coppaddresd-83-days|2|9|Ep. 9: Proteína, saciedad y masa muscular
program-coppaddresd-83-days|3|10|Ep. 10: Ultraprocesados: lee la etiqueta como experto
program-coppaddresd-83-days|4|11|Ep. 11: Fibra: el nutriente que olvidamos
program-coppaddresd-83-days|5|12|Ep. 12: Grasas que curan, grasas que dañan
program-coppaddresd-83-days|6|13|Ep. 13: Cardio zona 2: la base de tu resistencia
program-coppaddresd-83-days|7|14|Ep. 14: Fuerza para principiantes: empieza bien
```

### 3) Resultados de Tests de Salud del paciente demo 55551234 (score/severity)

```sql
SELECT i.code AS instrumento, r.result_type, r.code, r.value, r.qualifier, r.severity
FROM app.health_test_results r
JOIN app.health_test_evaluations e ON e.id = r.evaluation_id
JOIN app.health_test_versions v ON v.id = e.version_id
JOIN app.health_test_instruments i ON i.id = v.instrument_id
JOIN app.patient_profiles p ON p.id = e.patient_id
WHERE p.document_number = '55551234'
  AND e.started_at > now() - interval '90 days'
ORDER BY e.completed_at DESC, r.result_type
LIMIT 12;
```

```text
movimiento|score|movimiento|4.00|bajo|low
movimiento|subscale|Actividad · Nivel actual|0.00|bajo|low
movimiento|subscale|Capacidad · Resistencia|2.00|bajo|low
movimiento|subscale|Limitaciones · Dolor|0.00|bajo|low
movimiento|subscale|Disponibilidad · Tiempo|2.00|bajo|low
nutricional|score|nutricional|8.00|moderado|moderate
nutricional|subscale|Hábitos · Ultraprocesados|2.00|bajo|low
nutricional|subscale|Motivación · Disposición al cambio|2.00|bajo|low
nutricional|subscale|Hidratación · Agua diaria|1.00|bajo|low
nutricional|subscale|Conducta · Control de porciones|2.00|bajo|low
nutricional|subscale|Hábitos · Frecuencia|0.00|bajo|low
nutricional|subscale|Conducta · Alimentación emocional|1.00|bajo|low
```

### 4) Citas de telemedicina del seed por estado

```sql
SELECT status, count(*) AS citas
FROM tele.appointments
WHERE created_by = '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f'
GROUP BY status
ORDER BY citas DESC;
```

```text
Completed|105
Confirmed|70
Cancelled|35
NoShow|35
```

### 5) Resumen de riqueza de datos por paciente demo (55551234 = luis.prueba)

```sql
SELECT 'resumen' AS k, p.document_number,
       (SELECT count(*) FROM app.encounters x WHERE x.patient_id = p.id AND x.created_by = '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f') AS encounters,
       (SELECT count(*) FROM app.clinical_measurements x WHERE x.patient_id = p.id AND x.source = 'rich-seed') AS mediciones_3m,
       (SELECT count(*) FROM app.patient_diagnoses x WHERE x.patient_id = p.id) AS diagnosticos,
       (SELECT count(*) FROM app.patient_allergies x WHERE x.patient_id = p.id) AS alergias,
       (SELECT count(*) FROM app.patient_medications x WHERE x.patient_id = p.id) AS medicamentos,
       (SELECT count(*) FROM app.health_test_assignments x WHERE x.patient_id = p.id AND x.status = 'completed' AND x.assigned_at > now() - interval '90 days') AS tests_completados,
       (SELECT count(*) FROM app.routine_assignments x WHERE x.patient_id = p.id) AS rutinas,
       (SELECT count(*) FROM app.nutrition_intake_logs x WHERE x.patient_id = p.id AND x.source = 'rich_seed') AS intake_logs,
       (SELECT count(*) FROM tele.appointments x WHERE x.patient_id = p.id AND x.created_by = '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f') AS citas
FROM app.patient_profiles p WHERE p.document_number = '55551234';
```

```text
55551234 | encounters=4 | mediciones_3m=59 | diagnosticos=3 | alergias=2
| medicamentos=2 | tests_completados=9 | rutinas=2 | intake_logs=69 | citas=7
```

## Notas de operación

### Semanas congeladas (ProgramWeek.TasksSnapshot)

Este seed NUNCA inserta ni actualiza `app.program_weeks`: el conteo
`program_weeks` de la tabla de arriba debe quedar idéntico antes/después.
Las semanas ya activadas conservan su snapshot horneado por diseño; solo
las activaciones futuras heredan los podcasts nuevos de la plantilla.

### Claves S3 pendientes de subir por el usuario

- `media/podcasts/demo-copp.mp3` (audio de TODOS los 40 ítems)
- `media/thumbnails/demo-copp.jpg` (miniatura de TODOS los 40 ítems)

### Idempotencia y re-ejecución

- `ON CONFLICT DO NOTHING` donde existe clave única de negocio: baterías
  `(code)`, alergias `(patient_id, allergen_id)`, ingestas
  `(patient_id, local_date, meal_code)`, habit_checks
  `(patient_id, habit_template_id, local_date)`, tokens `(user_id, token)`.
- Guardas `NOT EXISTS` sobre clave de negocio donde la tabla solo tiene PK
  (encounters, measurements, citas, evaluaciones, respuestas, resultados).
- Todas las filas llevan marca de origen: `created_by`/`source` con el
  marcador `7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f` (o `source = 'rich-seed'/'rich_seed'`).
- Las ventanas de fechas son relativas a `current_date`, así que la
  re-ejecución mantiene el dataset fresco; los topes por paciente
  (4 encounters, 13 mediciones/métrica, 5 citas pasadas, 2 futuras) evitan
  crecimiento indefinido.

### Cómo re-ejecutar

```powershell
cd coppAddresdBack/scripts
python generate_rich_dataset_seed.py --apply
```

Manual:

```powershell
Get-Content .\generated\rich_dataset_seed.sql -Raw | docker exec -i coppAddresd psql -U app_user -d coppaddresd -v ON_ERROR_STOP=1
```

> NUNCA ejecutar contra producción. Solo BD local docker `coppaddresd`.
