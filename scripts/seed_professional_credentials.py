"""Credenciales de los profesionales demo (ERP).

Fija la password `Demo1234!` a los usuarios de profesionales del seed
(seed_professionals_demo.py), que se crean SIN password (solo enlazados).
No toca roles, scopes ni aplicaciones: solo la password, vía el endpoint
interno del Auth Service (POST /api/auth/internal/seed-demo-password).

Uso (desde ai-service):
    uv run python ../coppAddresdBack/scripts/seed_professional_credentials.py

Idempotente: fijar la misma password otra vez es un no-op.

Login en el ERP (coppaddresd-front :3000): email + `Demo1234!`.
"""

from __future__ import annotations

import json
import os
import sys
from pathlib import Path
from urllib import request

# Windows: la consola cp1252 no imprime ✓/✗; forzamos UTF-8 en stdout.
try:
    sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
except AttributeError:
    pass

AUTH_SEED_URL = "http://localhost:5123/api/auth/internal/seed-demo-password"
DEMO_PASSWORD = "Demo1234!"

# Profesionales del seed_professionals_demo.py (con acceso ERP).
DEMO_PROFESSIONAL_EMAILS = [
    "ana.torres@coppaddresd.com",
    "carlos.ruiz@coppaddresd.com",
    "lucia.mendez@coppaddresd.com",
    "pedro.salas@coppaddresd.com",
    "rosa.pineda@coppaddresd.com",
    "felipe.castro@coppaddresd.com",
    "jorge.vega@coppaddresd.com",
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

    raise SystemExit(
        "No se encontró la clave interna del Auth Service. Configura AUTH_INTERNAL_KEY "
        "o crea src/Services/CoppAddresd.Auth/appsettings.json desde appsettings.Example.json."
    )


def reset_password(email: str) -> None:
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
            print(f"  ✓ {result.get('email')}")
    except Exception as exc:  # noqa: BLE001
        print(f"  ✗ {email}: {exc}")


def main() -> None:
    print(f"Credenciales de profesionales demo (password: {DEMO_PASSWORD}):")
    for email in DEMO_PROFESSIONAL_EMAILS:
        reset_password(email)
    print()
    print("Login en el ERP (http://localhost:3000): email + password.")


if __name__ == "__main__":
    main()
