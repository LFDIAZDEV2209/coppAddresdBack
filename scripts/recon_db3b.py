"""Recon 3b: salida completa sin filtros."""

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


print("=== pacientes con document_number NULL ===")
print(
    psql(
        "SELECT 'null_doc=' || count(*) FROM app.patient_profiles WHERE document_number IS NULL OR document_number = '';"
    )
)
print("=== patients sin user_id ===")
print(
    psql(
        "SELECT 'no_user=' || count(*) FROM app.patient_profiles WHERE user_id IS NULL;"
    )
)
print("=== patient_professionals ===")
print(psql("SELECT 'pp_count=' || count(*) FROM app.patient_professionals;"))
print("=== insurers ===")
print(psql("SELECT id || '|' || name FROM app.insurers LIMIT 8;"))
print("=== PRUEBA user existe? ===")
print(
    psql(
        'SELECT "Id" || \'|\' || "UserName" || \'|\' || "IsActive" FROM auth."Users" WHERE "Id" = \'01a024c2-74d0-779d-9eab-826aaf68fe85\';'
    )
)
print("=== clinic_id FK info ===")
print(
    psql(
        "SELECT conname || '|' || pg_get_constraintdef(oid) FROM pg_constraint WHERE conrelid = 'app.patient_profiles'::regclass AND contype = 'f';"
    )
)
print("=== device_tokens columnas ===")
print(
    psql(
        "SELECT column_name FROM information_schema.columns WHERE table_schema='app' AND table_name='device_tokens' ORDER BY ordinal_position;"
    )
)
print("=== asignaciones existentes ===")
print(
    psql(
        "SELECT a.id || '|' || a.patient_id || '|' || a.status || '|' || i.code FROM app.health_test_assignments a JOIN app.health_test_versions v ON v.id = a.version_id JOIN app.health_test_instruments i ON i.id = v.instrument_id ORDER BY a.assigned_at;"
    )
)
print("=== batería activa ===")
print(
    psql(
        "SELECT id || '|' || code || '|' || name FROM app.health_test_batteries WHERE is_active;"
    )
)
print("=== pacientes sin módulos relacionados (junk) sample ===")
print(
    psql(
        "SELECT id || '|' || first_name || ' ' || last_name || '|' || coalesce(document_number,'NULL') FROM app.patient_profiles ORDER BY created_at LIMIT 5;"
    )
)
