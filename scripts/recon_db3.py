"""Recon 3: FKs, junk prefixes, referencias cruzadas."""

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
    if "ERROR" in r.stderr:
        print("STDERR:", r.stderr.strip()[:400])
    return r.stdout


print("=== prefijos de documento de pacientes ===")
print(
    psql(
        "SELECT left(document_number, 3) || ':' || count(*) FROM app.patient_profiles GROUP BY left(document_number, 3) ORDER BY count(*) DESC LIMIT 12;"
    )
)
print("=== clinic_id FK ===")
print(
    psql(
        "SELECT tc.constraint_name || '->' || tc.table_schema || '.' || tc.table_name FROM information_schema.table_constraints tc JOIN information_schema.key_column_usage kcu ON kcu.constraint_name = tc.constraint_name WHERE tc.constraint_type = 'FOREIGN KEY' AND kcu.table_schema = 'app' AND kcu.table_name = 'patient_profiles' AND kcu.column_name = 'clinic_id';"
    )
)
print("=== patient_professionals ===")
print(psql("SELECT 'pp_count=' || count(*) FROM app.patient_professionals;"))
print("=== insurer FK y sample ===")
print(psql("SELECT id || '|' || name FROM app.insurers LIMIT 6;"))
print("=== el user del paciente PRUEBA existe? ===")
print(
    psql(
        'SELECT "Id" || \'|\' || "UserName" || \'|\' || "Email" || \'|\' || "IsActive" FROM auth."Users" WHERE "Id" = \'01a024c2-74d0-779d-9eab-826aaf68fe85\';'
    )
)
print("=== ¿junk VOL referenciado desde otros módulos? ===")
print(
    psql(
        "SELECT 'tele_appts=' || count(*) FROM tele.appointments a JOIN app.patient_profiles p ON p.id = a.patient_id WHERE p.document_number LIKE 'VOL%';"
    )
)
print(
    psql(
        "SELECT 'program_enroll=' || count(*) FROM app.program_enrollments e JOIN app.patient_profiles p ON p.id = e.patient_id WHERE p.document_number LIKE 'VOL%';"
    )
)
print(
    psql(
        "SELECT 'wellness_np=' || count(*) FROM app.nutrition_plan_assignments a JOIN app.patient_profiles p ON p.id = a.patient_id WHERE p.document_number LIKE 'VOL%';"
    )
)
print(
    psql(
        "SELECT 'device_tokens=' || count(*) FROM app.device_tokens d JOIN app.patient_profiles p ON p.id = d.patient_id WHERE p.document_number LIKE 'VOL%';"
    )
)
print("=== asignaciones existentes (a qué pacientes) ===")
print(
    psql(
        "SELECT a.id || '|' || a.patient_id || '|' || a.status || '|' || i.code FROM app.health_test_assignments a JOIN app.health_test_versions v ON v.id = a.version_id JOIN app.health_test_instruments i ON i.id = v.instrument_id;"
    )
)
print("=== batería inicial ===")
print(
    psql(
        "SELECT id || '|' || code || '|' || name FROM app.health_test_batteries WHERE is_active;"
    )
)
print("=== preguntas por versión (muestra temperamento) ===")
print(
    psql(
        "SELECT section || ':' || count(*) FROM app.health_test_questions q JOIN app.health_test_versions v ON v.id = q.version_id JOIN app.health_test_instruments i ON i.id = v.instrument_id WHERE i.code = 'temperamento' GROUP BY section ORDER BY section;"
    )
)
