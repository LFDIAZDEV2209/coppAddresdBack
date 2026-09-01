"""Credenciales uniformes para TODOS los usuarios demo.

Fija `Demo1234!` a pacientes (coppaddresd.com y mediquer.com) y profesionales,
vía el endpoint interno del Auth Service (POST /api/auth/internal/seed-demo-password),
y asegura acceso a la aplicación correcta (app para pacientes, erp para
profesionales) insertando auth."UserApplications" si falta.

Uso (desde coppAddresdBack/scripts):
    python seed_unified_credentials.py

Idempotente: resetear la misma password es no-op; el INSERT de app usa
ON CONFLICT DO NOTHING.

Resultado: TODOS los usuarios de prueba entran con `Demo1234!`.
"""

from __future__ import annotations

import json
import os
import subprocess
import sys
from pathlib import Path
from urllib import request

try:
    sys.stdout.reconfigure(encoding="utf-8")
except AttributeError:
    pass

AUTH_SEED_URL = "http://localhost:5123/api/auth/internal/seed-demo-password"
DEMO_PASSWORD = "Demo1234!"
DB = "coppaddresd"

# (email, app) — pacientes de la app móvil ANTARES
PATIENT_APP_EMAILS = [
    "juan.perez@coppaddresd.com",
    "maria.gomez@coppaddresd.com",
    "carlos.rodriguez@coppaddresd.com",
    "ana.martinez@coppaddresd.com",
    "luis.fernandez@coppaddresd.com",
    "laura.sanchez@coppaddresd.com",
    "paciente.prueba@mediquer.com",
    "paciente.test.t23@coppaddresd.com",
    "paciente.1@mediquer.com",
    "paciente.2@mediquer.com",
    "paciente.3@mediquer.com",
    "paciente.4@mediquer.com",
    "paciente.5@mediquer.com",
    "paciente.6@mediquer.com",
    "paciente.7@mediquer.com",
    "paciente.8@mediquer.com",
    "paciente.9@mediquer.com",
    "paciente.10@mediquer.com",
    "paciente.11@mediquer.com",
    "paciente.12@mediquer.com",
    "paciente.13@mediquer.com",
    "paciente.14@mediquer.com",
    "paciente.15@mediquer.com",
    "paciente.16@mediquer.com",
    "paciente.17@mediquer.com",
    "paciente.18@mediquer.com",
    "paciente.19@mediquer.com",
    "paciente.20@mediquer.com",
    "paciente.21@mediquer.com",
    "paciente.22@mediquer.com",
    "paciente.23@mediquer.com",
    "paciente.24@mediquer.com",
]

# (email, app) — profesionales del ERP
PROFESSIONAL_ERP_EMAILS = [
    "ana.torres@coppaddresd.com",
    "carlos.ruiz@coppaddresd.com",
    "lucia.mendez@coppaddresd.com",
    "pedro.salas@coppaddresd.com",
    "rosa.pineda@coppaddresd.com",
    "felipe.castro@coppaddresd.com",
    "jorge.vega@coppaddresd.com",
    "maria.gomez@coppaddresd.com",
    "dra.maria@mediquer.com",
    "nutritionist@mediquer.com",
]


def auth_internal_key() -> str:
    env_key = os.environ.get("AUTH_INTERNAL_KEY")
    if env_key:
        return env_key
    appsettings = (
        Path(__file__).resolve().parent.parent
        / "src/Services/CoppAddresd.Auth/appsettings.json"
    )
    if appsettings.exists():
        with appsettings.open(encoding="utf-8") as fh:
            config = json.load(fh)
        key = config.get("Auth", {}).get("InternalApiKey")
        if key:
            return key
    raise SystemExit("No se encontró la clave interna del Auth Service.")


def reset_password(email: str) -> bool:
    payload = json.dumps({"email": email, "password": DEMO_PASSWORD}).encode("utf-8")
    req = request.Request(
        AUTH_SEED_URL,
        data=payload,
        headers={
            "Content-Type": "application/json",
            "X-Internal-Key": auth_internal_key(),
        },
        method="POST",
    )
    try:
        with request.urlopen(req, timeout=15) as resp:
            result = json.loads(resp.read().decode("utf-8"))
            print(f"  ok {result.get('email')}")
            return True
    except Exception as exc:  # noqa: BLE001
        print(f"  FAIL {email}: {exc}")
        return False


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
        raise RuntimeError(r.stderr.strip()[:400])
    return r.stdout


def ensure_app_access(email: str, app_code: str) -> None:
    """Inserta auth."UserApplications" para el usuario si no existe (ON CONFLICT)."""
    psql(
        """
        INSERT INTO auth."UserApplications" ("UserId", "ApplicationId", "CreatedAt")
        SELECT u."Id", a."Id", now()
        FROM auth."Users" u
        CROSS JOIN auth."Applications" a
        WHERE u."UserName" = '{0}' AND a."Code" = '{1}'
        ON CONFLICT DO NOTHING;
        """.format(email.replace("'", "''"), app_code)
    )


def main() -> None:
    print("=== Reset de passwords (Demo1234!) ===")
    all_emails = list(dict.fromkeys(PATIENT_APP_EMAILS + PROFESSIONAL_ERP_EMAILS))
    for email in all_emails:
        reset_password(email)

    print("\n=== Acceso a aplicaciones ===")
    for email in PATIENT_APP_EMAILS:
        ensure_app_access(email, "app")
        print(f"  app  {email}")
    for email in PROFESSIONAL_ERP_EMAILS:
        ensure_app_access(email, "erp")
        print(f"  erp  {email}")

    print("\n=== Verificación (UserApplications) ===")
    print(
        psql(
            """
        SELECT u."UserName", a."Code"
        FROM auth."Users" u
        JOIN auth."UserApplications" ua ON ua."UserId" = u."Id"
        JOIN auth."Applications" a ON a."Id" = ua."ApplicationId"
        WHERE u."UserName" IN ('juan.perez@coppaddresd.com', 'paciente.1@mediquer.com',
            'paciente.24@mediquer.com', 'lucia.mendez@coppaddresd.com', 'felipe.castro@coppaddresd.com')
        ORDER BY u."UserName";
        """
        )
    )
    print("Listo: todos los usuarios demo usan password `Demo1234!`.")


if __name__ == "__main__":
    main()
