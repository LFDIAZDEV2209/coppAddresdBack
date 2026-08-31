"""Recon rápido de la BD dev coppaddresd para diseñar el seed completo."""

import subprocess

CONN_DB = "coppaddresd"


def psql(sql: str) -> str:
    return subprocess.run(
        [
            "docker",
            "exec",
            "-i",
            "coppAddresd",
            "psql",
            "-U",
            "app_user",
            "-d",
            CONN_DB,
            "-t",
            "-A",
        ],
        input=sql,
        text=True,
        check=True,
        capture_output=True,
    ).stdout


print("=== COLUMNAS patient_profiles ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='app' AND table_name='patient_profiles' ORDER BY ordinal_position;"
    )
)
print("=== COLUMNAS patient_professionals ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='app' AND table_name='patient_professionals' ORDER BY ordinal_position;"
    )
)
print("=== COLUMNAS auth.Users ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='auth' AND table_name='Users' ORDER BY ordinal_position;"
    )
)
print("=== COLUMNAS health_test_alerts ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='app' AND table_name='health_test_alerts' ORDER BY ordinal_position;"
    )
)
print("=== COLUMNAS health_test_assignments ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='app' AND table_name='health_test_assignments' ORDER BY ordinal_position;"
    )
)
print("=== COLUMNAS health_test_evaluations ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='app' AND table_name='health_test_evaluations' ORDER BY ordinal_position;"
    )
)
print("=== COLUMNAS health_test_results ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='app' AND table_name='health_test_results' ORDER BY ordinal_position;"
    )
)
print("=== COLUMNAS health_test_comments ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='app' AND table_name='health_test_comments' ORDER BY ordinal_position;"
    )
)
print("=== COLUMNAS erp.professionals ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='erp' AND table_name='professionals' ORDER BY ordinal_position;"
    )
)
print("=== COLUMNAS erp.employees ===")
print(
    psql(
        "SELECT column_name || ':' || data_type FROM information_schema.columns WHERE table_schema='erp' AND table_name='employees' ORDER BY ordinal_position;"
    )
)
print("=== COUNT + muestras ===")
print(psql("SELECT 'patients=' || count(*) FROM app.patient_profiles;"))
print(psql("SELECT 'profs=' || count(*) FROM erp.professionals;"))
print(psql("SELECT 'emps=' || count(*) FROM erp.employees;"))
print(
    psql(
        'SELECT \'prof users=\' || count(*) FROM auth."Users" u JOIN erp.employees e ON e.user_id = u."Id";'
    )
)
print("=== pacientes existentes ===")
print(
    psql(
        "SELECT id || '|' || first_name || ' ' || last_name || '|' || document_number || '|user=' || coalesce(user_id::text,'NULL') || '|' || status FROM app.patient_profiles ORDER BY created_at DESC LIMIT 8;"
    )
)
print("=== profesionales ===")
print(
    psql(
        "SELECT id || '|' || user_id || '|' || first_name || ' ' || last_name || '|' || specialty FROM erp.professionals LIMIT 10;"
    )
)
print("=== insurers ===")
print(psql("SELECT count(*) FROM app.insurers;"))
print("=== instrumentos activos ===")
print(
    psql(
        "SELECT id || '|' || code || '|' || name || '|' || category FROM app.health_test_instruments WHERE is_active ORDER BY sort_order;"
    )
)
print("=== alert rules ===")
print(
    psql(
        "SELECT id || '|' || code || '|' || severity || '|' || condition FROM app.health_test_alert_rules WHERE is_active;"
    )
)
