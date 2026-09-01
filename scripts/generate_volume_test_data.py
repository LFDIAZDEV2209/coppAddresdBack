"""Genera volumen sintÃ©tico para el mÃ³dulo Tests de Salud en la BD de
rendimiento (coppaddresd_vol_test): 50k pacientes, 450k evaluaciones
completadas, ~5M respuestas, ~2.7M resultados y ~50k alertas.

Uso (desde coppAddresdBack/scripts):
  python generate_volume_test_data.py
"""

import random
import subprocess
import sys

CONN = "Host=localhost;Port=5432;Database=coppaddresd_vol_test;Username=app_user;Password=CoppAddresdDev!2026"

PATIENTS = 50_000
TESTS_PER_PATIENT = 9
RANDOM = random.Random(42)


def psql(sql: str) -> None:
    subprocess.run(
        [
            "docker",
            "exec",
            "-i",
            "coppAddresd",
            "psql",
            "-U",
            "app_user",
            "-d",
            "coppaddresd_vol_test",
            "-v",
            "ON_ERROR_STOP=1",
            "-t",
            "-A",
        ],
        input=sql,
        text=True,
        check=True,
    )


def main() -> None:
    print("Pacientes sintÃ©ticos...")
    psql(f"""
    INSERT INTO app.patient_profiles (id, first_name, last_name, document_number,
        status, created_at, updated_at)
    SELECT ('00000000-0000-0000-0000-' || lpad(g::text, 12, '0'))::uuid,
           'Volumen', 'Paciente ' || g, 'VOL' || lpad(g::text, 10, '0'),
           'Activo', now() - (g % 365) * interval '1 day',
           now() - (g % 30) * interval '1 day'
    FROM generate_series(1, {PATIENTS}) AS g
    ON CONFLICT (id) DO NOTHING;
    """)

    print("Asignaciones (pendientes + completadas)...")
    psql(f"""
    INSERT INTO app.health_test_assignments (id, patient_id, battery_assignment_id,
        version_id, status, assigned_by, assigned_at, completed_at)
    SELECT gen_random_uuid(),
           ('00000000-0000-0000-0000-' || lpad((((g - 1) / {TESTS_PER_PATIENT}) + 1)::text, 12, '0'))::uuid,
           NULL,
           v.id,
           CASE WHEN g % {TESTS_PER_PATIENT} = 0 THEN 'pending' ELSE 'completed' END,
           NULL,
           now() - (g % 90) * interval '1 day',
           CASE WHEN g % {TESTS_PER_PATIENT} = 0 THEN NULL
                ELSE now() - (g % 90) * interval '1 day' + interval '10 minutes' END
    FROM generate_series(1, {PATIENTS * TESTS_PER_PATIENT}) AS g
    JOIN app.health_test_versions v
      ON v.instrument_id = (
        SELECT id FROM app.health_test_instruments i
        WHERE i.sort_order = (g % {TESTS_PER_PATIENT}) + 1 LIMIT 1)
    ON CONFLICT (id) DO NOTHING;
    """)

    print("Evaluaciones completadas...")
    psql(f"""
    INSERT INTO app.health_test_evaluations (id, assignment_id, patient_id,
        version_id, status, started_at, completed_at, score, score_percentage)
    SELECT gen_random_uuid(), a.id, a.patient_id, a.version_id,
           'completed', a.completed_at - interval '20 minutes',
           a.completed_at, (random() * 100)::numeric(10,2),
           (random() * 100)::numeric(6,2)
    FROM app.health_test_assignments a
    WHERE a.status = 'completed'
      AND NOT EXISTS (SELECT 1 FROM app.health_test_evaluations e
                      WHERE e.assignment_id = a.id)
    ON CONFLICT (id) DO NOTHING;
    """)

    print("Respuestas sintÃ©ticas (una por pregunta)...")
    psql("""
    INSERT INTO app.health_test_responses (id, evaluation_id, question_id,
        answer_option_id, created_at)
    SELECT gen_random_uuid(), e.id, q.id,
           (SELECT o.id FROM app.health_test_answer_options o
            WHERE o.question_id = q.id
            ORDER BY random() LIMIT 1),
           e.completed_at
    FROM app.health_test_evaluations e
    JOIN app.health_test_questions q ON q.version_id = e.version_id
    WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r
                      WHERE r.evaluation_id = e.id AND r.question_id = q.id)
    ON CONFLICT (id) DO NOTHING;
    """)

    print("Resultados sintÃ©ticos (score + subescalas)...")
    psql("""
    INSERT INTO app.health_test_results (id, evaluation_id, result_type, code,
        label, value, qualifier, severity, created_at)
    SELECT gen_random_uuid(), e.id, 'score',
           (SELECT i.code FROM app.health_test_instruments i
            JOIN app.health_test_versions v2 ON v2.instrument_id = i.id
            WHERE v2.id = e.version_id),
           'Score total', e.score,
           CASE WHEN e.score >= 75 THEN 'alto' WHEN e.score >= 40 THEN 'moderado'
                ELSE 'bajo' END,
           CASE WHEN e.score >= 75 THEN 'high' WHEN e.score >= 40 THEN 'moderate'
                ELSE 'low' END,
           e.completed_at
    FROM app.health_test_evaluations e
    WHERE NOT EXISTS (SELECT 1 FROM app.health_test_results r2
                      WHERE r2.evaluation_id = e.id AND r2.result_type = 'score')
    ON CONFLICT (id) DO NOTHING;
    """)

    print("Alertas sintÃ©ticas (20% de evaluaciones con score alto)...")
    psql("""
    INSERT INTO app.health_test_alerts (id, patient_id, result_id, rule_id,
        severity, title, body, status, created_at)
    SELECT gen_random_uuid(), e.patient_id, r.id,
           (SELECT ar.id FROM app.health_test_alert_rules ar LIMIT 1),
           'high', 'Alerta sintÃ©tica ORP', 'Riesgo elevado sintÃ©tico', 'active',
           e.completed_at
    FROM app.health_test_evaluations e
    JOIN app.health_test_results r ON r.evaluation_id = e.id
       AND r.result_type = 'score' AND r.severity IN ('high', 'critical')
    WHERE random() < 0.2
    ON CONFLICT (id) DO NOTHING;
    """)

    print("Listo. Conteos:")
    for table in [
        "patient_profiles",
        "health_test_assignments",
        "health_test_evaluations",
        "health_test_responses",
        "health_test_results",
        "health_test_alerts",
    ]:
        out = subprocess.run(
            [
                "docker",
                "exec",
                "-i",
                "coppAddresd",
                "psql",
                "-U",
                "app_user",
                "-d",
                "coppaddresd_vol_test",
                "-t",
                "-A",
                "-c",
                f"SELECT count(*) FROM app.{table};",
            ],
            capture_output=True,
            text=True,
        )
        print(f"  {table}: {out.stdout.strip()}")


if __name__ == "__main__":
    main()
