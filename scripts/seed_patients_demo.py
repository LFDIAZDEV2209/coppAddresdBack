"""Seed de pacientes demo para la app mÃ³vil ANTARES (telemedicina).

Crea perfiles de paciente demo en app.patient_profiles (si no existen) y
aprovisiona su cuenta en el Auth Service (usuario con password conocida +
acceso a la aplicaciÃ³n 'app' + vÃ­nculo app.patient_profiles.user_id) a travÃ©s
del endpoint interno POST /api/auth/internal/seed-patient-demo.

Uso (desde ai-service, donde hay venv/uv):
    uv run --with psycopg[binary] python ../coppAddresdBack/scripts/seed_patients_demo.py

Idempotente: los perfiles se buscan por document_number (no duplica) y el
endpoint del Auth reutiliza el usuario existente en corridas siguientes.

Credenciales resultantes (todas con password `Demo1234!`):
    juan.perez@coppaddresd.com / maria.gomez@coppaddresd.com /
    carlos.rodriguez@coppaddresd.com / ana.martinez@coppaddresd.com /
    luis.fernandez@coppaddresd.com / laura.sanchez@coppaddresd.com

Requisitos: Postgres local (docker compose) y Auth Service corriendo en :5123.
"""

from __future__ import annotations

import json
import os
import uuid
from datetime import date
from pathlib import Path
from urllib import request

import psycopg

DB_DSN = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"
AUTH_SEED_URL = "http://localhost:5123/api/auth/internal/seed-patient-demo"

# Password común para todos los pacientes demo (pruebas locales).
DEMO_PASSWORD = "Demo1234!"

# Usuario del Auth que marca el origen del seed (created_by exige FK a auth.Users).
SEED_USER_EMAIL = "admin@coppaddresd.com"

# Pacientes demo: documento, nombre, email, fecha de nacimiento.
DEMO_PATIENTS = [
    {
        "document": "1000000001",
        "first_name": "Juan",
        "last_name": "Pérez",
        "email": "juan.perez@coppaddresd.com",
        "birth": date(1985, 3, 12),
    },
    {
        "document": "1000000002",
        "first_name": "María",
        "last_name": "Gómez",
        "email": "maria.gomez@coppaddresd.com",
        "birth": date(1990, 7, 25),
    },
    {
        "document": "1000000003",
        "first_name": "Carlos",
        "last_name": "Rodríguez",
        "email": "carlos.rodriguez@coppaddresd.com",
        "birth": date(1978, 11, 3),
    },
    {
        "document": "1000000004",
        "first_name": "Ana",
        "last_name": "Martínez",
        "email": "ana.martinez@coppaddresd.com",
        "birth": date(1995, 1, 18),
    },
    {
        "document": "1000000005",
        "first_name": "Luis",
        "last_name": "Fernández",
        "email": "luis.fernandez@coppaddresd.com",
        "birth": date(1982, 9, 30),
    },
    {
        "document": "1000000006",
        "first_name": "Laura",
        "last_name": "Sánchez",
        "email": "laura.sanchez@coppaddresd.com",
        "birth": date(1992, 5, 7),
    },
]


def stable_patient_id(document: str) -> uuid.UUID:
    """Id determinista por documento: misma corrida â†’ mismo uuid."""
    return uuid.uuid5(uuid.NAMESPACE_DNS, f"demo-patient:{document}")


def auth_internal_key() -> str:
    """Clave interna del Auth Service: appsettings.json local o env AUTH_INTERNAL_KEY."""
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

    raise SystemExit(
        "No se encontrÃ³ la clave interna del Auth Service. Configura AUTH_INTERNAL_KEY "
        "o crea src/Services/CoppAddresd.Auth/appsettings.json desde appsettings.Example.json."
    )


def seed_user_id(conn: psycopg.Connection) -> uuid.UUID:
    """Id del usuario del Auth que marca el origen (created_by → FK a auth.Users)."""
    with conn.cursor() as cur:
        cur.execute(
            'SELECT "Id" FROM auth."Users" WHERE "Email" = %s LIMIT 1',
            (SEED_USER_EMAIL,),
        )
        row = cur.fetchone()
    if row is None:
        raise SystemExit(
            f"El usuario '{SEED_USER_EMAIL}' no existe en auth.Users. "
            "Inicia el Auth Service (corre su seeder) antes del seed."
        )
    return row[0]


def seed_patient_profile(
    conn: psycopg.Connection, patient: dict, created_by: uuid.UUID
) -> uuid.UUID:
    """Crea el perfil del paciente si no existe (idempotente por documento)."""
    with conn.cursor() as cur:
        cur.execute(
            "SELECT id FROM app.patient_profiles WHERE document_number = %s",
            (patient["document"],),
        )
        row = cur.fetchone()
        if row is not None:
            return row[0]

        patient_id = stable_patient_id(patient["document"])
        cur.execute(
            """
            INSERT INTO app.patient_profiles (
                id, first_name, last_name, document_number, email,
                date_of_birth, status, created_by, created_at
            ) VALUES (%s, %s, %s, %s, %s, %s, 'Activo', %s, now())
            ON CONFLICT (id) DO NOTHING
            """,
            (
                patient_id,
                patient["first_name"],
                patient["last_name"],
                patient["document"],
                patient["email"],
                patient["birth"],
                created_by,
            ),
        )
        conn.commit()
        print(
            f"  âœ“ perfil creado: {patient['first_name']} {patient['last_name']} ({patient['document']})"
        )
        return patient_id


def seed_auth_account(patient: dict, patient_id: uuid.UUID) -> None:
    """Aprovisiona usuario + password + acceso 'app' + vÃ­nculo del perfil."""
    payload = json.dumps(
        {
            "patientId": str(patient_id),
            "firstName": patient["first_name"],
            "lastName": patient["last_name"],
            "email": patient["email"],
            "password": DEMO_PASSWORD,
        }
    ).encode("utf-8")

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
            status = "creado" if result.get("created") else "reutilizado"
            print(f"  âœ“ cuenta {status}: {result.get('email')}")
    except Exception as exc:  # noqa: BLE001 â€” el seed reporta y sigue
        print(f"  âœ— error Auth para {patient['email']}: {exc}")


def main() -> None:
    print(f"Seed de pacientes demo ({len(DEMO_PATIENTS)}):")
    with psycopg.connect(DB_DSN) as conn:
        created_by = seed_user_id(conn)
        for patient in DEMO_PATIENTS:
            patient_id = seed_patient_profile(conn, patient, created_by)
            seed_auth_account(patient, patient_id)

    print()
    print(f"Credenciales (password: {DEMO_PASSWORD}):")
    for patient in DEMO_PATIENTS:
        print(f"  {patient['email']}")
    print()
    print(
        "Login en ANTARES: email + password (application 'app'), o flujo OTP por email."
    )


if __name__ == "__main__":
    main()
