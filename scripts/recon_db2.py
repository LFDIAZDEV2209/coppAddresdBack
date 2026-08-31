"""Recon corregido: instrumentos, profesionales reales, basura de volumen."""

import subprocess

DB = "coppaddresd"


def psql(sql: str) -> str:
    r = subprocess.run(
        [
            "docker",
            "exec",
            "-i",
            "coppAddresd",
            "psql",
            "-U",
            "app_user",
            "-d",
            DB,
            "-t",
            "-A",
        ],
        input=sql,
        text=True,
        check=True,
        capture_output=True,
    )
    if r.stderr.strip():
        print("STDERR:", r.stderr.strip()[:500])
    return r.stdout


print("=== instrumentos ===")
print(
    psql(
        "SELECT id || '|' || code || '|' || name || '|' || category || '|' || is_active FROM app.health_test_instruments ORDER BY sort_order;"
    )
)
print("=== versiones activas ===")
print(
    psql(
        "SELECT v.id || '|' || i.code || '|v' || v.version_number || '|' || v.status || '|' || v.scoring_strategy FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id ORDER BY i.sort_order;"
    )
)
print("=== profesionales (con empleado) ===")
print(
    psql(
        "SELECT p.id || '|' || e.first_name || ' ' || e.last_name || '|' || e.user_id || '|' || e.status FROM erp.professionals p JOIN erp.employees e ON e.id = p.employee_id ORDER BY e.first_name LIMIT 15;"
    )
)
print("=== basura: pacientes vacíos ===")
print(
    psql(
        "SELECT 'empty_names=' || count(*) FROM app.patient_profiles WHERE coalesce(first_name,'') = '' AND coalesce(last_name,'') = '';"
    )
)
print(
    psql(
        "SELECT 'named=' || count(*) FROM app.patient_profiles WHERE coalesce(first_name,'') <> '' OR coalesce(last_name,'') <> '';"
    )
)
print("=== asignaciones por origen (¿asignaciones a pacientes vacíos?) ===")
print(psql("SELECT 'assign_total=' || count(*) FROM app.health_test_assignments;"))
print(
    psql(
        "SELECT 'assign_to_empty=' || count(*) FROM app.health_test_assignments a JOIN app.patient_profiles p ON p.id = a.patient_id WHERE coalesce(p.first_name,'') = '';"
    )
)
print("=== evaluaciones/results/respuestas/alerts/comments totales ===")
print(psql("SELECT 'evaluations=' || count(*) FROM app.health_test_evaluations;"))
print(psql("SELECT 'results=' || count(*) FROM app.health_test_results;"))
print(psql("SELECT 'responses=' || count(*) FROM app.health_test_responses;"))
print(psql("SELECT 'alerts=' || count(*) FROM app.health_test_alerts;"))
print(psql("SELECT 'comments=' || count(*) FROM app.health_test_comments;"))
print("=== el paciente PRUEBA ===")
print(
    psql(
        "SELECT id || '|' || first_name || ' ' || last_name || '|' || document_number || '|' || user_id || '|' || date_of_birth || '|' || gender || '|' || phone_number || '|' || email || '|' || insurer_id || '|' || clinic_id FROM app.patient_profiles WHERE id = 'b6928024-89b4-4838-b57d-0e9519bf8eb5';"
    )
)
print("=== insurers sample ===")
print(psql("SELECT id || '|' || code || '|' || name FROM app.insurers LIMIT 5;"))
print("=== score ranges versión 1 de cada instrumento ===")
print(
    psql(
        "SELECT i.code || ':' || r.min_value || '-' || r.max_value || '=' || r.label || '/' || r.severity FROM app.health_test_score_ranges r JOIN app.health_test_versions v ON v.id = r.version_id JOIN app.health_test_instruments i ON i.id = v.instrument_id WHERE v.version_number = 1 AND r.is_active ORDER BY i.code, r.min_value;"
    )
)
