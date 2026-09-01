"""Limpia los datos de health tests de los pacientes sembrados (para re-seed limpio)."""

import subprocess

from seed_full_demo_data import PATIENTS

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
        encoding="utf-8",
        errors="replace",
        check=True,
        capture_output=True,
    )
    if "ERROR" in r.stderr:
        raise RuntimeError(r.stderr[:400])
    return r.stdout


def main() -> None:
    docs = ",".join(f"'{p[4]}'" for p in PATIENTS)
    ids = psql(
        "SELECT string_agg('''' || id::text || '''', ',') FROM app.patient_profiles "
        f"WHERE document_number IN ({docs});"
    ).strip()
    if not ids:
        print("sin pacientes sembrados")
        return
    print(
        psql(f"""
    DELETE FROM app.health_test_alerts WHERE patient_id IN ({ids});
    DELETE FROM app.health_test_comments WHERE patient_id IN ({ids});
    DELETE FROM app.health_test_results
      WHERE evaluation_id IN (SELECT id FROM app.health_test_evaluations WHERE patient_id IN ({ids}));
    DELETE FROM app.health_test_responses
      WHERE evaluation_id IN (SELECT id FROM app.health_test_evaluations WHERE patient_id IN ({ids}));
    DELETE FROM app.health_test_evaluations WHERE patient_id IN ({ids});
    DELETE FROM app.health_test_assignments WHERE patient_id IN ({ids});
    DELETE FROM app.health_test_battery_assignments WHERE patient_id IN ({ids});
    SELECT 'ok' AS estado;
    """).strip()
    )


if __name__ == "__main__":
    main()
