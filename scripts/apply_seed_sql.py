"""Aplica un archivo SQL de Migrations/Seed a la BD local de desarrollo.

Contexto: los seeds de catálogos viajan embebidos en migraciones EF que solo
corren una vez. En una BD ya migrada, regenerar el SQL con
`generate_*_seed.py` NO reaplica nada por sí solo (caso real: `URGENT_CARE`
generado pero ausente en local). Este script ejecuta el archivo tal cual; los
INSERTs usan `ON CONFLICT DO NOTHING`, así que es idempotente y seguro
reejecutar. Solo para desarrollo local, nunca contra producción.

Uso desde coppAddresdBack/scripts con:
  uv run --with psycopg[binary] python apply_seed_sql.py <ruta-al-sql>

Ejemplo:
  uv run --with psycopg[binary] python apply_seed_sql.py ../coppAddresdBack/src/CoppAddresd.Infrastructure/Migrations/Seed/AddProfessionalCatalogs.sql
"""

from __future__ import annotations

import os
import sys
from pathlib import Path

import psycopg

DB_DSN_DEFAULT = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"


def resolve_dsn() -> str:
    """DSN local por defecto; override con la env DB_DSN (p. ej. RDS vía túnel).

    Guard de seguridad: si el host no es local y no se pasa --yes-remote, el
    script se niega a correr (evita escrituras accidentales fuera de dev).
    """
    dsn = os.environ.get("DB_DSN", DB_DSN_DEFAULT)
    if "--yes-remote" not in sys.argv:
        host = dsn.split("host=")[-1].split(" ")[0] if "host=" in dsn else ""
        if host and host not in {"localhost", "127.0.0.1"}:
            raise SystemExit(
                f"DB_DSN apunta a un host remoto ({host}); "
                "repite con --yes-remote si es intencional."
            )
    return dsn


def main() -> None:
    if len(sys.argv) != 2:
        print("Uso: apply_seed_sql.py <ruta-al-archivo-sql>")
        raise SystemExit(2)

    path = Path(sys.argv[1])
    if not path.is_file():
        print(f"No existe el archivo: {path}")
        raise SystemExit(2)

    sql = path.read_text(encoding="utf-8")
    with psycopg.connect(resolve_dsn()) as conn:
        conn.execute(sql)

    print(f"Aplicado (idempotente) {path.name}")


if __name__ == "__main__":
    main()
